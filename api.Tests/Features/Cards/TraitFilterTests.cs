using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class TraitFilterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwuGame = "Star Wars Unlimited";

    private async Task SeedTraitTestCardsAsync()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var game = await db.Games.SingleAsync(g => g.Name == SwuGame);
        var set = new Set { Game = game, Code = "SOR", Name = "Spark of Rebellion", ReleasedAt = new DateOnly(2024, 3, 8) };
        var rarity = new Rarity { Game = game, Name = "Common" };

        var force = new Trait { Game = game, Name = "Force" };
        var imperial = new Trait { Game = game, Name = "Imperial" };
        var sith = new Trait { Game = game, Name = "Sith" };
        var rebel = new Trait { Game = game, Name = "Rebel" };

        // 1. Force + Imperial + Sith (e.g. Darth Vader)
        var card1 = new Card { Game = game, Name = "Darth Vader" };
        card1.CardTraits.Add(new CardTrait { Card = card1, Trait = force });
        card1.CardTraits.Add(new CardTrait { Card = card1, Trait = imperial });
        card1.CardTraits.Add(new CardTrait { Card = card1, Trait = sith });
        db.Cards.Add(card1);
        db.CardPrintings.Add(new CardPrinting { Card = card1, Number = "SOR-001", CatalogSet = set, RarityLookup = rarity });

        // 2. Force + Rebel (e.g. Luke Skywalker)
        var card2 = new Card { Game = game, Name = "Luke Skywalker" };
        card2.CardTraits.Add(new CardTrait { Card = card2, Trait = force });
        card2.CardTraits.Add(new CardTrait { Card = card2, Trait = rebel });
        db.Cards.Add(card2);
        db.CardPrintings.Add(new CardPrinting { Card = card2, Number = "SOR-002", CatalogSet = set, RarityLookup = rarity });

        // 3. Imperial only (e.g. Stormtrooper)
        var card3 = new Card { Game = game, Name = "Stormtrooper" };
        card3.CardTraits.Add(new CardTrait { Card = card3, Trait = imperial });
        db.Cards.Add(card3);
        db.CardPrintings.Add(new CardPrinting { Card = card3, Number = "SOR-003", CatalogSet = set, RarityLookup = rarity });

        // 4. Rebel only (e.g. Rebel Trooper)
        var card4 = new Card { Game = game, Name = "Rebel Trooper" };
        card4.CardTraits.Add(new CardTrait { Card = card4, Trait = rebel });
        db.Cards.Add(card4);
        db.CardPrintings.Add(new CardPrinting { Card = card4, Number = "SOR-004", CatalogSet = set, RarityLookup = rarity });

        // 5. No traits (e.g. Space Base)
        var card5 = new Card { Game = game, Name = "Space Base" };
        db.Cards.Add(card5);
        db.CardPrintings.Add(new CardPrinting { Card = card5, Number = "SOR-005", CatalogSet = set, RarityLookup = rarity });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Trait_OrMethod_MatchesCardsWithAnySpecifiedTrait()
    {
        await SeedTraitTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Force OR Imperial: should match Darth Vader (Force, Imperial), Luke Skywalker (Force), Stormtrooper (Imperial)
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&trait=Force,Imperial&traitMethod=or");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Darth Vader");
        Assert.Contains(printings, p => p.CardName == "Luke Skywalker");
        Assert.Contains(printings, p => p.CardName == "Stormtrooper");
    }

    [Fact]
    public async Task Trait_AndMethod_MatchesCardsWithAllSpecifiedTraits()
    {
        await SeedTraitTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Force AND Imperial: only Darth Vader has both
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&trait=Force,Imperial&traitMethod=and");
        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Darth Vader", printing.CardName);
    }

    [Fact]
    public async Task Trait_ExcludeMethod_ExcludesCardsWithAnySpecifiedTraits()
    {
        await SeedTraitTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Exclude Imperial: should exclude Darth Vader and Stormtrooper, leaving Luke Skywalker, Rebel Trooper, Space Base
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&trait=Imperial&traitMethod=exclude");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Luke Skywalker");
        Assert.Contains(printings, p => p.CardName == "Rebel Trooper");
        Assert.Contains(printings, p => p.CardName == "Space Base");
        Assert.DoesNotContain(printings, p => p.CardName == "Darth Vader");
        Assert.DoesNotContain(printings, p => p.CardName == "Stormtrooper");
    }

    [Fact]
    public async Task CardsEndpoint_AppliesTraitMethod()
    {
        await SeedTraitTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Cards virtualized search endpoint with traitMethod=and
        var response = await client.GetFromJsonAsync<CardListPageResponse>(
            "/api/cards?game=Star+Wars+Unlimited&trait=Force,Sith&traitMethod=and");
        Assert.NotNull(response);
        var card = Assert.Single(response.Items);
        Assert.Equal("Darth Vader", card.Name);
    }

    [Fact]
    public async Task FacetsEndpoint_AppliesTraitMethod()
    {
        await SeedTraitTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // When filtering with traitMethod=exclude for Imperial, the available cards are Luke, Rebel Trooper, Space Base
        // Available traits computed on traitQuery are not filtered by traits itself, but other facet queries are
        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Star+Wars+Unlimited&trait=Imperial&traitMethod=exclude");
        Assert.NotNull(facets);
        Assert.NotEmpty(facets.Traits);
    }
}
