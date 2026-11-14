using System.Net;
using System.Net.Http.Json;
using System.Text;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Importing.Mtg;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests;

public class MtgScryfallImporterTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ScryfallApiClient_SendsRequiredHeaders_AndRetriesOn429()
    {
        var attempts = 0;
        var seenUserAgent = string.Empty;
        var seenAccept = string.Empty;

        var handler = new StubHttpMessageHandler(req =>
        {
            attempts++;
            seenUserAgent = string.Join(" ", req.Headers.UserAgent.Select(u => u.ToString()));
            seenAccept = string.Join(",", req.Headers.Accept.Select(a => a.MediaType));

            if (attempts == 1)
            {
                var retryResp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                retryResp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                return retryResp;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"object":"list","has_more":false,"data":[{"id":"s1","code":"blb","name":"Bloomburrow","set_type":"expansion","released_at":"2024-08-02","card_count":281,"digital":false,"foil_only":false,"nonfoil_only":false,"icon_svg_uri":"https://svgs.scryfall.io/sets/blb.svg"}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com/") };
        var client = new ScryfallApiClient(http, NullLogger<ScryfallApiClient>.Instance);

        var sets = await client.GetSetsAsync();

        Assert.Equal(2, attempts);
        Assert.Contains("TCGTracker/1.0", seenUserAgent);
        Assert.Contains("application/json", seenAccept);
        Assert.Single(sets);
        Assert.Equal("blb", sets[0].Code);
    }

    [Fact]
    public async Task ScryfallApiClient_RejectsNonScryfallPaginationHost()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"object":"list","has_more":true,"next_page":"https://evil.example.com/steal","data":[]}""",
                Encoding.UTF8,
                "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com/") };
        var client = new ScryfallApiClient(http, NullLogger<ScryfallApiClient>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSetCardsAsync("blb"));
    }

    [Fact]
    public async Task SyncSetsAsync_ExcludesDigitalSets_AndPersistsMetadata()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stubApi = new FakeMtgApiClient(
            sets:
            [
                new ScryfallSet("1", "blb", "Bloomburrow", "expansion", null, null, "2024-08-02", 281, false, false, false, "https://svgs.scryfall.io/sets/blb.svg", null),
                new ScryfallSet("2", "yblb", "Alchemy: Bloomburrow", "alchemy", null, null, "2024-08-20", 30, true, false, false, "https://svgs.scryfall.io/sets/yblb.svg", null)
            ],
            cardsBySet: new());

        var importer = new MtgScryfallImporter(db, stubApi, NullLogger<MtgScryfallImporter>.Instance);
        var summary = await importer.SyncSetsAsync();

        var sets = await db.Sets.Include(s => s.Game).Where(s => s.Game.Name == MtgCatalog.GameName).ToListAsync();
        Assert.Single(sets);
        Assert.Equal("BLB", sets[0].Code);
        Assert.Equal("Core & Expansions", sets[0].Era);
        Assert.Equal("https://svgs.scryfall.io/sets/blb.svg", sets[0].LogoUrl);
        Assert.Equal(new DateOnly(2024, 8, 2), sets[0].ReleasedAt);
        Assert.Contains("1 MTG sets", summary.Messages.Single());
    }

    [Fact]
    public async Task SyncCardsAsync_DecouplesCardAndFinishes_SupportsDfc_AndIsIdempotent()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sampleCards = new List<ScryfallCard>
        {
            new(
                Id: "11111111-1111-1111-1111-111111111111",
                OracleId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Lang: "en",
                Name: "Llanowar Elves",
                PrintedName: null,
                Layout: "normal",
                ManaCost: "{G}",
                Cmc: 1,
                TypeLine: "Creature — Elf Druid",
                PrintedTypeLine: null,
                OracleText: "{T}: Add {G}.",
                PrintedText: null,
                FlavorText: null,
                Power: "1",
                Toughness: "1",
                Loyalty: null,
                Defense: null,
                Colors: ["G"],
                ColorIdentity: ["G"],
                Keywords: [],
                Rarity: "common",
                CollectorNumber: "184",
                Set: "fdn",
                SetName: "Foundations",
                Finishes: ["nonfoil", "foil"],
                Frame: "2015",
                FrameEffects: null,
                BorderColor: "black",
                FullArt: false,
                Textless: false,
                Promo: false,
                PromoTypes: null,
                Variation: false,
                Artist: "Kieran Yanner",
                ReleasedAt: "2024-11-15",
                ImageUris: new ScryfallImageUris(null, "https://cards.scryfall.io/normal/front/elves.jpg", null, null, null, null),
                CardFaces: null),
            new(
                Id: "22222222-2222-2222-2222-222222222222",
                OracleId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                Lang: "en",
                Name: "Delver of Secrets // Insectile Aberration",
                PrintedName: null,
                Layout: "transform",
                ManaCost: null,
                Cmc: 1,
                TypeLine: "Creature — Human Wizard // Creature — Human Insect",
                PrintedTypeLine: null,
                OracleText: null,
                PrintedText: null,
                FlavorText: null,
                Power: null,
                Toughness: null,
                Loyalty: null,
                Defense: null,
                Colors: null,
                ColorIdentity: ["U"],
                Keywords: ["Flying", "Transform"],
                Rarity: "common",
                CollectorNumber: "51",
                Set: "fdn",
                SetName: "Foundations",
                Finishes: ["nonfoil"],
                Frame: "2015",
                FrameEffects: null,
                BorderColor: "black",
                FullArt: false,
                Textless: false,
                Promo: false,
                PromoTypes: null,
                Variation: false,
                Artist: "Matt Stewart",
                ReleasedAt: "2024-11-15",
                ImageUris: null,
                CardFaces:
                [
                    new ScryfallCardFace("Delver of Secrets", null, null, "{U}", "Creature — Human Wizard", null, "Look at the top card...", null, null, "1", "1", null, null, ["U"], null, new ScryfallImageUris(null, "https://cards.scryfall.io/normal/front/delver.jpg", null, null, null, null)),
                    new ScryfallCardFace("Insectile Aberration", null, null, "", "Creature — Human Insect", null, "Flying", null, null, "3", "2", null, null, ["U"], null, new ScryfallImageUris(null, "https://cards.scryfall.io/normal/back/delver.jpg", null, null, null, null))
                ])
        };

        var stubApi = new FakeMtgApiClient(
            sets: [new ScryfallSet("1", "fdn", "Foundations", "core", null, null, "2024-11-15", 300, false, false, false, "https://svgs.scryfall.io/sets/fdn.svg", null)],
            cardsBySet: new Dictionary<string, IReadOnlyList<ScryfallCard>>(StringComparer.OrdinalIgnoreCase) { ["FDN"] = sampleCards });

        var importer = new MtgScryfallImporter(db, stubApi, NullLogger<MtgScryfallImporter>.Instance);
        await importer.SyncSetsAsync();

        var firstRun = await importer.SyncCardsAsync(["FDN"]);
        Assert.Equal(2, firstRun.CardsCreated);
        Assert.Equal(3, firstRun.PrintingsCreated);
        Assert.Equal(0, firstRun.Errors);

        var secondRun = await importer.SyncCardsAsync(["FDN"]);
        Assert.Equal(0, secondRun.CardsCreated);
        Assert.Equal(0, secondRun.PrintingsCreated);

        var cards = await db.Cards.Include(c => c.Printings).Where(c => c.Game.Name == MtgCatalog.GameName).ToListAsync();
        Assert.Equal(2, cards.Count);
        var dfc = cards.Single(c => c.CardUid == "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Assert.Equal("Insectile Aberration", dfc.BackTitle);
        Assert.Equal("Flying", dfc.BackText);
        Assert.Equal("https://cards.scryfall.io/normal/back/delver.jpg", dfc.Printings.Single().BackImageUrl);
    }

    [Fact]
    public async Task SyncCardsAsync_DryRun_DoesNotPersistChanges()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stubApi = new FakeMtgApiClient(
            sets: [new ScryfallSet("1", "fdn", "Foundations", "core", null, null, "2024-11-15", 1, false, false, false, null, null)],
            cardsBySet: new Dictionary<string, IReadOnlyList<ScryfallCard>>(StringComparer.OrdinalIgnoreCase)
            {
                ["FDN"] =
                [
                    new ScryfallCard("111", "aaa", "en", "Opt", null, "normal", "{U}", 1, "Instant", null, "Scry 1. Draw a card.", null, null, null, null, null, null, ["U"], ["U"], ["Scry"], "common", "1", "fdn", "Foundations", ["nonfoil"], "2015", null, "black", false, false, false, null, false, "Artist", "2024-11-15", null, null)
                ]
            });

        var importer = new MtgScryfallImporter(db, stubApi, NullLogger<MtgScryfallImporter>.Instance);
        await importer.SyncSetsAsync();

        var drySummary = await importer.SyncCardsAsync(["FDN"], dryRun: true);
        Assert.True(drySummary.DryRun);
        Assert.Equal(1, drySummary.CardsCreated);

        Assert.Equal(0, await db.Cards.CountAsync(c => c.Game.Name == MtgCatalog.GameName));
    }

    [Fact]
    public async Task AdminMtgSyncEndpoints_ReturnStatusAndValidateRequests()
    {
        await factory.ResetDatabaseAsync();
        var fakeApi = new FakeMtgApiClient(
            sets: [new ScryfallSet("1", "fdn", "Foundations", "core", null, null, "2024-11-15", 1, false, false, false, "https://svgs.scryfall.io/sets/fdn.svg", null)],
            cardsBySet: new Dictionary<string, IReadOnlyList<ScryfallCard>>(StringComparer.OrdinalIgnoreCase));

        using var customFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IMtgApiClient>();
                services.AddSingleton<IMtgApiClient>(fakeApi);
            }));

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var setsResp = await client.PostAsync("/api/admin/sync/mtg/sets", null);
        Assert.Equal(HttpStatusCode.OK, setsResp.StatusCode);

        var statusResp = await client.GetFromJsonAsync<AdminMtgSyncStatusResponse>("/api/admin/sync/mtg/status");
        Assert.NotNull(statusResp);
        var fdn = Assert.Single(statusResp.Sets, s => s.Code == "FDN");
        Assert.False(fdn.IsSynced);
        Assert.Equal(0, fdn.CardCountInDb);

        var badCardsResp = await client.PostAsJsonAsync("/api/admin/sync/mtg/cards", new AdminMtgCardsSyncRequest([]));
        Assert.Equal(HttpStatusCode.BadRequest, badCardsResp.StatusCode);

        var cardsResp = await client.PostAsJsonAsync("/api/admin/sync/mtg/cards", new AdminMtgCardsSyncRequest(["FDN"]));
        Assert.Equal(HttpStatusCode.OK, cardsResp.StatusCode);

        var logsResp = await client.GetAsync("/api/admin/sync/mtg/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
    }

    private sealed class FakeMtgApiClient(
        IReadOnlyList<ScryfallSet> sets,
        Dictionary<string, IReadOnlyList<ScryfallCard>> cardsBySet) : IMtgApiClient
    {
        public Task<IReadOnlyList<ScryfallSet>> GetSetsAsync(CancellationToken ct = default)
            => Task.FromResult(sets);

        public Task<IReadOnlyList<ScryfallCard>> GetSetCardsAsync(string setCode, CancellationToken ct = default)
            => Task.FromResult(cardsBySet.TryGetValue(setCode, out var list) ? list : Array.Empty<ScryfallCard>());
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
