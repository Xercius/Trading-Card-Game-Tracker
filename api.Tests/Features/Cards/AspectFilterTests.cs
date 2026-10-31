using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class AspectFilterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwuGame = "Star Wars Unlimited";

    private async Task SeedAspectTestCardsAsync()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var game = await db.Games.SingleAsync(g => g.Name == SwuGame);
        var set = new Set { Game = game, Code = "SOR", Name = "Spark of Rebellion", ReleasedAt = new DateOnly(2024, 3, 8) };
        var rarity = new Rarity { Game = game, Name = "Common" };

        var aggression = new Aspect { Game = game, Name = "Aggression" };
        var command = new Aspect { Game = game, Name = "Command" };

        // 1. Single Aggression card
        var card1 = new Card { Game = game, Name = "Single Aggression" };
        card1.CardAspects.Add(new CardAspect { Card = card1, Aspect = aggression, Count = 1 });
        db.Cards.Add(card1);
        db.CardPrintings.Add(new CardPrinting { Card = card1, Number = "SOR-001", CatalogSet = set, RarityLookup = rarity });

        // 2. Double Aggression card
        var card2 = new Card { Game = game, Name = "Double Aggression" };
        card2.CardAspects.Add(new CardAspect { Card = card2, Aspect = aggression, Count = 2 });
        db.Cards.Add(card2);
        db.CardPrintings.Add(new CardPrinting { Card = card2, Number = "SOR-002", CatalogSet = set, RarityLookup = rarity });

        // 3. Aggression + Command card
        var card3 = new Card { Game = game, Name = "Aggression and Command" };
        card3.CardAspects.Add(new CardAspect { Card = card3, Aspect = aggression, Count = 1 });
        card3.CardAspects.Add(new CardAspect { Card = card3, Aspect = command, Count = 1 });
        db.Cards.Add(card3);
        db.CardPrintings.Add(new CardPrinting { Card = card3, Number = "SOR-003", CatalogSet = set, RarityLookup = rarity });

        // 4. Command only card
        var card4 = new Card { Game = game, Name = "Single Command" };
        card4.CardAspects.Add(new CardAspect { Card = card4, Aspect = command, Count = 1 });
        db.Cards.Add(card4);
        db.CardPrintings.Add(new CardPrinting { Card = card4, Number = "SOR-004", CatalogSet = set, RarityLookup = rarity });

        // 5. Neutral / No Aspect card
        var card5 = new Card { Game = game, Name = "Neutral Base" };
        db.Cards.Add(card5);
        db.CardPrintings.Add(new CardPrinting { Card = card5, Number = "SOR-005", CatalogSet = set, RarityLookup = rarity });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Aspect_OrMethod_MatchesCardsWithAnySpecifiedAspect()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Aggression (Count >= 1) with OR
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=Aggression&aspectMethod=or");
        Assert.NotNull(printings);
        Assert.Equal(3, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Single Aggression");
        Assert.Contains(printings, p => p.CardName == "Double Aggression");
        Assert.Contains(printings, p => p.CardName == "Aggression and Command");
    }

    [Fact]
    public async Task Aspect_OrMethod_WithDoubleAspect_MatchesOnlyDoubleOrHigher()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Aggression x2 with OR -> cards with Aggression count >= 2
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=Aggression,Aggression&aspectMethod=or");
        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Double Aggression", printing.CardName);
    }

    [Fact]
    public async Task Aspect_AndMethod_MatchesCardsWithAllSpecifiedAspects()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Aggression AND Command
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=Aggression,Command&aspectMethod=and");
        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Aggression and Command", printing.CardName);
    }

    [Fact]
    public async Task Aspect_ExcludeMethod_PreservesCardsWithNoAspect()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Exclude Aggression: should keep Single Command and Neutral Base
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=Aggression&aspectMethod=exclude");
        Assert.NotNull(printings);
        Assert.Equal(2, printings.Count);
        Assert.Contains(printings, p => p.CardName == "Single Command");
        Assert.Contains(printings, p => p.CardName == "Neutral Base");
    }

    [Fact]
    public async Task Aspect_ExcludeMethod_WithDoubleAspect_KeepsCardsWithSingleAspect()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Exclude Aggression x2: should exclude Double Aggression, but keep Single Aggression, Aggression and Command, Single Command, Neutral Base
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=Aggression,Aggression&aspectMethod=exclude");
        Assert.NotNull(printings);
        Assert.Equal(4, printings.Count);
        Assert.DoesNotContain(printings, p => p.CardName == "Double Aggression");
        Assert.Contains(printings, p => p.CardName == "Single Aggression");
        Assert.Contains(printings, p => p.CardName == "Aggression and Command");
        Assert.Contains(printings, p => p.CardName == "Single Command");
        Assert.Contains(printings, p => p.CardName == "Neutral Base");
    }

    [Fact]
    public async Task Aspect_NoAspect_MatchesOnlyCardsWithZeroAspects()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=no-aspect");
        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Neutral Base", printing.CardName);
    }

    [Fact]
    public async Task Aspect_Exclude_NoAspect_ExcludesCardsWithNoAspect()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&aspect=no-aspect&aspectMethod=exclude");
        Assert.NotNull(printings);
        Assert.Equal(4, printings.Count);
        Assert.DoesNotContain(printings, p => p.CardName == "Neutral Base");
    }

    [Fact]
    public async Task CardsEndpoint_AppliesAspectMethod()
    {
        await SeedAspectTestCardsAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetFromJsonAsync<CardListPageResponse>(
            "/api/cards?game=Star+Wars+Unlimited&aspect=Aggression&aspectMethod=exclude");
        Assert.NotNull(response);
        Assert.Equal(2, response.Items.Count);
        Assert.Contains(response.Items, c => c.Name == "Single Command");
        Assert.Contains(response.Items, c => c.Name == "Neutral Base");
    }
}
