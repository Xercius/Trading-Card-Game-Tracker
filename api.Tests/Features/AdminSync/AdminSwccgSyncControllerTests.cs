using System.Net;
using System.Net.Http.Json;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Importing;
using api.Importing.Swccg;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Features.AdminSync;

public sealed class AdminSwccgSyncControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static (HttpClient Client, IServiceProvider Services) CreateClientWithSwccgImporter(
        CustomWebApplicationFactory factory,
        ISwccgApiClient? swccgClient = null)
    {
        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISwccgApiClient>();
                services.AddSingleton<ISwccgApiClient>(swccgClient ?? new RecordingSwccgApiClient());

                services.RemoveAll<ISourceImporter>();
                services.AddScoped<ISourceImporter>(sp => new SwccgJsonImporter(
                    sp.GetRequiredService<AppDbContext>(),
                    sp.GetRequiredService<ISwccgApiClient>(),
                    NullLogger<SwccgJsonImporter>.Instance,
                    sp.GetRequiredService<TimeProvider>()));
            });
        });

        var client = customizedFactory.CreateClient();
        return (client, customizedFactory.Services);
    }

    [Fact]
    public async Task GetStarWarsCcgStatus_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var (client, _) = CreateClientWithSwccgImporter(factory);

        var response = await client.GetAsync("/api/admin/sync/star-wars-ccg/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStarWarsCcgStatus_WhenAuthenticated_ReturnsIdleStatus_And_Messages()
    {
        await factory.ResetDatabaseAsync();
        var (client, _) = CreateClientWithSwccgImporter(factory);

        client.AsAdmin();
        var response = await client.GetAsync("/api/admin/sync/star-wars-ccg/status");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swccg", payload!.Source);
        Assert.Equal("Idle", payload.Status);
        Assert.Null(payload.RunningSince);
        Assert.Contains("No successful Star Wars CCG sync recorded yet.", payload.Messages.First());
    }

    [Fact]
    public async Task RunStarWarsCcg_ExecutesSync_And_PersistsHistoryAndLogs()
    {
        await factory.ResetDatabaseAsync();
        var apiClient = new RecordingSwccgApiClient(
            sets: [new SwccgSet("1", "Premiere", "P", "1995-12-01", "Premiere")],
            cards:
            [
                new SwccgCardRecord(
                    Id: 501,
                    GempId: "1_501",
                    Side: "Dark",
                    Rarity: "R1",
                    Set: "1",
                    Printings: null,
                    Abbr: null,
                    Combo: null,
                    Matching: null,
                    MatchingWeapon: null,
                    Pulls: null,
                    PulledBy: null,
                    UnderlyingCardFor: null,
                    Personas: null,
                    Rulings: ["Pilot ruling."],
                    Misc: null,
                    Counterpart: null,
                    Legacy: false,
                    Front: new SwccgFaceRecord(
                        Title: "Darth Vader",
                        Type: "Character",
                        SubType: "Imperial",
                        Uniqueness: "*",
                        Destiny: "1",
                        Deploy: "6",
                        Forfeit: "8",
                        Power: "6",
                        Ability: "6",
                        Armor: "5",
                        Maneuver: null,
                        Hyperspeed: null,
                        Landspeed: null,
                        Parsec: null,
                        Ferocity: null,
                        Politics: null,
                        Gametext: "Vader gametext.",
                        Lore: "Vader lore.",
                        ImageUrl: "https://res.starwarsccg.org/vader.gif",
                        Icons: ["Pilot", "Warrior"],
                        Characteristics: ["Leader"],
                        ExtraText: ["Dark Jedi"]
                    ),
                    Back: null
                )
            ]
        );

        var (client, services) = CreateClientWithSwccgImporter(factory, apiClient);

        client.AsAdmin();
        var response = await client.PostAsync("/api/admin/sync/star-wars-ccg", null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("swccg", payload!.Source);
        Assert.Equal("Succeeded", payload.Status);
        Assert.Equal(2, payload.Created); // 1 card + 1 printing

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var history = await db.ImportSyncHistories.FirstOrDefaultAsync(h => h.ImporterKey == "swccg-catalog");
        Assert.NotNull(history);

        var syncedSet = await db.Sets.FirstOrDefaultAsync(s => s.Name == "Premiere");
        Assert.NotNull(syncedSet);
        Assert.Equal("P", syncedSet!.Code);
        Assert.Equal(new DateOnly(1995, 12, 1), syncedSet.ReleasedAt);

        var syncedRarity = await db.Rarities.FirstOrDefaultAsync(r => r.Character == "R1");
        Assert.NotNull(syncedRarity);
        Assert.Equal("Rare 1", syncedRarity!.Name);
        Assert.Equal("Rare", syncedRarity.RarityType);

        var log = await db.SyncLogs.FirstOrDefaultAsync(l => l.CatalogScope == "SwccgCatalog");
        Assert.NotNull(log);
        Assert.Equal("Succeeded", log!.Status);

        // Verify logs endpoint
        var logsResponse = await client.GetAsync("/api/admin/sync/star-wars-ccg/logs");
        logsResponse.EnsureSuccessStatusCode();
        var logsPayload = await logsResponse.Content.ReadFromJsonAsync<AdminSyncLogsResponse>();
        Assert.NotNull(logsPayload);
        Assert.Equal("swccg", logsPayload!.Source);
        Assert.Equal(1, logsPayload.TotalCount);
    }

    [Fact]
    public async Task RunStarWarsCcg_DryRunHonorsLimit_AndDoesNotPersistCatalogOrCheckpoint()
    {
        await factory.ResetDatabaseAsync();
        var apiClient = new RecordingSwccgApiClient(
            cards: [CreateCard(501, "Darth Vader"), CreateCard(502, "Luke Skywalker")]);
        var (client, services) = CreateClientWithSwccgImporter(factory, apiClient);
        client.AsAdmin();

        var response = await client.PostAsJsonAsync(
            "/api/admin/sync/star-wars-ccg",
            new { dryRun = true, limit = 1 });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Succeeded", payload!.Status);
        Assert.Equal(2, payload.Created);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Cards.Where(c => c.Game!.Name == "Star Wars CCG").ToListAsync());
        Assert.Empty(await db.CardPrintings.Where(p => p.Card.Game!.Name == "Star Wars CCG").ToListAsync());
        Assert.Null(await db.ImportSyncHistories.SingleOrDefaultAsync(h => h.ImporterKey == "swccg-catalog"));
    }

    [Fact]
    public async Task RunStarWarsCcg_WhenLimitIsInvalid_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var (client, _) = CreateClientWithSwccgImporter(factory);
        client.AsAdmin();

        var response = await client.PostAsJsonAsync(
            "/api/admin/sync/star-wars-ccg",
            new { dryRun = false, limit = 0 });

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RunStarWarsCcg_WhenImporterThrows_DoesNotExposeExceptionDetails()
    {
        await factory.ResetDatabaseAsync();
        var (client, services) = CreateClientWithSwccgImporter(
            factory,
            new ThrowingSwccgApiClient("internal database detail"));
        client.AsAdmin();

        var response = await client.PostAsync("/api/admin/sync/star-wars-ccg", null);

        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internal database detail", responseBody);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncLog = await db.SyncLogs.SingleAsync(l => l.CatalogScope == "SwccgCatalog");
        Assert.Equal("Import failed. See server logs for details.", syncLog.ErrorMessage);
    }

    [Fact]
    public async Task RunStarWarsCcg_WhenCardRecordFails_UsesGenericSourceIdMessage()
    {
        await factory.ResetDatabaseAsync();
        var apiClient = new RecordingSwccgApiClient(cards: [CreateCard(99, null)]);
        var (client, _) = CreateClientWithSwccgImporter(factory, apiClient);
        client.AsAdmin();

        var response = await client.PostAsync("/api/admin/sync/star-wars-ccg", null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminSyncStatusResponse>();
        Assert.NotNull(payload);
        Assert.Contains("Failed to import SWCCG record [99].", payload!.Messages);
        Assert.DoesNotContain(payload.Messages, message => message.Contains("missing face title"));
    }

    private static SwccgCardRecord CreateCard(int id, string? title)
    {
        return new SwccgCardRecord(
            Id: id,
            GempId: $"1_{id}",
            Side: "Dark",
            Rarity: null,
            Set: "1",
            Printings: null,
            Abbr: null,
            Combo: null,
            Matching: null,
            MatchingWeapon: null,
            Pulls: null,
            PulledBy: null,
            UnderlyingCardFor: null,
            Personas: null,
            Rulings: null,
            Misc: null,
            Counterpart: null,
            Legacy: false,
            Front: title is null
                ? null
                : new SwccgFaceRecord(
                    Title: title,
                    Type: "Character",
                    SubType: null,
                    Uniqueness: null,
                    Destiny: null,
                    Deploy: null,
                    Forfeit: null,
                    Power: null,
                    Ability: null,
                    Armor: null,
                    Maneuver: null,
                    Hyperspeed: null,
                    Landspeed: null,
                    Parsec: null,
                    Ferocity: null,
                    Politics: null,
                    Gametext: null,
                    Lore: null,
                    ImageUrl: null,
                    Icons: null,
                    Characteristics: null,
                    ExtraText: null),
            Back: null);
    }

    private sealed class RecordingSwccgApiClient(
        List<SwccgSet>? sets = null,
        List<SwccgCardRecord>? cards = null) : ISwccgApiClient
    {
        private readonly List<SwccgSet> _sets = sets ?? [];
        private readonly List<SwccgCardRecord> _cards = cards ?? [];

        public Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
            => Task.FromResult(_sets);

        public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
            => Task.FromResult(_cards.Where(c => c.Side == "Dark").ToList());

        public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
            => Task.FromResult(_cards.Where(c => c.Side == "Light").ToList());

        public Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
            => Task.FromResult(_cards);
    }

    private sealed class ThrowingSwccgApiClient(string message) : ISwccgApiClient
    {
        public Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(message);

        public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(message);

        public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(message);

        public Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(message);
    }
}
