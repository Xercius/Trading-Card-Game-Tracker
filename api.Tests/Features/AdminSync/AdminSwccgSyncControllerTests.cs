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
            sets: [new SwccgSet("1", "Premiere")],
            cards:
            [
                new SwccgCardRecord(
                    Id: 501,
                    GempId: "1_501",
                    Side: "Dark",
                    Rarity: "R1",
                    Set: "Premiere",
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
}
