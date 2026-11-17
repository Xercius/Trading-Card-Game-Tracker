using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class GameCardFilterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string MtgGame = "Magic The Gathering";
    private const string SwuGame = "Star Wars Unlimited";

    private async Task SeedFilterTestDataAsync()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mtg = await db.Games.SingleOrDefaultAsync(g => g.Name == MtgGame)
                  ?? new Game { Name = MtgGame };
        if (mtg.Id == 0) db.Games.Add(mtg);

        var swu = await db.Games.SingleAsync(g => g.Name == SwuGame);

        var mtgSetWithCards = new Set { Game = mtg, Code = "TST", Name = "Test Set" };
        var mtgEmptySet = new Set { Game = mtg, Code = "EMP", Name = "Empty Set" };
        db.Sets.AddRange(mtgSetWithCards, mtgEmptySet);

        var rare = new Rarity { Game = mtg, Name = "Rare" };
        var white = new Aspect { Game = mtg, Name = "White" };
        var blue = new Aspect { Game = mtg, Name = "Blue" };
        var black = new Aspect { Game = mtg, Name = "Black" };
        var colorless = new Aspect { Game = mtg, Name = "Colorless" };
        db.Aspects.AddRange(white, blue, black, colorless);

        // White-only card (cost 2, power 2, toughness 3)
        var whiteCard = new Card { Game = mtg, Name = "White Knight", Cost = 2, Power = 2, Toughness = 3 };
        whiteCard.CardAspects.Add(new CardAspect { Card = whiteCard, Aspect = white });
        whiteCard.CardColorIdentities.Add(new CardColorIdentity { Card = whiteCard, Aspect = white });

        // Azorius (White + Blue, cost 3, loyalty 4)
        var azoriusCard = new Card { Game = mtg, Name = "Azorius Walker", Cost = 3, Loyalty = 4 };
        azoriusCard.CardAspects.Add(new CardAspect { Card = azoriusCard, Aspect = white });
        azoriusCard.CardAspects.Add(new CardAspect { Card = azoriusCard, Aspect = blue });
        azoriusCard.CardColorIdentities.Add(new CardColorIdentity { Card = azoriusCard, Aspect = white });
        azoriusCard.CardColorIdentities.Add(new CardColorIdentity { Card = azoriusCard, Aspect = blue });

        // Esper (White + Blue + Black, cost 5, power 4, toughness 5)
        var esperCard = new Card { Game = mtg, Name = "Esper Sphinx", Cost = 5, Power = 4, Toughness = 5 };
        esperCard.CardAspects.Add(new CardAspect { Card = esperCard, Aspect = white });
        esperCard.CardAspects.Add(new CardAspect { Card = esperCard, Aspect = blue });
        esperCard.CardAspects.Add(new CardAspect { Card = esperCard, Aspect = black });
        esperCard.CardColorIdentities.Add(new CardColorIdentity { Card = esperCard, Aspect = white });
        esperCard.CardColorIdentities.Add(new CardColorIdentity { Card = esperCard, Aspect = blue });
        esperCard.CardColorIdentities.Add(new CardColorIdentity { Card = esperCard, Aspect = black });

        // Colorless card (cost 1)
        var artifact = new Card { Game = mtg, Name = "Sol Ring", Cost = 1 };
        artifact.CardAspects.Add(new CardAspect { Card = artifact, Aspect = colorless });
        artifact.CardColorIdentities.Add(new CardColorIdentity { Card = artifact, Aspect = colorless });

        db.Cards.AddRange(whiteCard, azoriusCard, esperCard, artifact);
        db.CardPrintings.AddRange(
            new CardPrinting { Card = whiteCard, Number = "1", CatalogSet = mtgSetWithCards, RarityLookup = rare },
            new CardPrinting { Card = azoriusCard, Number = "2", CatalogSet = mtgSetWithCards, RarityLookup = rare },
            new CardPrinting { Card = esperCard, Number = "3", CatalogSet = mtgSetWithCards, RarityLookup = rare },
            new CardPrinting { Card = artifact, Number = "4", CatalogSet = mtgSetWithCards, RarityLookup = rare }
        );

        // SWU upgrade card
        var swuSet = new Set { Game = swu, Code = "SOR", Name = "Spark of Rebellion" };
        var swuCommon = new Rarity { Game = swu, Name = "Common" };
        var upgrade = new Card { Game = swu, Name = "Jedi Lightsaber", Cost = 3, UpgradePower = 3, UpgradeHp = 1 };
        db.Cards.Add(upgrade);
        db.CardPrintings.Add(new CardPrinting { Card = upgrade, Number = "054", CatalogSet = swuSet, RarityLookup = swuCommon });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Migration_ExistingMtgJson_BackfillsFiltersWithoutResync()
    {
        await SeedFilterTestDataAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261117192545_AddMtgFilterColumnsAndColorIdentity");
        await db.CardColorIdentities.ExecuteDeleteAsync();
        var cards = await db.Cards.Where(c => c.Game.Name == MtgGame).OrderBy(c => c.Name).ToListAsync();
        var payloads = new[]
        {
            """{"power":"*","toughness":"X","loyalty":"4","colorIdentity":["W","U","W"]}""",
            """{"power":"4","toughness":"5","loyalty":"2147483648","colorIdentity":["W","U","B"]}""",
            """{"power":"0","toughness":"-1","loyalty":"2.5","colorIdentity":[]}""",
            """{"power":"+2","toughness":" 3 ","loyalty":"3foo","colorIdentity":["W"]}"""
        };
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].Power = null;
            cards[i].Toughness = null;
            cards[i].Loyalty = null;
            cards[i].ExtraCardDataJson = payloads[i];
        }
        var swu = await db.Cards.SingleAsync(c => c.Game.Name == SwuGame);
        swu.ExtraCardDataJson = """{"power":"99","colorIdentity":["W"]}""";
        var malformed = new Card { GameId = cards[0].GameId, Name = "Malformed JSON", ExtraCardDataJson = "invalid" };
        db.Cards.Add(malformed);
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var recovered = await db.Cards.Where(c => c.Game.Name == MtgGame && c.Id != malformed.Id)
            .OrderBy(c => c.Name).ToListAsync();
        Assert.Equal(new int?[] { null, 4, 0, 2 }, recovered.Select(c => c.Power));
        Assert.Equal(new int?[] { null, 5, -1, 3 }, recovered.Select(c => c.Toughness));
        Assert.Equal(new int?[] { 4, null, null, null }, recovered.Select(c => c.Loyalty));
        Assert.Null((await db.Cards.SingleAsync(c => c.Id == swu.Id)).Power);
        Assert.False(await db.CardColorIdentities.AnyAsync(ci => ci.CardId == swu.Id || ci.CardId == malformed.Id));
        Assert.Equal(7, await db.CardColorIdentities.CountAsync());

        using var client = factory.CreateClient();
        var loyalty = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&loyalty=4");
        Assert.Equal("Azorius Walker", Assert.Single(loyalty!).CardName);
        var identity = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&colorIdentity=White,Blue,Colorless");
        Assert.Equal(3, identity!.Count);
        Assert.DoesNotContain(identity, p => p.CardName == "Esper Sphinx");
        await db.Database.MigrateAsync();
        Assert.Equal(7, await db.CardColorIdentities.CountAsync());
    }

    [Fact]
    public async Task Printings_MtgColorsSubsetAndExact_FilterCorrectly()
    {
        await SeedFilterTestDataAsync();
        using var client = factory.CreateClient();

        // Subset ("AND" in UI): White or Blue, but NO Black -> White Knight + Azorius Walker
        var subset = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&aspect=White,Blue&aspectMethod=subset");
        Assert.NotNull(subset);
        Assert.Equal(2, subset.Count);
        Assert.Contains(subset, p => p.CardName == "White Knight");
        Assert.Contains(subset, p => p.CardName == "Azorius Walker");

        // Exact ("EXACT" in UI): Strictly White + Blue -> Azorius Walker only
        var exact = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&aspect=White,Blue&aspectMethod=exact");
        Assert.NotNull(exact);
        var single = Assert.Single(exact);
        Assert.Equal("Azorius Walker", single.CardName);
    }

    [Fact]
    public async Task Printings_MtgColorIdentity_ExcludesUnselectedColors()
    {
        await SeedFilterTestDataAsync();
        using var client = factory.CreateClient();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&colorIdentity=White,Blue,Colorless");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.DoesNotContain(printings, p => p.CardName == "Esper Sphinx");
    }

    [Fact]
    public async Task Printings_NumericFilters_CostToughnessLoyaltyUpgradeStats_Work()
    {
        await SeedFilterTestDataAsync();
        using var client = factory.CreateClient();

        var highCost = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&cost=>3");
        Assert.NotNull(highCost);
        Assert.Equal("Esper Sphinx", Assert.Single(highCost).CardName);

        var highToughness = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&toughness=>4");
        Assert.NotNull(highToughness);
        Assert.Equal("Esper Sphinx", Assert.Single(highToughness).CardName);

        var loyalty = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Magic+The+Gathering&loyalty=4");
        Assert.NotNull(loyalty);
        Assert.Equal("Azorius Walker", Assert.Single(loyalty).CardName);

        var swuUpgrade = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&upgradePower=>2&upgradeHp=1");
        Assert.NotNull(swuUpgrade);
        Assert.Equal("Jedi Lightsaber", Assert.Single(swuUpgrade).CardName);
    }

    [Fact]
    public async Task Facets_ExcludesEmptySetsWithZeroSyncedCards()
    {
        await SeedFilterTestDataAsync();
        using var client = factory.CreateClient();

        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Magic+The+Gathering");
        Assert.NotNull(facets);
        Assert.Contains(facets.Sets, s => s.Value == "Test Set");
        Assert.DoesNotContain(facets.Sets, s => s.Value == "Empty Set");
    }
}
