using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class KeywordFilterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwuGame = "Star Wars Unlimited";

    private async Task SeedKeywordTestCardsAsync()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var game = await db.Games.SingleAsync(g => g.Name == SwuGame);
        var set = new Set { Game = game, Code = "SOR", Name = "Spark of Rebellion", ReleasedAt = new DateOnly(2024, 3, 8) };
        var rarity = new Rarity { Game = game, Name = "Common" };

        var sentinel = new Keyword { Game = game, Name = "Sentinel" };
        var grit = new Keyword { Game = game, Name = "Grit" };
        var overwhelm = new Keyword { Game = game, Name = "Overwhelm" };

        // 1. Sentinel + Grit (e.g. Obi-Wan Kenobi)
        var card1 = new Card { Game = game, Name = "Obi-Wan Kenobi" };
        card1.CardKeywords.Add(new CardKeyword { Card = card1, Keyword = sentinel });
        card1.CardKeywords.Add(new CardKeyword { Card = card1, Keyword = grit });
        db.Cards.Add(card1);
        db.CardPrintings.Add(new CardPrinting { Card = card1, Number = "SOR-001", CatalogSet = set, RarityLookup = rarity });

        // 2. Sentinel only (e.g. Echo Base Defender)
        var card2 = new Card { Game = game, Name = "Echo Base Defender" };
        card2.CardKeywords.Add(new CardKeyword { Card = card2, Keyword = sentinel });
        db.Cards.Add(card2);
        db.CardPrintings.Add(new CardPrinting { Card = card2, Number = "SOR-002", CatalogSet = set, RarityLookup = rarity });

        // 3. Grit only (e.g. Chewbacca)
        var card3 = new Card { Game = game, Name = "Chewbacca" };
        card3.CardKeywords.Add(new CardKeyword { Card = card3, Keyword = grit });
        db.Cards.Add(card3);
        db.CardPrintings.Add(new CardPrinting { Card = card3, Number = "SOR-003", CatalogSet = set, RarityLookup = rarity });

        // 4. Overwhelm only (e.g. AT-AT)
        var card4 = new Card { Game = game, Name = "Blizzard Assault AT-AT" };
        card4.CardKeywords.Add(new CardKeyword { Card = card4, Keyword = overwhelm });
        db.Cards.Add(card4);
        db.CardPrintings.Add(new CardPrinting { Card = card4, Number = "SOR-004", CatalogSet = set, RarityLookup = rarity });

        // 5. No keywords (e.g. Basic Trooper)
        var card5 = new Card { Game = game, Name = "Basic Trooper" };
        db.Cards.Add(card5);
        db.CardPrintings.Add(new CardPrinting { Card = card5, Number = "SOR-005", CatalogSet = set, RarityLookup = rarity });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Keyword_OrMethod_MatchesCardsWithAnySpecifiedKeyword()
    {
        await SeedKeywordTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Sentinel OR Grit: should match Obi-Wan, Echo Base Defender, Chewbacca
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&keyword=Sentinel,Grit&keywordMethod=or");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Obi-Wan Kenobi");
        Assert.Contains(printings, p => p.CardName == "Echo Base Defender");
        Assert.Contains(printings, p => p.CardName == "Chewbacca");
    }

    [Fact]
    public async Task Keyword_AndMethod_MatchesCardsWithAllSpecifiedKeywords()
    {
        await SeedKeywordTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Sentinel AND Grit: only Obi-Wan has both
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&keyword=Sentinel,Grit&keywordMethod=and");
        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Obi-Wan Kenobi", printing.CardName);
    }

    [Fact]
    public async Task Keyword_ExcludeMethod_ExcludesCardsWithAnySpecifiedKeywords()
    {
        await SeedKeywordTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Exclude Sentinel: should exclude Obi-Wan and Echo Base Defender, keeping Chewbacca, AT-AT, Basic Trooper
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&keyword=Sentinel&keywordMethod=exclude");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Chewbacca");
        Assert.Contains(printings, p => p.CardName == "Blizzard Assault AT-AT");
        Assert.Contains(printings, p => p.CardName == "Basic Trooper");
        Assert.DoesNotContain(printings, p => p.CardName == "Obi-Wan Kenobi");
        Assert.DoesNotContain(printings, p => p.CardName == "Echo Base Defender");
    }

    [Fact]
    public async Task CardsEndpoint_AppliesKeywordMethod()
    {
        await SeedKeywordTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetFromJsonAsync<CardListPageResponse>(
            "/api/cards?game=Star+Wars+Unlimited&keyword=Sentinel,Grit&keywordMethod=and");
        Assert.NotNull(response);
        var card = Assert.Single(response.Items);
        Assert.Equal("Obi-Wan Kenobi", card.Name);
    }

    [Fact]
    public async Task FacetsEndpoint_AppliesKeywordMethod()
    {
        await SeedKeywordTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Star+Wars+Unlimited&keyword=Sentinel&keywordMethod=exclude");
        Assert.NotNull(facets);
        Assert.NotEmpty(facets.Keywords);
    }
}

