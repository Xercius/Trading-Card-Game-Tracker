using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using api.Data;
using api.Features.Admin.Import;
using api.Importing;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc;
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

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("CSV missing required columns.", problem!.Title);
    }

    private static (HttpClient Client, IServiceProvider Services) CreateClientWithTestImporter(CustomWebApplicationFactory factory)
    {
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
        public string Key => "dummy";
        public string DisplayName => "Dummy Importer";
        public IEnumerable<string> SupportedGames => new[] { "Test Game" };

        public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        {
            file.Position = 0;
            using var reader = new StreamReader(file, leaveOpen: true);
            var content = reader.ReadToEnd();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                if (!seen.Add(line)) duplicates.Add(line);
            }

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
