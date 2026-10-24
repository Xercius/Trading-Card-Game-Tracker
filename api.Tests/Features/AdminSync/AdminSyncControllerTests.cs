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
using System.Web;
using Xunit;

namespace api.Tests.Features.AdminSync;

public sealed class AdminSyncControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetStarWarsUnlimitedStatus_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var (client, _, _) = CreateClientWithSyncImporter(factory);

        var response = await client.GetAsync("/api/admin/sync/star-wars-unlimited/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStarWarsUnlimitedStatus_WhenAuthenticated_ReturnsIdleStatus_And_HistoryDetails()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        await SeedSyncHistoryAsync(services, "SHD", new DateTimeOffset(2026, 7, 21, 11, 30, 0, TimeSpan.Zero));

        client.AsAdmin();
        var response = await client.GetAsync("/api/admin/sync/star-wars-unlimited/status");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swu", payload!.Source);
        Assert.Equal("Idle", payload.Status);
        Assert.Null(payload.RunningSince);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 11, 30, 0, TimeSpan.Zero), payload.LastCompletedAt);
        Assert.Equal(2, payload.HistoryCount);
        Assert.Empty(payload.Messages);
        Assert.Equal(["SHD", "SOR"], payload.History.Select(h => h.SetCode).ToArray());
    }

    [Fact]
    public async Task GetStarWarsUnlimitedStatus_WhenSyncRunning_ReturnsRunningStatus()
    {
        await factory.ResetDatabaseAsync();
        var (firstClient, services, customizedFactory) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        SyncTestImporter.SetSummary("SOR", created: 1, updated: 0, invalid: 0, "SOR complete");
        SyncTestImporter.BlockNextImport();

        firstClient.AsAdmin();
        var firstRequest = firstClient.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);
        await SyncTestImporter.WaitForImportToStartAsync();

        using var secondClient = customizedFactory.CreateClient().AsAdmin();
        var statusResponse = await secondClient.GetAsync("/api/admin/sync/star-wars-unlimited/status");
        statusResponse.EnsureSuccessStatusCode();

        var payload = await statusResponse.Content.ReadFromJsonAsync<AdminSyncStatusDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Running", payload!.Status);
        Assert.NotNull(payload.RunningSince);

        SyncTestImporter.ReleaseImport();
        var completed = await firstRequest;
        completed.EnsureSuccessStatusCode();
    }

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
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        SyncTestImporter.SetSummary("SOR", created: 2, updated: 1, invalid: 0, "SOR complete");
        SyncTestImporter.SetSummary("SHD", created: 1, updated: 3, invalid: 0, "SHD complete");

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
        Assert.Equal(0, payload.Invalid);
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
    public async Task RunStarWarsUnlimited_WithRealImporter_UpdatesCardsAndPersistsSyncMetadata()
    {
        await factory.ResetDatabaseAsync();
        var remoteCards = new RecordingSwuApiClient(
        [
            CreateRecord(
                id: 3001,
                title: "Grand Moff Tarkin",
                cardUid: "3001",
                serialCode: "SOR-020",
                setCode: "SOR",
                setName: "Spark of Rebellion",
                typeName: "Leader",
                text: "Updated text.",
                artist: "Updated Artist",
                cost: 4,
                power: 3,
                health: 5,
                aspects: ["Villainy", "Command"],
                traits: ["Imperial", "Leader"],
                rarity: "Legendary",
                imageUrl: "https://example.test/tarkin-updated.png",
                createdAt: new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
                updatedAt: new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                id: 3002,
                title: "Luke Skywalker",
                cardUid: "3002",
                serialCode: "SOR-021",
                setCode: "SOR",
                setName: "Spark of Rebellion",
                text: "Deal 3 damage to a unit.",
                artist: "Artist Two",
                cost: 5,
                power: 4,
                health: 6,
                aspects: ["Heroism", "Command"],
                traits: ["Rebel", "Jedi"],
                keywords: ["Restore"],
                rarity: "Rare",
                imageUrl: "https://example.test/luke.png",
                createdAt: new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.Zero),
                updatedAt: new DateTimeOffset(2026, 7, 21, 12, 5, 0, TimeSpan.Zero))
        ]);
        var (client, services, _) = CreateClientWithSwuApiClient(factory, remoteCards);
        await SeedSwuSetAsync(services, "SOR");

        using (var seedScope = services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cards.Add(new Card
            {
                Game = "Star Wars Unlimited",
                Name = "Grand Moff Tarkin",
                CardType = "Leader",
                Description = "Original text.",
                DetailsJson = "{\"version\":1}"
            });
            await db.SaveChangesAsync();

            var existingCard = await db.Cards.SingleAsync(c => c.Game == "Star Wars Unlimited" && c.Name == "Grand Moff Tarkin");
            db.CardPrintings.Add(new CardPrinting
            {
                CardId = existingCard.Id,
                Set = "SOR",
                Number = "SOR-020",
                Rarity = "Common",
                Style = "Standard",
                ImageUrl = "https://example.test/tarkin-original.png",
                DetailsJson = "{\"version\":1}"
            });
            db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = "swu",
                SetCode = "SOR",
                LastSyncedAt = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero)
            });
            await db.SaveChangesAsync();
        }

        client.AsAdmin();
        var response = await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swu", payload!.Source);
        Assert.Equal("Succeeded", payload.Status);
        Assert.Equal(1, payload.SetCount);
        Assert.Equal(2, payload.Created);
        Assert.Equal(2, payload.Updated);
        Assert.Equal(0, payload.Invalid);
        Assert.Contains(payload.Messages, message => message.Contains("Processed 2 records", StringComparison.Ordinal));

        Assert.Equal(1, remoteCards.GetAllCardsCallCount);
        Assert.NotNull(remoteCards.CapturedFilter);
        Assert.Equal(42, remoteCards.CapturedFilter!.ExpansionId);
        Assert.Null(remoteCards.CapturedFilter.ExpansionCode);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 7, 59, 0, TimeSpan.Zero), remoteCards.CapturedFilter.UpdatedSince);

        using var scope = services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedCard = await appDb.Cards
            .Include(c => c.Printings)
            .SingleAsync(c => c.Game == "Star Wars Unlimited" && c.Name == "Grand Moff Tarkin");
        Assert.Equal("Updated text.", updatedCard.Description);
        Assert.Contains("\"Updated text.\"", updatedCard.DetailsJson, StringComparison.Ordinal);

        var updatedPrinting = Assert.Single(updatedCard.Printings);
        Assert.Equal("Legendary", updatedPrinting.Rarity);
        Assert.Equal("https://example.test/tarkin-updated.png", updatedPrinting.ImageUrl);

        var newCard = await appDb.Cards
            .Include(c => c.Printings)
            .SingleAsync(c => c.Game == "Star Wars Unlimited" && c.Name == "Luke Skywalker");
        Assert.Equal("Deal 3 damage to a unit.", newCard.Description);
        var newPrinting = Assert.Single(newCard.Printings);
        Assert.Equal("SOR", newPrinting.Set);
        Assert.Equal("SOR-021", newPrinting.Number);
        Assert.Equal("Rare", newPrinting.Rarity);

        var syncHistory = await appDb.ImportSyncHistories
            .Where(h => h.ImporterKey == "swu" && h.SetCode == "SOR")
            .ToArrayAsync();
        var historyEntry = Assert.Single(syncHistory);
        Assert.True(historyEntry.LastSyncedAt >= payload.StartedAt);
        Assert.True(historyEntry.LastSyncedAt <= payload.CompletedAt);

        var syncLog = await appDb.SyncLogs
            .Include(log => log.SwuSet)
            .SingleAsync(log => log.SwuSet != null && log.SwuSet.Code == "SOR");
        Assert.Equal("Succeeded", syncLog.Status);
        Assert.True(syncLog.IsIncremental);
        Assert.Equal(remoteCards.CapturedFilter.UpdatedSince, syncLog.UpdatedSince);
        Assert.Equal(4, syncLog.CardsUpserted);
        Assert.NotNull(syncLog.CompletedAt);
        Assert.Null(syncLog.ErrorMessage);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenSyncAlreadyRunning_ReturnsConflictStatus()
    {
        await factory.ResetDatabaseAsync();
        var (firstClient, services, customizedFactory) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
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

    [Fact]
    public async Task GetStarWarsUnlimitedLogs_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var (client, _, _) = CreateClientWithSyncImporter(factory);

        var response = await client.GetAsync("/api/admin/sync/star-wars-unlimited/logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStarWarsUnlimitedLogs_WhenNoLogs_ReturnsEmptyList()
    {
        await factory.ResetDatabaseAsync();
        var (client, _, _) = CreateClientWithSyncImporter(factory);

        client.AsAdmin();
        var response = await client.GetAsync("/api/admin/sync/star-wars-unlimited/logs");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncLogsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swu", payload!.Source);
        Assert.Equal(0, payload.TotalCount);
        Assert.Empty(payload.Logs);
    }

    [Fact]
    public async Task GetStarWarsUnlimitedLogs_AfterSync_ReturnsSyncLogEntries()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        SyncTestImporter.SetSummary("SOR", created: 2, updated: 1, invalid: 0, "SOR complete");

        client.AsAdmin();
        await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        var logsResponse = await client.GetAsync("/api/admin/sync/star-wars-unlimited/logs");
        logsResponse.EnsureSuccessStatusCode();

        var payload = await logsResponse.Content.ReadFromJsonAsync<AdminSyncLogsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Logs);
        Assert.Equal("Succeeded", payload.Logs[0].Status);
        Assert.Equal("SOR", payload.Logs[0].SetCode);
        Assert.Null(payload.Logs[0].ErrorMessage);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenRecordsRejected_ReportsFailureAndPreservesCheckpoint()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        await SeedSwuSetAsync(services, "SOR", "SHD");
        var checkpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        await SeedSyncHistoryAsync(services, "SOR", checkpoint);
        SyncTestImporter.SetSummary("SOR", created: 1, updated: 0, invalid: 1, "Invalid card");
        SyncTestImporter.SetSummary("SHD", created: 1, updated: 0, invalid: 0, "SHD complete");

        var response = await client.AsAdmin().PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Failed", payload!.Status);
        Assert.Equal(1, payload.Invalid);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync(h => h.SetCode == "SOR")).LastSyncedAt);
        Assert.True(await db.ImportSyncHistories.AnyAsync(h => h.SetCode == "SHD"));
        var logs = await db.SyncLogs.Include(l => l.SwuSet).ToArrayAsync();
        Assert.Equal("Failed", logs.Single(l => l.SwuSet!.Code == "SOR").Status);
        Assert.Equal("Succeeded", logs.Single(l => l.SwuSet!.Code == "SHD").Status);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WithoutLocalSets_ImportsFullCatalogAndRecordsDiscoveredCoverage()
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http));
        client.AsAdmin();

        for (int run = 0; run < 2; run++)
        {
            var url = "/api/admin/sync/star-wars-unlimited" + (run == 0 ? "" : "?forceFullSync=true");
            var response = await client.PostAsync(url, content: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
            Assert.Equal("Succeeded", payload!.Status);
            Assert.Equal(2, payload.SetCount);
            Assert.Equal(run == 0 ? 4 : 0, payload.Created);
            Assert.Equal(0, payload.Invalid);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2, await db.Cards.CountAsync(c => c.Game == "Star Wars Unlimited"));
            Assert.Equal(2, await db.CardPrintings.CountAsync(p => p.Card.Game == "Star Wars Unlimited"));
            Assert.Equal(["NEW", "SOR"], await db.ImportSyncHistories.Where(h => h.ImporterKey == "swu")
                .OrderBy(h => h.SetCode).Select(h => h.SetCode).ToArrayAsync());
            Assert.Empty(await db.SwuSets.ToListAsync());
            Assert.All(await db.SyncLogs.ToListAsync(), log =>
            {
                Assert.Equal("Succeeded", log.Status);
                Assert.False(log.IsIncremental);
                Assert.Null(log.SwuSetId);
            });
        }

        Assert.Equal([1, 2, 1, 2], handler.Pages);
    }

    [Theory]
    [InlineData(false, "http")]
    [InlineData(false, "incomplete")]
    [InlineData(false, "invalid-record")]
    [InlineData(true, "http")]
    [InlineData(true, "incomplete")]
    [InlineData(true, "invalid-record")]
    public async Task RunStarWarsUnlimited_FullCatalogFailure_DoesNotRecordCoverage(bool forceFullSync, string failure)
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler(failure);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http));
        var checkpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        if (forceFullSync)
        {
            await SeedSwuSetAsync(services, "SOR");
            await SeedSyncHistoryAsync(services, "SOR", checkpoint);
        }

        var response = await client.AsAdmin().PostAsync(
            $"/api/admin/sync/star-wars-unlimited?forceFullSync={forceFullSync}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Failed", payload!.Status);
        Assert.True(payload.Invalid > 0);
        Assert.Equal([1, 2], handler.Pages);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.ImportSyncHistories.Where(h => h.ImporterKey == "swu").ToArrayAsync();
        if (forceFullSync)
            Assert.Equal(checkpoint, Assert.Single(history).LastSyncedAt);
        else
            Assert.Empty(history);
        Assert.Equal("Failed", (await db.SyncLogs.SingleAsync()).Status);
        if (failure != "invalid-record")
            Assert.Empty(await db.CardPrintings.Where(p => p.Card.Game == "Star Wars Unlimited").ToArrayAsync());
    }

    [Fact]
    public async Task RunStarWarsUnlimited_AfterRejectedBootstrap_RetriesFullCatalog()
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler("invalid-record");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http));
        client.AsAdmin();
        var first = await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);
        Assert.Equal("Failed", (await first.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(await db.CardPrintings.Where(p => p.Card.Game == "Star Wars Unlimited").ToListAsync());
            Assert.Empty(await db.ImportSyncHistories.ToListAsync());
        }

        handler.Failure = null;
        var retry = await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var payload = await retry.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Succeeded", payload!.Status);
        Assert.Equal(2, payload.SetCount);
        Assert.Equal([1, 2, 1, 2], handler.Pages);
        using var verifyScope = services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(["NEW", "SOR"], await verifyDb.CardPrintings.Where(p => p.Card.Game == "Star Wars Unlimited")
            .OrderBy(p => p.Set).Select(p => p.Set).ToArrayAsync());
        Assert.Equal(2, await verifyDb.ImportSyncHistories.CountAsync());
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WithFullReconciliation_IgnoresCheckpoint()
    {
        await factory.ResetDatabaseAsync();
        var remoteCards = new RecordingSwuApiClient([]);
        var (client, services, _) = CreateClientWithSwuApiClient(factory, remoteCards);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "SOR", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));

        var response = await client.AsAdmin().PostAsync("/api/admin/sync/star-wars-unlimited?forceFullSync=true", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(remoteCards.CapturedFilter);
        Assert.Null(remoteCards.CapturedFilter.UpdatedSince);
        Assert.Null(remoteCards.CapturedFilter.ExpansionCode);
        Assert.Null(remoteCards.CapturedFilter.ExpansionId);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.SyncLogs.SingleAsync();
        Assert.False(log.IsIncremental);
        Assert.Null(log.UpdatedSince);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenFetchFails_DoesNotAdvanceCheckpointOrExposeException()
    {
        await factory.ResetDatabaseAsync();
        var remoteCards = new RecordingSwuApiClient([], failFetch: true);
        var (client, services, _) = CreateClientWithSwuApiClient(factory, remoteCards);
        await SeedSwuSetAsync(services, "SOR");
        var checkpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        await SeedSyncHistoryAsync(services, "SOR", checkpoint);

        var response = await client.AsAdmin().PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Failed", payload!.Status);
        Assert.Equal(1, payload.Invalid);
        Assert.DoesNotContain(payload.Messages, m => m.Contains("internal diagnostics", StringComparison.Ordinal));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync()).LastSyncedAt);
        Assert.Equal("Failed", (await db.SyncLogs.SingleAsync()).Status);
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

    private static (HttpClient Client, IServiceProvider Services, WebApplicationFactory<Program> Factory) CreateClientWithSwuApiClient(
        CustomWebApplicationFactory factory,
        ISWUApiClient apiClient)
    {
        var customized = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ISWUApiClient));
                services.AddSingleton<ISWUApiClient>(apiClient);
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

    private static async Task SeedSyncHistoryAsync(IServiceProvider services, string setCode, DateTimeOffset syncedAt)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ImportSyncHistories.Add(new ImportSyncHistory
        {
            ImporterKey = "swu",
            SetCode = setCode,
            LastSyncedAt = syncedAt
        });

        await db.SaveChangesAsync();
    }

    private static StrapiRecord CreateRecord(
        int id,
        string title,
        string cardUid,
        string serialCode,
        string setCode,
        string setName,
        string typeName = "Unit",
        string? subtitle = null,
        string? text = null,
        string? artist = null,
        int? cost = null,
        int? power = null,
        int? health = null,
        string? arena = null,
        string[]? aspects = null,
        string[]? traits = null,
        string[]? keywords = null,
        string rarity = "Common",
        string? imageUrl = null,
        string? backImageUrl = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null) =>
        new(
            id,
            new SwuCardAttributes(
                Title: title,
                Subtitle: subtitle,
                CardUid: cardUid,
                SerialCode: serialCode,
                Locale: "en",
                CardNumber: 1,
                Rarity: rarity,
                Text: text,
                Artist: artist,
                Cost: cost,
                Power: power,
                Health: health,
                Arena: arena,
                Aspects: aspects,
                Traits: traits,
                Keywords: keywords,
                CreatedAt: createdAt,
                UpdatedAt: updatedAt,
                PublishedAt: updatedAt,
                Type: new StrapiRelation<SwuTypeAttributes>(
                    new StrapiRelationData<SwuTypeAttributes>(1, new SwuTypeAttributes(typeName, null))),
                Expansion: new StrapiRelation<SwuExpansionAttributes>(
                    new StrapiRelationData<SwuExpansionAttributes>(1, new SwuExpansionAttributes(setName, setCode))),
                VariantTypes: null,
                VariantOf: null,
                ReprintOf: null,
                ArtFront: imageUrl is null
                    ? null
                    : new StrapiRelation<SwuImageAttributes>(
                        new StrapiRelationData<SwuImageAttributes>(
                            1,
                            new SwuImageAttributes(imageUrl, null))),
                ArtBack: backImageUrl is null
                    ? null
                    : new StrapiRelation<SwuImageAttributes>(
                        new StrapiRelationData<SwuImageAttributes>(
                            2,
                            new SwuImageAttributes(backImageUrl, null)))));

    private sealed class SyncTestImporter : ISourceImporter
    {
        private static readonly ConcurrentDictionary<string, ImportSummary> Summaries = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentBag<string> RequestedSetsInternal = [];
        private static TaskCompletionSource<bool>? _started;
        private static TaskCompletionSource<bool>? _release;

        public string Key => "swu";

        public string DisplayName => "SWU Test Importer";

        public IEnumerable<string> SupportedGames => ["Star Wars Unlimited"];

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

    private sealed class FullCatalogHandler(string? failure = null) : HttpMessageHandler
    {
        internal List<int> Pages { get; } = [];
        internal string? Failure { get; set; } = failure;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            Assert.DoesNotContain(query.AllKeys, key => key?.StartsWith("filters[", StringComparison.Ordinal) == true);
            int page = int.Parse(query["pagination[page]"]!);
            Pages.Add(page);
            if (page == 2 && Failure == "http")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
            StrapiRecord[] records = page == 2 && Failure == "incomplete" ? [] :
            [
                CreateRecord(page, page == 2 && Failure == "invalid-record" ? " " : $"Card {page}",
                    $"uid-{page}", $"serial-{page}", page == 1 ? "SOR" : "NEW", $"Expansion {page}")
            ];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = records,
                    meta = new { pagination = new { page, pageSize = 1, pageCount = 2, total = 2 } }
                })
            });
        }
    }

    private sealed class RecordingSwuApiClient(IReadOnlyList<StrapiRecord> records, bool failFetch = false) : ISWUApiClient
    {
        public SWUCardFilter? CapturedFilter { get; private set; }

        public int GetAllCardsCallCount { get; private set; }

        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default)
        {
            GetAllCardsCallCount++;
            CapturedFilter = filter;
            if (failFetch)
            {
                throw new HttpRequestException("internal diagnostics");
            }
            return Task.FromResult(records);
        }

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(code == "SOR" ? 42 : null);
    }
}
