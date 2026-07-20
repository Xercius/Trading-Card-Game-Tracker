using api.Data;
using api.Features.Admin.Import;
using api.Importing;
using api.Models;
using api.Shared.Importing;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace api.Tests.Features.AdminImport;

public sealed class AdminImportControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DryRun_Remote_ComputesSummary_WithoutPersisting()
    {
        var (client, services, _) = CreateClientWithTestImporter(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };

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
        var (client, _, _) = CreateClientWithTestImporter(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.Equal(ImportingOptions.DefaultPreviewLimit, TestImporter.LastLimit);
    }

    [Fact]
    public async Task Apply_Remote_IsIdempotent()
    {
        var (client, _, _) = CreateClientWithTestImporter(factory);

        var first = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "BETA" }),
        };
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
        var (client, _, _) = CreateClientWithTestImporter(factory);
        client.WithUser(TestDataSeeder.AdminUserId);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/import/dry-run?limit={limit}")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };

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
        var (client, _, _) = CreateClientWithTestImporter(factory);
        using var form = new MultipartFormDataContent();
        var csv = "name,set,number\nLightning Bolt,Alpha,1\nLightning Bolt,Alpha,1\n";
        form.Add(new StringContent("dummy"), "source");
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(csv))), "file", "cards.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run") { Content = form };

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
        var (client, _, _) = CreateClientWithTestImporter(factory);
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

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.Equal(2, TestImporter.LastLimit);
        Assert.Equal(2, TestImporter.LastPreviewRowCount);
    }

    [Fact]
    public async Task DryRun_Logs_Timing_And_Summary_Metadata()
    {
        var loggerProvider = new TestLoggerProvider();
        var (client, _, provider) = CreateClientWithTestImporter(factory, loggerProvider);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "ALP" }),
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var entries = provider.Entries
            .Where(e => e.Category.Contains(nameof(AdminImportController)))
            .ToArray();

        Assert.NotEmpty(entries);
        var completion = entries.Last(e => e.Level == LogLevel.Information && e.State.Any(s => s.Key == "Operation" && s.Value?.ToString() == "dry-run"));
        var state = completion.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Assert.True(state.TryGetValue("DurationMs", out var durationObj));
        Assert.True(Convert.ToInt64(durationObj) >= 0);
        Assert.Equal("dummy", state["Source"]);
        Assert.Equal("remote", state["Mode"]);
        Assert.Equal(true, state["Success"]);
        Assert.True(state.ContainsKey("ItemCount"));
    }

    [Fact]
    public async Task DryRun_File_InvalidCsv_ReturnsProblem()
    {
        var (client, _, _) = CreateClientWithTestImporter(factory);
        using var form = new MultipartFormDataContent();
        var csv = "name,number\nLightning Bolt,1\n";
        form.Add(new StringContent("dummy"), "source");
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(csv))), "file", "cards.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run") { Content = form };

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

    [Fact]
    public async Task Apply_Remote_Stores_SyncHistory_After_Success()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithTestImporter(factory);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "SOR" }),
        };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.ImportSyncHistories
            .FirstOrDefaultAsync(h => h.ImporterKey == "dummy" && h.SetCode == "SOR");

        Assert.NotNull(history);
        // The recorded timestamp should be close to now.
        Assert.True(DateTimeOffset.UtcNow - history!.LastSyncedAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Apply_Remote_Updates_SyncHistory_On_Repeat_Run()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithTestImporter(factory);

        async Task<DateTimeOffset> RunApply()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
            {
                Content = JsonContent.Create(new { source = "dummy", set = "SHD" }),
            };
            (await client.SendAsync(request)).EnsureSuccessStatusCode();

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var h = await db.ImportSyncHistories.FirstAsync(h => h.ImporterKey == "dummy" && h.SetCode == "SHD");
            return h.LastSyncedAt;
        }

        var first = await RunApply();
        await Task.Delay(10); // ensure clock advances
        var second = await RunApply();

        // The second run should have updated the timestamp.
        Assert.True(second >= first);

        // There should be exactly one row, not two.
        using var finalScope = services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await finalDb.ImportSyncHistories.CountAsync(h => h.ImporterKey == "dummy" && h.SetCode == "SHD");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Apply_Remote_Uses_Single_Setless_SyncHistory_Row()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithTestImporter(factory);

        async Task RunApply()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
            {
                Content = JsonContent.Create(new { source = "dummy" }),
            };
            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        }

        await RunApply();
        await RunApply();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.ImportSyncHistories
            .Where(h => h.ImporterKey == "dummy")
            .ToArrayAsync();

        Assert.Single(history);
        Assert.Equal(string.Empty, history[0].SetCode);

        var historyResponse = await client.GetFromJsonAsync<ImportSyncHistoryEntry[]>("/api/admin/import/history");
        Assert.NotNull(historyResponse);
        Assert.Contains(historyResponse!, h => h.ImporterKey == "dummy" && h.SetCode is null);
    }

    [Fact]
    public async Task DryRun_Does_Not_Store_SyncHistory()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithTestImporter(factory);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "JTL" }),
        };
        (await client.SendAsync(request)).EnsureSuccessStatusCode();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.ImportSyncHistories.AnyAsync(h => h.ImporterKey == "dummy" && h.SetCode == "JTL");
        Assert.False(exists);
    }

    [Fact]
    public async Task GetHistory_Returns_All_SyncHistory_Entries()
    {
        await factory.ResetDatabaseAsync();
        var (client, _, _) = CreateClientWithTestImporter(factory);

        // Seed two apply runs with different set codes.
        async Task Apply(string set)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
            {
                Content = JsonContent.Create(new { source = "dummy", set }),
            };
            (await client.SendAsync(req)).EnsureSuccessStatusCode();
        }

        await Apply("SOR");
        await Apply("SHD");

        var historyResponse = await client.GetFromJsonAsync<ImportSyncHistoryEntry[]>("/api/admin/import/history");
        Assert.NotNull(historyResponse);
        Assert.Contains(historyResponse!, h => h.ImporterKey == "dummy" && h.SetCode == "SOR");
        Assert.Contains(historyResponse!, h => h.ImporterKey == "dummy" && h.SetCode == "SHD");
    }

    [Fact]
    public async Task Apply_Remote_PassesUpdatedSince_To_Importer()
    {
        await factory.ResetDatabaseAsync();
        var (client, _, _) = CreateClientWithTestImporter(factory);

        var since = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/apply")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "SOR", updatedSince = since }),
        };
        (await client.SendAsync(request)).EnsureSuccessStatusCode();

        Assert.NotNull(TestImporter.LastUpdatedSince);
        Assert.Equal(since, TestImporter.LastUpdatedSince);
    }

    [Fact]
    public async Task DryRun_PassesUpdatedSince_To_Importer()
    {
        var (client, _, _) = CreateClientWithTestImporter(factory);

        var since = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/dry-run")
        {
            Content = JsonContent.Create(new { source = "dummy", set = "SOR", updatedSince = since }),
        };
        (await client.SendAsync(request)).EnsureSuccessStatusCode();

        Assert.NotNull(TestImporter.LastUpdatedSince);
        Assert.Equal(since, TestImporter.LastUpdatedSince);
    }

    private static (HttpClient Client, IServiceProvider Services, TestLoggerProvider LoggerProvider) CreateClientWithTestImporter(CustomWebApplicationFactory factory, TestLoggerProvider? loggerProvider = null)
    {
        TestImporter.Reset();
        var customized = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ISourceImporter));
                services.AddScoped<ISourceImporter, TestImporter>();
            });

            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
            }
        });

        var client = customized.CreateClient().WithUser(TestDataSeeder.AdminUserId);
        return (client, customized.Services, loggerProvider ?? new TestLoggerProvider());
    }

    private sealed class TestImporter(AppDbContext db) : ISourceImporter
    {
        public static int? LastLimit { get; private set; }
        public static int LastPreviewRowCount { get; private set; }
        public static DateTimeOffset? LastUpdatedSince { get; private set; }

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
                if (processed >= effectiveLimit) break;
                processed++;
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
            LastUpdatedSince = options.UpdatedSince;
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
            LastUpdatedSince = null;
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

internal sealed record TestLogEntry(string Category, LogLevel Level, IReadOnlyList<KeyValuePair<string, object?>> State, string Message, Exception? Exception);

internal sealed class NullScopeProvider : IExternalScopeProvider
{
    public static NullScopeProvider Instance { get; } = new();

    private NullScopeProvider() { }

    public void ForEachScope<TState>(Action<object?, TState> callback, TState state) { }

    public IDisposable Push(object? state) => NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        private NullScope() { }
        public void Dispose() { }
    }
}

internal sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();
    private IExternalScopeProvider _scopeProvider = NullScopeProvider.Instance;

    public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, _entries, () => _scopeProvider);

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider ?? NullScopeProvider.Instance;
    }

    private sealed class TestLogger(
        string categoryName,
        ConcurrentQueue<TestLogEntry> sink,
        Func<IExternalScopeProvider> scopeProviderAccessor) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => scopeProviderAccessor().Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var stateValues = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            sink.Enqueue(new TestLogEntry(categoryName, logLevel, stateValues, formatter(state, exception), exception));
        }
    }
}
