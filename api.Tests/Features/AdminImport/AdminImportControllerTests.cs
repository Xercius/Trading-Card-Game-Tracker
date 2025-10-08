using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using api.Data;
using api.Features.Admin.Import;
using api.Importing;
using api.Models;
using api.Shared.Importing;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace api.Tests.Features.AdminImport;

public sealed class AdminImportControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DryRun_Remote_ComputesSummary_WithoutPersisting()
    {
        var (client, services) = CreateClientWithTestImporter(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var preview = await response.Content.ReadFromJsonAsync<ImportPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.Summary.New);
        Assert.Equal(0, preview.Summary.Update);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Cards.AnyAsync(c => c.Game == "Test Game" && c.Name == "Card ALP");
        Assert.False(persisted);
    }

    [Fact]
    public async Task DryRun_Limit_Defaults_To_100()
    {
        var (client, _) = CreateClientWithTestImporter(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.Equal(ImportingOptions.DefaultPreviewLimit, TestImporter.LastLimit);
    }

    [Fact]
    public async Task Apply_Remote_IsIdempotent()
    {
        var (client, _) = CreateClientWithTestImporter(factory);

        var first = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "BETA" }),
        };
        first.Headers.Add("X-User-Id", "1");
        var firstResponse = await client.SendAsync(first);
        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ImportApplyResponse>();
        Assert.NotNull(firstPayload);
        Assert.Equal(1, firstPayload!.Created);
        Assert.Equal(0, firstPayload.Updated);

        var second = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "BETA" }),
        };
        second.Headers.Add("X-User-Id", "1");
        var secondResponse = await client.SendAsync(second);
        secondResponse.EnsureSuccessStatusCode();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ImportApplyResponse>();
        Assert.NotNull(secondPayload);
        Assert.Equal(0, secondPayload!.Created);
        Assert.Equal(0, secondPayload.Updated);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("5000")]
    public async Task DryRun_Limit_Rejected_When_Negative_Or_Zero_Or_TooLarge(string limit)
    {
        var (client, _) = CreateClientWithTestImporter(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/import/dry-run?limit={limit}")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid limit", problem!.Title);
        Assert.True(problem.Errors.TryGetValue("limit", out var messages));
        Assert.Contains($"limit must be between {ImportingOptions.MinPreviewLimit} and {ImportingOptions.MaxPreviewLimit}", messages);
    }

    [Fact]
    public async Task DryRun_File_FlagsDuplicates()
    {
        var (client, _) = CreateClientWithTestImporter(factory);
        using var form = new MultipartFormDataContent();
        var csv = "name,set,number\nLightning Bolt,Alpha,1\nLightning Bolt,Alpha,1\n";
        form.Add(new StringContent("dummy"), "source");
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(csv))), "file", "cards.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run") { Content = form };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var preview = await response.Content.ReadFromJsonAsync<ImportPreviewResponse>();
        Assert.NotNull(preview);
        Assert.True(preview!.Summary.Invalid >= 1);
        Assert.Contains(preview.Rows, r => r.Status == "Invalid");
    }

    [Fact]
    public async Task DryRun_Respects_Limit_Caps_Number_Of_Preview_Rows()
    {
        var (client, _) = CreateClientWithTestImporter(factory);
        using var form = new MultipartFormDataContent();
        var builder = new StringBuilder();
        builder.AppendLine("name,set,number");
        for (var i = 0; i < 5; i++)
        {
            builder.AppendLine($"Lightning Bolt {i},Alpha,{i}");
        }

        form.Add(new StringContent("dummy"), "source");
        form.Add(new StringContent("2"), "limit");
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()))), "file", "cards.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run") { Content = form };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.Equal(2, TestImporter.LastLimit);
        Assert.Equal(2, TestImporter.LastPreviewRowCount);
    }

    [Fact]
    public async Task DryRun_File_InvalidCsv_ReturnsProblem()
    {
        var (client, _) = CreateClientWithTestImporter(factory);
        using var form = new MultipartFormDataContent();
        var csv = "name,number\nLightning Bolt,1\n";
        form.Add(new StringContent("dummy"), "source");
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(csv))), "file", "cards.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run") { Content = form };
        request.Headers.Add("X-User-Id", "1");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("CSV missing required columns.", problem!.Title);
        Assert.Contains("missing", problem.Errors.Keys);
    }

    [Fact]
    public async Task FileParser_Returned_Stream_Is_Readable_And_NotDisposed()
    {
        var parser = new FileParser();
        var bytes = Encoding.UTF8.GetBytes("name,set,number\nLightning Bolt,Alpha,1\n");
        using var upload = new MemoryStream(bytes);
        var file = new FormFile(upload, 0, bytes.Length, "file", "cards.csv");
        upload.Position = 0;

        using var result = await parser.ParseAsync(file, ImportingOptions.DefaultPreviewLimit);
        Assert.True(result.Stream.CanRead);
        result.Stream.Position = 0;
        using var reader = new StreamReader(result.Stream, leaveOpen: true);
        var content = reader.ReadToEnd();
        Assert.Contains("Lightning Bolt", content);
    }

    private static (HttpClient Client, IServiceProvider Services) CreateClientWithTestImporter(CustomWebApplicationFactory factory)
    {
        TestImporter.Reset();
        var customized = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ISourceImporter));
                services.AddScoped<ISourceImporter, TestImporter>();
            });
        });

        return (customized.CreateClient(), customized.Services);
    }

    private sealed class TestImporter(AppDbContext db) : ISourceImporter
    {
        public static int? LastLimit { get; private set; }
        public static int LastPreviewRowCount { get; private set; }

        public string Key => "dummy";
        public string DisplayName => "Dummy Importer";
        public IEnumerable<string> SupportedGames => new[] { "Test Game" };

        public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        {
            var effectiveLimit = options.Limit ?? ImportingOptions.DefaultPreviewLimit;
            LastLimit = effectiveLimit;

            file.Position = 0;
            using var reader = new StreamReader(file, leaveOpen: true);
            var content = reader.ReadToEnd();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processed = 0;
            foreach (var line in lines.Skip(1))
            {
                if (processed++ >= effectiveLimit) break;
                if (!seen.Add(line)) duplicates.Add(line);
            }

            LastPreviewRowCount = processed;

            var summary = CreateSummary(options);
            foreach (var dup in duplicates)
            {
                summary.Errors++;
                summary.Messages.Add($"Error [duplicate] {dup}");
            }

            return Task.FromResult(summary);
        }

        public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
        {
            LastLimit = options.Limit ?? ImportingOptions.DefaultPreviewLimit;
            LastPreviewRowCount = 0;
            return await db.WithDryRunAsync(options.DryRun, async () =>
            {
                var summary = CreateSummary(options);
                var set = options.SetCode ?? "GEN";
                var name = $"Card {set}";
                var card = await db.Cards.FirstOrDefaultAsync(c => c.Game == "Test Game" && c.Name == name, ct);
                if (card is null)
                {
                    card = new Card
                    {
                        Game = "Test Game",
                        Name = name,
                        CardType = "Test",
                        Description = null,
                        DetailsJson = "{}",
                    };
                    db.Cards.Add(card);
                    summary.CardsCreated++;
                }

                await db.SaveChangesAsync(ct);
                return summary;
            });
        }

        public static void Reset()
        {
            LastLimit = null;
            LastPreviewRowCount = 0;
        }

        private static ImportSummary CreateSummary(ImportOptions options) => new()
        {
            Source = "dummy",
            DryRun = options.DryRun,
            CardsCreated = 0,
            CardsUpdated = 0,
            PrintingsCreated = 0,
            PrintingsUpdated = 0,
            Errors = 0,
            Messages = new List<string>(),
        };
    }
}
