using api.Features.Cards.Dtos;
using api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests;

public class CardFacetsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Games_ReturnSeededFacets()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets/games");
        response.EnsureSuccessStatusCode();

        var games = await response.Content.ReadFromJsonAsync<List<string>>(Options);
        Assert.NotNull(games);
        Assert.NotEmpty(games!);
        Assert.Contains("Magic", games!);
        Assert.Contains("Lorcana", games!);
    }

    [Fact]
    public async Task SetsAndRarities_FilterByGame()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var setsResponse = await client.GetAsync("/api/cards/facets/sets");
        setsResponse.EnsureSuccessStatusCode();
        var allSets = await setsResponse.Content.ReadFromJsonAsync<CardFacetSetsResponse>(Options);
        Assert.NotNull(allSets);
        Assert.Contains("Rise of the Floodborn", allSets!.Sets);
        Assert.Contains("Alpha", allSets.Sets);

        var raritiesResponse = await client.GetAsync("/api/cards/facets/rarities");
        raritiesResponse.EnsureSuccessStatusCode();
        var allRarities = await raritiesResponse.Content.ReadFromJsonAsync<CardFacetRaritiesResponse>(Options);
        Assert.NotNull(allRarities);
        Assert.Contains("Legendary", allRarities!.Rarities);
        Assert.Contains("Common", allRarities.Rarities);

        var filteredSetsResponse = await client.GetAsync("/api/cards/facets/sets?game=Magic");
        filteredSetsResponse.EnsureSuccessStatusCode();
        var magicSets = await filteredSetsResponse.Content.ReadFromJsonAsync<CardFacetSetsResponse>(Options);
        Assert.NotNull(magicSets);
        Assert.Equal("Magic", magicSets!.Game);
        Assert.Contains("Alpha", magicSets.Sets);
        Assert.DoesNotContain("Rise of the Floodborn", magicSets.Sets);

        var filteredRaritiesResponse = await client.GetAsync("/api/cards/facets/rarities?game=Magic");
        filteredRaritiesResponse.EnsureSuccessStatusCode();
        var magicRarities = await filteredRaritiesResponse.Content.ReadFromJsonAsync<CardFacetRaritiesResponse>(Options);
        Assert.NotNull(magicRarities);
        Assert.Equal("Magic", magicRarities!.Game);
        Assert.Contains("Common", magicRarities.Rarities);
        Assert.DoesNotContain("Legendary", magicRarities.Rarities);
    }

    [Fact]
    public async Task GetFacets_ReturnsAllFacetsWithCounts_WhenNoFiltersApplied()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Should have games
        Assert.NotEmpty(facets!.Games);
        Assert.All(facets.Games, f => Assert.True(f.Count > 0));

        // Should have sets
        Assert.NotEmpty(facets.Sets);
        Assert.All(facets.Sets, f => Assert.True(f.Count > 0));

        // Should have rarities
        Assert.NotEmpty(facets.Rarities);
        Assert.All(facets.Rarities, f => Assert.True(f.Count > 0));
    }

    [Fact]
    public async Task GetFacets_FiltersSetsByGame_CaseInsensitive()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?game=magic");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Sets should only contain Magic sets
        Assert.NotEmpty(facets!.Sets);
        Assert.Contains(facets.Sets, f => f.Value == "Alpha");
        Assert.DoesNotContain(facets.Sets, f => f.Value == "Rise of the Floodborn");

        // Rarities should only contain Magic rarities
        Assert.NotEmpty(facets.Rarities);
        Assert.DoesNotContain(facets.Rarities, f => f.Value == "Legendary");
    }

    [Fact]
    public async Task GetFacets_FiltersRaritiesBySet()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?set=Alpha");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Games should contain Magic
        Assert.Contains(facets!.Games, f => f.Value == "Magic");

        // Rarities should only contain rarities from Alpha set
        Assert.NotEmpty(facets.Rarities);
    }

    [Fact]
    public async Task GetFacets_FiltersGamesByRarity()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?rarity=Common");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Games should only contain games with Common rarity
        Assert.NotEmpty(facets!.Games);
        Assert.Contains(facets.Games, f => f.Value == "Magic");
    }

    [Fact]
    public async Task GetFacets_FiltersWithSearchTerm()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?q=black");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Should have facets but only for cards matching search term
        Assert.NotNull(facets!.Games);
        Assert.NotNull(facets.Sets);
        Assert.NotNull(facets.Rarities);
    }

    [Fact]
    public async Task GetFacets_SearchesPrintingSetAndNumber()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var setResponse = await client.GetFromJsonAsync<CardsFacetResponse>("/api/cards/facets?q=Beta&printingSearch=true", Options);
        var numberResponse = await client.GetFromJsonAsync<CardsFacetResponse>("/api/cards/facets?q=B2&printingSearch=true", Options);

        Assert.NotNull(setResponse);
        Assert.Contains(setResponse.Sets, facet => facet.Value == "Beta");
        Assert.NotNull(numberResponse);
        Assert.Contains(numberResponse.Sets, facet => facet.Value == "Beta");
    }

    [Fact]
    public async Task GetFacets_SearchesCardTypeInBothSearchModes()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var cardSearchResponse = await client.GetAsync("/api/cards/facets?q=Creature");
        cardSearchResponse.EnsureSuccessStatusCode();
        var cardSearchFacets = await cardSearchResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(cardSearchFacets);
        Assert.Contains(cardSearchFacets!.Games, facet => facet.Value == "Magic");

        var printingSearchResponse = await client.GetAsync("/api/cards/facets?q=Creature&printingSearch=true");
        printingSearchResponse.EnsureSuccessStatusCode();
        var printingSearchFacets = await printingSearchResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(printingSearchFacets);
        Assert.Contains(printingSearchFacets!.Games, facet => facet.Value == "Magic");
    }

    [Fact]
    public async Task GetFacets_CombinesMultipleFilters()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?game=Magic&rarity=Common");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);

        // Sets should only contain Magic sets with Common rarity cards
        Assert.NotEmpty(facets!.Sets);

        // Games should contain Magic
        Assert.Contains(facets.Games, f => f.Value == "Magic");
    }

    [Fact]
    public async Task GetFacets_ReturnsSwccgSpecificFacets()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swccg = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var subType = new api.Models.SubType { Game = swccg, Name = "Weapon" };
            db.SubTypes.Add(subType);

            var card = new api.Models.Card
            {
                Game = swccg,
                Name = "Vader's Lightsaber",
                Side = "Dark",
                Destiny = "1",
                Deploy = "2",
                Forfeit = "3",
                Power = 4,
                Ability = 5,
                LightSideIcons = 0,
                DarkSideIcons = 2,
            };
            card.CardSubTypes.Add(new api.Models.CardSubType { Card = card, SubType = subType });
            db.Cards.Add(card);

            var printing = new api.Models.CardPrinting
            {
                Card = card,
                CatalogSet = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Premiere"),
                RarityLookup = api.Importing.TreatmentLookup.UpsertRarity(db, swccg, "Rare"),
                Number = "10",
            };
            db.CardPrintings.Add(printing);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync("/api/cards/facets?game=Star+Wars+CCG");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        Assert.Contains(facets!.SubTypes, f => f.Value == "Weapon");
        Assert.Contains(facets.Destinies, f => f.Value == "1");
        Assert.Contains(facets.Deploys, f => f.Value == "2");
        Assert.Contains(facets.Forfeits, f => f.Value == "3");
        Assert.Contains(facets.Powers, f => f.Value == "4");
        Assert.Contains(facets.Abilities, f => f.Value == "5");
        Assert.Contains(facets.DarkSideIcons, f => f.Value == "2");
    }

    [Fact]
    public async Task GetFacets_StatFacetsRespectOtherFiltersAndExcludeTheirOwnSelection()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swccg = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var magic = api.Importing.TreatmentLookup.GetOrAddGame(db, "Magic");
            var premiere = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var otherSet = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Other");
            var magicSet = api.Importing.TreatmentLookup.UpsertSet(db, magic, "Premiere");

            var cards = new[]
            {
                (Game: swccg, Name: "Dark One", Side: "Dark", Destiny: "1", Set: premiere),
                (Game: swccg, Name: "Dark Two", Side: "Dark", Destiny: "2", Set: premiere),
                (Game: swccg, Name: "Dark Other Set", Side: "Dark", Destiny: "4", Set: otherSet),
                (Game: swccg, Name: "Light One", Side: "Light", Destiny: "3", Set: premiere),
                (Game: magic, Name: "Magic One", Side: "Dark", Destiny: "5", Set: magicSet),
            };

            foreach (var item in cards)
            {
                var card = new api.Models.Card
                {
                    Game = item.Game,
                    Name = item.Name,
                    Side = item.Side,
                    Destiny = item.Destiny,
                };
                db.CardPrintings.Add(new api.Models.CardPrinting
                {
                    Card = card,
                    CatalogSet = item.Set,
                    Number = item.Name,
                });
            }

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync(
            "/api/cards/facets?game=Star+Wars+CCG&set=Premiere&side=Dark&destiny=1");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        Assert.Equal(["1", "2"], facets!.Destinies.Select(facet => facet.Value).Order().ToArray());
    }

    [Fact]
    public async Task GetFacets_PersonaFilter_DoesNotMatchPartialSubstring()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swccg = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var premiere = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var ds2 = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Death Star II");
            var rarity = api.Importing.TreatmentLookup.UpsertRarity(db, swccg, "Rare");

            var hanSolo = new api.Models.Card
            {
                Game = swccg,
                Name = "Han Solo",
                Persona = "Han",
            };
            db.Cards.Add(hanSolo);
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = hanSolo,
                CatalogSet = premiere,
                RarityLookup = rarity,
                Number = "101",
            });

            var invisibleHand = new api.Models.Card
            {
                Game = swccg,
                Name = "Invisible Hand: Bridge",
                Persona = "Invisible Hand",
            };
            db.Cards.Add(invisibleHand);
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = invisibleHand,
                CatalogSet = ds2,
                RarityLookup = rarity,
                Number = "102",
            });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        try
        {
            var response = await client.GetAsync("/api/cards/facets?game=Star+Wars+CCG&persona=Han");
            response.EnsureSuccessStatusCode();

            var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(facets);
            // Sets facet should only contain sets with Han (Premiere), not Invisible Hand (Death Star II)
            Assert.Contains(facets!.Sets, s => s.Value == "Premiere");
            Assert.DoesNotContain(facets.Sets, s => s.Value == "Death Star II");
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task GetFacets_SWCCG_RarityFacets_UsesRarityType()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swccg = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var set = api.Importing.TreatmentLookup.UpsertSet(db, swccg, "Premiere");

            var rare1 = new api.Models.Rarity { Game = swccg, Name = "Rare 1", RarityType = "Rare" };
            var rare2 = new api.Models.Rarity { Game = swccg, Name = "Rare 2", RarityType = "Rare" };
            var common1 = new api.Models.Rarity { Game = swccg, Name = "Common 1", RarityType = "Common" };
            db.Rarities.AddRange(rare1, rare2, common1);

            var c1 = new api.Models.Card { Game = swccg, Name = "Card 1" };
            var c2 = new api.Models.Card { Game = swccg, Name = "Card 2" };
            var c3 = new api.Models.Card { Game = swccg, Name = "Card 3" };
            db.Cards.AddRange(c1, c2, c3);

            db.CardPrintings.Add(new api.Models.CardPrinting { Card = c1, CatalogSet = set, RarityLookup = rare1, Number = "1" });
            db.CardPrintings.Add(new api.Models.CardPrinting { Card = c2, CatalogSet = set, RarityLookup = rare2, Number = "2" });
            db.CardPrintings.Add(new api.Models.CardPrinting { Card = c3, CatalogSet = set, RarityLookup = common1, Number = "3" });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        try
        {
            var response = await client.GetAsync("/api/cards/facets?game=Star+Wars+CCG");
            response.EnsureSuccessStatusCode();

            var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(facets);

            // Rarities should contain "Rare" (count 2) and "Common" (count 1), not "Rare 1" or "Rare 2"
            Assert.Contains(facets!.Rarities, r => r.Value == "Rare" && r.Count == 2);
            Assert.Contains(facets.Rarities, r => r.Value == "Common" && r.Count == 1);
            Assert.DoesNotContain(facets.Rarities, r => r.Value == "Rare 1");
            Assert.DoesNotContain(facets.Rarities, r => r.Value == "Rare 2");
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task GetFacets_DisplaysSetNameInsteadOfSetCode_ForBothSwuAndSwccg()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swu = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars Unlimited");
            var swccg = api.Importing.TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");

            var swuSet = new api.Models.Set { Game = swu, Code = "SOR", Name = "Spark of Rebellion" };
            var swccgSet = new api.Models.Set { Game = swccg, Code = "PRE", Name = "Premiere" };
            db.Sets.AddRange(swuSet, swccgSet);

            var swuCard = new api.Models.Card { Game = swu, Name = "Luke Skywalker (SWU)" };
            var swccgCard = new api.Models.Card { Game = swccg, Name = "Luke Skywalker (SWCCG)" };
            db.Cards.AddRange(swuCard, swccgCard);

            db.CardPrintings.Add(new api.Models.CardPrinting { Card = swuCard, CatalogSet = swuSet, Number = "1" });
            db.CardPrintings.Add(new api.Models.CardPrinting { Card = swccgCard, CatalogSet = swccgSet, Number = "2" });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        try
        {
            // SWU facets
            var swuResponse = await client.GetAsync("/api/cards/facets?game=Star+Wars+Unlimited");
            swuResponse.EnsureSuccessStatusCode();
            var swuFacets = await swuResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(swuFacets);
            Assert.Contains(swuFacets!.Sets, s =>
                s.Value == "Spark of Rebellion" && s.Label == "Spark of Rebellion");
            Assert.DoesNotContain(swuFacets.Sets, s => s.Value == "SOR");

            // SWCCG facets
            var swccgResponse = await client.GetAsync("/api/cards/facets?game=Star+Wars+CCG");
            swccgResponse.EnsureSuccessStatusCode();
            var swccgFacets = await swccgResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(swccgFacets);
            Assert.Contains(swccgFacets!.Sets, s => s.Value == "Premiere" && s.Label == "Premiere");
            Assert.DoesNotContain(swccgFacets.Sets, s => s.Value == "PRE");
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task GetFacets_PokemonStagesAndHpFilters_ReturnExpectedResults()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var pokemon = api.Importing.TreatmentLookup.GetOrAddGame(db, "Pokemon TCG");

            var set = new api.Models.Set { Game = pokemon, Code = "ME03", Name = "Perfect Order" };
            db.Sets.Add(set);

            var card1 = new api.Models.Card { Game = pokemon, Name = "Pikachu", Stage = "Basic", Hp = 60 };
            var card2 = new api.Models.Card { Game = pokemon, Name = "Raichu", Stage = "Stage 1", Hp = 120 };
            var card3 = new api.Models.Card { Game = pokemon, Name = "Aegislash", Stage = "Stage 2", Hp = 150 };
            db.Cards.AddRange(card1, card2, card3);

            db.CardPrintings.Add(new api.Models.CardPrinting { Card = card1, CatalogSet = set, Number = "001" });
            db.CardPrintings.Add(new api.Models.CardPrinting { Card = card2, CatalogSet = set, Number = "002" });
            db.CardPrintings.Add(new api.Models.CardPrinting { Card = card3, CatalogSet = set, Number = "003" });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        try
        {
            // All Pokemon facets
            var response = await client.GetAsync("/api/cards/facets?game=Pokemon+TCG");
            response.EnsureSuccessStatusCode();
            var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(facets);
            Assert.Contains(facets!.Stages, s => s.Value == "Basic" && s.Count == 1);
            Assert.Contains(facets.Stages, s => s.Value == "Stage 1" && s.Count == 1);
            Assert.Contains(facets.Stages, s => s.Value == "Stage 2" && s.Count == 1);

            // Filter by HP > 100
            var hpResponse = await client.GetAsync("/api/cards/facets?game=Pokemon+TCG&hp=>100");
            hpResponse.EnsureSuccessStatusCode();
            var hpFacets = await hpResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(hpFacets);
            Assert.DoesNotContain(hpFacets!.Stages, s => s.Value == "Basic");
            Assert.Contains(hpFacets.Stages, s => s.Value == "Stage 1");
            Assert.Contains(hpFacets.Stages, s => s.Value == "Stage 2");

            // Filter by stage = Stage 2
            var stageResponse = await client.GetAsync("/api/cards/facets?game=Pokemon+TCG&stage=Stage+2");
            stageResponse.EnsureSuccessStatusCode();
            var stageFacets = await stageResponse.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
            Assert.NotNull(stageFacets);
            // In stat facets, the stage facet excludes its own filter, so all stages are returned
            Assert.Contains(stageFacets!.Stages, s => s.Value == "Stage 2");
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }
}
