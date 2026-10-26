using api.Data;
using api.Features.Admin.Sync;
using api.Features.Admin.Sync.Dtos;
using api.Importing;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Xunit;

namespace api.Tests.Features.AdminSync;

public sealed class AdminSyncControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    public static IEnumerable<object[]> AuthenticationCases =>
        from authentication in new[] { "anonymous", "non-admin", "invalid-token", "session", "script" }
        from endpoint in new[] { "status", "logs", "", "?forceFullSync=true" }
        select new object[] { authentication, endpoint };

    [Theory]
    [MemberData(nameof(AuthenticationCases))]
    public async Task SyncEndpoints_RequireVerifiedAdministrator(string authentication, string endpoint)
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        using var browser = client;
        browser.BaseAddress = new Uri("https://localhost");

        if (authentication is "session" or "non-admin")
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Username = "sync-login", DisplayName = "Sync", IsAdmin = authentication == "session" };
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "test-password");
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            const string sessionPath = "/api/admin/sync/session";
            var session = await browser.GetFromJsonAsync<AdminSessionResponse>(sessionPath);
            browser.DefaultRequestHeaders.Add("X-Admin-CSRF", session!.CsrfToken);
            var login = await browser.PostAsJsonAsync(sessionPath,
                new { username = "sync-login", password = "test-password" });
            Assert.Equal(authentication == "session" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized,
                login.StatusCode);
            session = await browser.GetFromJsonAsync<AdminSessionResponse>(sessionPath);
            browser.DefaultRequestHeaders.Remove("X-Admin-CSRF");
            browser.DefaultRequestHeaders.Add("X-Admin-CSRF", session!.CsrfToken);
        }

        if (authentication == "script")
        {
            browser.AsAdmin();
        }
        else if (authentication is "session" or "invalid-token")
        {
            browser.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "unrelated-client-token");
        }

        var path = "/api/admin/sync/star-wars-unlimited";
        var response = endpoint is "status" or "logs"
            ? await browser.GetAsync($"{path}/{endpoint}")
            : await browser.PostAsync($"{path}{endpoint}", null);

        var authorized = authentication is "session" or "script";
        Assert.Equal(authorized ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
        if (authorized && endpoint is not ("status" or "logs"))
        {
            var result = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
            Assert.Equal("Succeeded", result!.Status);
            Assert.Single(SyncTestImporter.RequestedSetCodes);
        }
        else
        {
            Assert.Empty(SyncTestImporter.RequestedSetCodes);
        }
    }

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
        await SeedSyncHistoryAsync(services, "", new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero), "swu-catalog");

        client.AsAdmin();
        var response = await client.GetAsync("/api/admin/sync/star-wars-unlimited/status");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swu", payload!.Source);
        Assert.Equal("Idle", payload.Status);
        Assert.Null(payload.RunningSince);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero), payload.LastCompletedAt);
        Assert.Equal(2, payload.HistoryCount);
        Assert.Empty(payload.Messages);
        Assert.Equal(["SHD", "SOR"], payload.History.Select(h => h.SetCode).ToArray());
    }

    [Fact]
    public async Task GetStarWarsUnlimitedStatus_IgnoresUnclassifiedLegacyOutcomes()
    {
        await factory.ResetDatabaseAsync();
        var (client, services, _) = CreateClientWithSyncImporter(factory);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SyncLogs.AddRange(
                new SyncLog
                {
                    StartedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero),
                    CompletedAt = new DateTimeOffset(2026, 7, 21, 12, 1, 0, TimeSpan.Zero),
                    Status = "Succeeded"
                },
                new SyncLog
                {
                    CatalogScope = "GlobalCatalog",
                    StartedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero),
                    CompletedAt = new DateTimeOffset(2026, 7, 20, 12, 1, 0, TimeSpan.Zero),
                    Status = "Failed"
                });
            await db.SaveChangesAsync();
        }

        client.AsAdmin();
        var response = await client.GetFromJsonAsync<AdminSyncStatusDetailsResponse>(
            "/api/admin/sync/star-wars-unlimited/status");

        Assert.Equal("Failed", response!.LastOutcome);
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
        SyncTestImporter.SetSummary("", created: 3, updated: 4, invalid: 0, "Catalog complete");

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
        Assert.Contains("Catalog complete", payload.Messages);

        Assert.Equal([""], SyncTestImporter.RequestedSetCodes.ToArray());

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("GlobalCatalog", (await db.SyncLogs.SingleAsync()).CatalogScope);
        var syncHistory = await db.ImportSyncHistories
            .Where(h => h.ImporterKey == "swu")
            .OrderBy(h => h.SetCode)
            .ToArrayAsync();

        Assert.Equal("SOR", Assert.Single(syncHistory).SetCode);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero), syncHistory[0].LastSyncedAt);
        Assert.True(await db.ImportSyncHistories.AnyAsync(h => h.ImporterKey == "swu-catalog" && h.SetCode == ""));
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
                ImporterKey = "swu-catalog",
                SetCode = "",
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
        Assert.Null(remoteCards.CapturedFilter!.ExpansionId);
        Assert.Null(remoteCards.CapturedFilter.ExpansionCode);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 7, 59, 0, TimeSpan.Zero), remoteCards.CapturedFilter.UpdatedSince);

        using var scope = services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedCard = await appDb.Cards
            .Include(c => c.Printings)
            .SingleAsync(c => c.Game == "Star Wars Unlimited" && c.Name == "Grand Moff Tarkin");
        Assert.Equal("Updated text.", updatedCard.Description);
        Assert.Null(updatedCard.DetailsJson);

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
            .Where(h => h.ImporterKey == "swu-catalog" && h.SetCode == "")
            .ToArrayAsync();
        var historyEntry = Assert.Single(syncHistory);
        Assert.True(historyEntry.LastSyncedAt >= payload.StartedAt);
        Assert.True(historyEntry.LastSyncedAt <= payload.CompletedAt);

        var syncLog = await appDb.SyncLogs
            .Include(log => log.Set)
            .SingleAsync(log => log.SetId == null);
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
        SyncTestImporter.SetSummary("", created: 2, updated: 1, invalid: 0, "Catalog complete");

        client.AsAdmin();
        await client.PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        var logsResponse = await client.GetAsync("/api/admin/sync/star-wars-unlimited/logs");
        logsResponse.EnsureSuccessStatusCode();

        var payload = await logsResponse.Content.ReadFromJsonAsync<AdminSyncLogsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Logs);
        Assert.Equal("Succeeded", payload.Logs[0].Status);
        Assert.Null(payload.Logs[0].SetCode);
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
        await SeedSyncHistoryAsync(services, "", checkpoint, "swu-catalog");
        SyncTestImporter.SetSummary("", created: 1, updated: 0, invalid: 1, "Invalid card");

        var response = await client.AsAdmin().PostAsync("/api/admin/sync/star-wars-unlimited", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Failed", payload!.Status);
        Assert.Equal(1, payload.Invalid);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync(h => h.SetCode == "SOR")).LastSyncedAt);
        Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync(h => h.ImporterKey == "swu-catalog")).LastSyncedAt);
        Assert.False(await db.ImportSyncHistories.AnyAsync(h => h.SetCode == "SHD"));
        var logs = await db.SyncLogs.Include(l => l.Set).ToArrayAsync();
        Assert.Equal("Failed", Assert.Single(logs).Status);
        var status = await client.GetFromJsonAsync<AdminSyncStatusDetailsResponse>("/api/admin/sync/star-wars-unlimited/status");
        Assert.Equal(checkpoint, status!.LastCompletedAt);
        Assert.Equal("Failed", status.LastOutcome);
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
            var card = await db.Cards
                .Include(c => c.CardAspects).ThenInclude(x => x.Aspect)
                .Include(c => c.CardTraits).ThenInclude(x => x.Trait)
                .Include(c => c.CardKeywords).ThenInclude(x => x.Keyword)
                .SingleAsync(c => c.Name == "Card 1");
            Assert.Equal(new[] { "Vigilance" },
                card.CardAspects.Select(x => x.Aspect.Name));
            Assert.Equal(new[] { "Rebel" },
                card.CardTraits.Select(x => x.Trait.Name));
            Assert.Equal(new[] { "Sentinel" },
                card.CardKeywords.Select(x => x.Keyword.Name));
            var printing = await db.CardPrintings.Include(p => p.RarityLookup).SingleAsync(p => p.CardId == card.Id);
            Assert.Equal("Special", printing.Rarity);
            Assert.Equal("Special", printing.RarityLookup!.Name);
            Assert.NotNull(printing.SetId);
            Assert.Equal("", (await db.ImportSyncHistories.SingleAsync(h => h.ImporterKey == "swu-catalog")).SetCode);
            Assert.Equal(["NEW", "SOR"], await db.Sets.OrderBy(s => s.Code).Select(s => s.Code).ToArrayAsync());
            Assert.All(await db.SyncLogs.ToListAsync(), log =>
            {
                Assert.Equal("Succeeded", log.Status);
                Assert.False(log.IsIncremental);
                Assert.Null(log.SetId);
            });
        }

        Assert.Equal([1, 2, 1, 2], handler.Pages);
    }

    [Theory]
    [InlineData(false, "http")]
    [InlineData(false, "incomplete")]
    [InlineData(false, "invalid-record")]
    [InlineData(false, "invalid-relationship")]
    [InlineData(false, "invalid-rarity")]
    [InlineData(true, "http")]
    [InlineData(true, "incomplete")]
    [InlineData(true, "invalid-record")]
    [InlineData(true, "invalid-relationship")]
    [InlineData(true, "invalid-rarity")]
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
            await SeedSyncHistoryAsync(services, "", checkpoint, "swu-catalog");
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
        var history = await db.ImportSyncHistories.Where(h => h.ImporterKey == "swu-catalog").ToArrayAsync();
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
        Assert.Equal(1, await verifyDb.ImportSyncHistories.CountAsync());
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WithLegacyHistory_BootstrapsThenDiscoversNewExpansionIncrementally()
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http));
        var legacyCheckpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "SOR", legacyCheckpoint);
        client.AsAdmin();

        var initialStatus = await client.GetFromJsonAsync<AdminSyncStatusDetailsResponse>("/api/admin/sync/star-wars-unlimited/status");
        Assert.Null(initialStatus!.LastCompletedAt);
        var bootstrap = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal("Succeeded", (await bootstrap.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);
        int[] originalIds;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var checkpoint = await db.ImportSyncHistories.SingleAsync(h => h.ImporterKey == "swu-catalog");
            handler.ExpectedSince = checkpoint.LastSyncedAt.AddMinutes(-1);
            originalIds = await db.CardPrintings.Where(p => p.Card.Game == "Star Wars Unlimited")
                .OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
        }

        handler.IncludeNewExpansion = true;
        for (int run = 0; run < 2; run++)
        {
            var response = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Succeeded", (await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3, await db.Cards.CountAsync(c => c.Game == "Star Wars Unlimited"));
            Assert.Equal(3, await db.CardPrintings.CountAsync(p => p.Card.Game == "Star Wars Unlimited"));
            Assert.Equal(originalIds, await db.CardPrintings.Where(p => p.Card.Game == "Star Wars Unlimited" && p.Set != "NEXT")
                .OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync());
            Assert.Equal("Expansion 3", (await db.Sets.SingleAsync(s => s.Code == "NEXT")).Name);
            Assert.Equal("Corrected expansion", (await db.Sets.SingleAsync(s => s.Code == "SOR")).Name);
            Assert.Equal("Updated text", (await db.Cards.SingleAsync(c => c.Name == "Card 1")).Description);
            Assert.Equal(legacyCheckpoint, (await db.ImportSyncHistories.SingleAsync(h => h.ImporterKey == "swu")).LastSyncedAt);
            var checkpoint = (await db.ImportSyncHistories.SingleAsync(h => h.ImporterKey == "swu-catalog")).LastSyncedAt;
            var status = await client.GetFromJsonAsync<AdminSyncStatusDetailsResponse>("/api/admin/sync/star-wars-unlimited/status");
            Assert.Equal(checkpoint, status!.LastCompletedAt);
            Assert.Equal("Succeeded", status.LastOutcome);
            handler.ExpectedSince = checkpoint.AddMinutes(-1);
        }

        Assert.Equal([1, 2, 1, 2, 1, 2], handler.Pages);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("incomplete")]
    [InlineData("invalid-record")]
    [InlineData("invalid-expansion")]
    [InlineData("invalid-relationship")]
    [InlineData("invalid-rarity")]
    public async Task RunStarWarsUnlimited_IncrementalFailure_RetainsCheckpointAndRetriesIdempotently(string failure)
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http));
        var checkpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        await SeedSyncHistoryAsync(services, "", checkpoint, "swu-catalog");
        handler.ExpectedSince = checkpoint.AddMinutes(-1);
        handler.Failure = failure;
        client.AsAdmin();

        var failed = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Equal("Failed", (await failed.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync()).LastSyncedAt);
        }

        handler.Failure = null;
        var retry = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal("Succeeded", (await retry.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);
        using var verifyScope = services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await verifyDb.Cards.CountAsync(c => c.Game == "Star Wars Unlimited"));
        Assert.Equal(2, await verifyDb.CardPrintings.CountAsync(p => p.Card.Game == "Star Wars Unlimited"));
        Assert.Equal(["Failed", "Succeeded"], (await verifyDb.SyncLogs.OrderBy(l => l.Id).ToListAsync()).Select(l => l.Status));
        Assert.Equal([1, 2, 1, 2], handler.Pages);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenCheckpointPersistenceFails_RetainsCheckpointAndRetriesUpsertedRecords()
    {
        await VerifyPersistenceFailureAsync(failCheckpoint: true);
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WhenRecordPersistenceFails_RetainsCheckpointAndRetriesUpsertedRecords()
    {
        await VerifyPersistenceFailureAsync(failCheckpoint: false);
    }

    private async Task VerifyPersistenceFailureAsync(bool failCheckpoint)
    {
        await factory.ResetDatabaseAsync();
        using var handler = new FullCatalogHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        var interceptor = new FailSyncSaveInterceptor(failCheckpoint);
        var (client, services, _) = CreateClientWithSwuApiClient(factory, new SWUApiClient(http), interceptor);
        var checkpoint = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        await SeedSyncHistoryAsync(services, "", checkpoint, "swu-catalog");
        handler.ExpectedSince = checkpoint.AddMinutes(-1);
        interceptor.Armed = true;
        client.AsAdmin();

        var failed = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        var payload = await failed.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.Equal("Failed", payload!.Status);
        Assert.DoesNotContain(payload.Messages, m => m.Contains("internal persistence diagnostics", StringComparison.Ordinal));
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(checkpoint, (await db.ImportSyncHistories.SingleAsync()).LastSyncedAt);
            Assert.Equal("Failed", (await db.SyncLogs.SingleAsync()).Status);
        }

        var retry = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal("Succeeded", (await retry.Content.ReadFromJsonAsync<AdminSyncStatusResponse>())!.Status);
        using var verifyScope = services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await verifyDb.Cards.CountAsync(c => c.Game == "Star Wars Unlimited"));
        Assert.Equal(2, await verifyDb.CardPrintings.CountAsync(p => p.Card.Game == "Star Wars Unlimited"));
    }

    [Fact]
    public async Task RunStarWarsUnlimited_WithFullReconciliation_IgnoresCheckpoint()
    {
        await factory.ResetDatabaseAsync();
        var remoteCards = new RecordingSwuApiClient([]);
        var (client, services, _) = CreateClientWithSwuApiClient(factory, remoteCards);
        await SeedSwuSetAsync(services, "SOR");
        await SeedSyncHistoryAsync(services, "", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero), "swu-catalog");

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
        await SeedSyncHistoryAsync(services, "", checkpoint, "swu-catalog");

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
        ISWUApiClient apiClient,
        SaveChangesInterceptor? interceptor = null)
    {
        var customized = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ISWUApiClient));
                services.AddSingleton<ISWUApiClient>(apiClient);
                if (interceptor is not null)
                    services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor));
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
            if (await db.Sets.AnyAsync(s => s.GameId == 1 && s.Code == code))
            {
                continue;
            }

            db.Sets.Add(new Set
            {
                GameId = 1,
                Code = code,
                Name = code,
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSyncHistoryAsync(
        IServiceProvider services, string setCode, DateTimeOffset syncedAt, string importerKey = "swu")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ImportSyncHistories.Add(new ImportSyncHistory
        {
            ImporterKey = importerKey,
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

    private sealed class FailSyncSaveInterceptor(bool failCheckpoint) : SaveChangesInterceptor
    {
        internal bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context!.ChangeTracker.Entries();
            if (Armed && entries.Any(e => e.State is EntityState.Added or EntityState.Modified
                && (failCheckpoint ? e.Entity is ImportSyncHistory : e.Entity is CardPrinting)))
            {
                Armed = false;
                throw new DbUpdateException("internal persistence diagnostics");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FullCatalogHandler(string? failure = null) : HttpMessageHandler
    {
        internal List<int> Pages { get; } = [];
        internal string? Failure { get; set; } = failure;
        internal DateTimeOffset? ExpectedSince { get; set; }
        internal bool IncludeNewExpansion { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            Assert.DoesNotContain(query.AllKeys, key => key?.StartsWith("filters[expansion]", StringComparison.Ordinal) == true);
            Assert.Equal(ExpectedSince?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), query["filters[updatedAt][$gt]"]);
            int page = int.Parse(query["pagination[page]"]!);
            Pages.Add(page);
            if (page == 2 && Failure == "http")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
            StrapiRecord[] records = page == 2 && Failure == "incomplete" ? [] :
            [
                CreateRecord(page, page == 2 && Failure == "invalid-record" ? " " : $"Card {page}",
                    $"uid-{page}", $"serial-{page}", page == 1 ? "SOR" : "NEW",
                    Failure == "invalid-expansion" && page == 2 ? "" : IncludeNewExpansion && page == 1 ? "Corrected expansion" : $"Expansion {page}",
                    text: IncludeNewExpansion && page == 1 ? "Updated text" : null)
            ];
            if (IncludeNewExpansion && page == 2)
                records = [.. records, CreateRecord(3, "New expansion card", "uid-3", "serial-3", "NEXT", "Expansion 3")];
            var response = JsonSerializer.SerializeToNode(new
            {
                data = records,
                meta = new { pagination = new { page, pageSize = IncludeNewExpansion ? 2 : 1, pageCount = 2, total = IncludeNewExpansion ? 3 : 2 } }
            })!;
            foreach (var record in response["data"]!.AsArray())
            {
                var attributes = record!["attributes"]!;
                attributes["rarity"] = JsonSerializer.SerializeToNode(new
                {
                    data = new { id = 22, attributes = new { name = "Special" } }
                });
                attributes["aspects"] = JsonSerializer.SerializeToNode(new
                {
                    data = new[] { new { id = 2, attributes = new { name = "Vigilance" } } }
                });
                attributes["traits"] = JsonSerializer.SerializeToNode(new
                {
                    data = new[] { new { id = 37, attributes = new { name = "Rebel" } } }
                });
                attributes["keywords"] = JsonSerializer.SerializeToNode(new
                {
                    data = new[] { new { id = 7, attributes = new { name = "Sentinel" } } }
                });
                if (page == 2 && Failure == "invalid-relationship")
                    attributes["aspects"]!["data"]![0]!["attributes"]!["name"] = 42;
                if (page == 2 && Failure == "invalid-rarity")
                    attributes["rarity"]!["data"]!["attributes"]!["name"] = 42;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
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
