using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Importing;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Features.AdminSync;

public sealed class AdminSyncControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task RunStarWarsUnlimited_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR");

        var response = await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenAuthenticated_ReturnsSucceededStatus_And_UpdatesSyncHistory()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR", "SHD");
        SyncTestImporter.SetSummary("SOR", created: 2, updated: 1, invalid: 0, "SOR complete");
        SyncTestImporter.SetSummary("SHD", created: 1, updated: 3, invalid: 1, "SHD complete");

        client.AsAdmin();
        var response = await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swu", payload!.Source);
        Assert.Equal("Succeeded", payload.Status);
        Assert.Equal(2, payload.SetCount);
        Assert.Equal(3, payload.Created);
        Assert.Equal(4, payload.Updated);
        Assert.Equal(1, payload.Invalid);
        Assert.Contains("SOR complete", payload.Messages);
        Assert.Contains("SHD complete", payload.Messages);

        Assert.Equal(["SHD", "SOR"], SyncTestImporter.RequestedSetCodes.OrderBy(x => x).ToArray());

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncHistory = await db.ImportSyncHistories
            .Where(h => h.ImporterKey == "swu")
            .OrderBy(h => h.SetCode)
            .ToArrayAsync();

        Assert.Equal(2, syncHistory.Length);
        Assert.Equal("SHD", syncHistory[0].SetCode);
        Assert.Equal("SOR", syncHistory[1].SetCode);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenSyncAlreadyRunning_ReturnsConflictStatus()
    {
        await factory.ResetDatabaseAsync();
        var (firstClient, services, customizedFactory) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR");
        SyncTestImporter.SetSummary("SOR", created: 1, updated: 0, invalid: 0, "SOR complete");
        SyncTestImporter.BlockNextImport();

        firstClient.AsAdmin();
        var firstRequest = firstClient.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);
        await SyncTestImporter.WaitForImportToStartAsync();

        using var secondClient = customizedFactory.CreateClient().AsAdmin();
        var secondResponse = await secondClient.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var payload = await secondResponse.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Running", payload!.Status);
        Assert.Contains("already running", payload.Messages[0], StringComparison.OrdinalIgnoreCase);

        SyncTestImporter.ReleaseImport();
        var completed = await firstRequest;
        completed.EnsureSuccessStatusCode();
    }

    private static (HttpClient Client, IServiceProvider Services, WebApplicationFactory<Program> Factory) CreateClientWithSyncImporter(CustomWebApplicationFactory factory)
    {
        SyncTestImporter.Reset();
        var customized = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ISourceImporter));
                services.AddScoped<ISourceImporter, SyncTestImporter>();
            });
        });

        return (customized.CreateClient(), customized.Services, customized);
    }

    private static async Task SeedSwuSetAsync(IServiceProvider services, params string[] codes)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var code in codes)
        {
            if (await db.SwuSets.AnyAsync(s => s.Code == code))
            {
                continue;
            }

            db.SwuSets.Add(new SwuSet
            {
                Code = code,
                Name = code,
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private sealed class SyncTestImporter : ISourceImporter
    {
        private static readonly ConcurrentDictionary<string, ImportSummary> Summaries = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentBag<string> RequestedSetsInternal = [];
        private static TaskCompletionSource<bool>? _started;
        private static TaskCompletionSource<bool>? _release;

        public string Key => "swu";

        public string DisplayName => "SWU Test Importer";

        public IEnumerable<string> SupportedGames => [ "Star Wars Unlimited" ];

        public static IReadOnlyCollection<string> RequestedSetCodes => RequestedSetsInternal.ToArray();

        public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
        {
            var setCode = options.SetCode ?? string.Empty;
            RequestedSetsInternal.Add(setCode);
            _started?.TrySetResult(true);

            if (_release is not null)
            {
                await _release.Task.WaitAsync(ct);
            }

            return Summaries.TryGetValue(setCode, out var summary)
                ? Clone(summary)
                : new ImportSummary { Source = "swu", DryRun = false };
        }

        public static void BlockNextImport()
        {
            _started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public static Task WaitForImportToStartAsync() => _started?.Task ?? Task.CompletedTask;

        public static void ReleaseImport() => _release?.TrySetResult(true);

        public static void SetSummary(string setCode, int created, int updated, int invalid, string message)
        {
            Summaries[setCode] = new ImportSummary
            {
                Source = "swu",
                DryRun = false,
                CardsCreated = created,
                CardsUpdated = updated,
                Errors = invalid,
                Messages = [message]
            };
        }

        public static void Reset()
        {
            Summaries.Clear();
            while (RequestedSetsInternal.TryTake(out _))
            {
            }

            _started = null;
            _release = null;
        }

        private static ImportSummary Clone(ImportSummary summary) => new()
        {
            Source = summary.Source,
            DryRun = summary.DryRun,
            CardsCreated = summary.CardsCreated,
            CardsUpdated = summary.CardsUpdated,
            PrintingsCreated = summary.PrintingsCreated,
            PrintingsUpdated = summary.PrintingsUpdated,
            Errors = summary.Errors,
            Messages = [.. summary.Messages]
        };
    }
}
