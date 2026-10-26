using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class SwuNormalizedSearchTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwuGame = "Star Wars Unlimited";

    [Fact]
    public async Task Search_UsesNormalizedSetRarityAndAnyType_AndReturnsNormalizedDetails()
    {
        await factory.ResetDatabaseAsync();
        int cardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            cardId = await SeedCardAsync(db, "Luke Skywalker", "SOR", "Spark of Rebellion",
                new DateOnly(2025, 10, 3), "Leader", "Leader Unit", "Heroism", 2);
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&set=SOR&rarity=Rare&type=Leader+Unit");

        var printing = Assert.Single(printings!);
        Assert.Equal("Spark of Rebellion", printing.SetName);
        Assert.Equal("SOR", printing.SetCode);
        Assert.Equal("Rare", printing.Rarity);
        Assert.Contains("Leader", printing.CardTypes);
        Assert.Contains("Leader Unit", printing.CardTypes);
        Assert.Equal(new CardAspectResponse("Heroism", 2), Assert.Single(printing.Aspects));
        Assert.Equal(new DateOnly(2025, 10, 3), printing.ReleaseDate);

        var cards = await client.GetFromJsonAsync<CardListPageResponse>(
            "/api/cards?game=Star+Wars+Unlimited&set=SOR&rarity=Rare&type=Leader+Unit");
        var card = Assert.Single(cards!.Items);
        Assert.Equal(cardId, card.CardId);
        Assert.Contains("Leader Unit", card.CardTypes);
        Assert.Equal(2, Assert.Single(card.Aspects).Count);

        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Star+Wars+Unlimited");
        Assert.Contains(facets!.Sets, option => option.Value == "SOR");
        Assert.Contains(facets.Rarities, option => option.Value == "Rare");
        Assert.Contains(facets.Types, option => option.Value == "Leader");
        Assert.Contains(facets.Types, option => option.Value == "Leader Unit");

        var details = await client.GetFromJsonAsync<CardDetailResponse>($"/api/cards/{cardId}");
        Assert.NotNull(details);
        Assert.Contains("Leader Unit", details!.CardTypes!);
        Assert.Equal(2, Assert.Single(details.Aspects!).Count);
        var detailPrinting = Assert.Single(details.Printings);
        Assert.Equal("SOR", detailPrinting.SetCode);
        Assert.Equal(new DateOnly(2025, 10, 3), detailPrinting.ReleaseDate);
        Assert.Equal("horizontal", detailPrinting.ImageOrientation);
        Assert.Equal("vertical", detailPrinting.BackImageOrientation);
    }

    [Fact]
    public async Task UniqueCards_OrdersBySetReleaseDateWithoutUsingPrintingDateForUndatedSets()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var game = await db.Games.SingleAsync(candidate => candidate.Name == SwuGame);
            var cardType = new CardType { Game = game, Name = "Unit" };
            var card = new Card { Game = SwuGame, Name = "Reprinted Card", CardType = "Unit" };
            card.CardTypes.Add(new CardCardType { Card = card, CardType = cardType, SourceRole = "Type" });
            db.Cards.Add(card);
            AddPrinting(card, game, "LATE", "Late set", new DateOnly(2024, 1, 1), "2018-01-01");
            AddPrinting(card, game, "EARLY", "Early set", new DateOnly(2021, 1, 1), "2030-01-01");
            AddPrinting(card, game, "UNDATED", "Undated set", null, "2010-01-01");

            var secondCard = new Card { Game = SwuGame, Name = "Undated Preferred", CardType = "Unit" };
            secondCard.CardTypes.Add(new CardCardType
            {
                Card = secondCard,
                CardType = cardType,
                SourceRole = "Type"
            });
            db.Cards.Add(secondCard);
            AddPrinting(secondCard, game, "UNKNOWN", "Unknown date", null, "2010-01-01");
            AddPrinting(secondCard, game, "KNOWN", "Known date", new DateOnly(2025, 1, 1), "2025-01-01");

            var setOnlyCard = new Card { Game = "Legacy Game", Name = "Set-associated card", CardType = "Unit" };
            db.Cards.Add(setOnlyCard);
            AddPrinting(setOnlyCard, game, "SET-LATE", "Later set", new DateOnly(2024, 1, 1), "2018-01-01");
            AddPrinting(setOnlyCard, game, "SET-EARLY", "Earlier set", new DateOnly(2021, 1, 1), "2030-01-01");
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&uniqueCards=true");

        Assert.NotNull(printings);
        Assert.Equal("EARLY", Assert.Single(printings!, item => item.CardName == "Reprinted Card").SetCode);
        Assert.Equal("KNOWN", Assert.Single(printings, item => item.CardName == "Undated Preferred").SetCode);
        Assert.Equal("SET-EARLY", Assert.Single(printings, item => item.CardName == "Set-associated card").SetCode);
    }

    private static async Task<int> SeedCardAsync(
        AppDbContext db,
        string name,
        string setCode,
        string setName,
        DateOnly releasedAt,
        string firstType,
        string secondType,
        string aspectName,
        int aspectCount)
    {
        var game = await db.Games.SingleAsync(candidate => candidate.Name == SwuGame);
        var set = new Set { Game = game, Code = setCode, Name = setName, ReleasedAt = releasedAt };
        var rarity = new Rarity { Game = game, Name = "Rare" };
        var card = new Card { Game = SwuGame, Name = name, CardType = firstType };
        card.CardTypes.Add(new CardCardType
        {
            Card = card,
            CardType = new CardType { Game = game, Name = firstType },
            SourceRole = "Type"
        });
        card.CardTypes.Add(new CardCardType
        {
            Card = card,
            CardType = new CardType { Game = game, Name = secondType },
            SourceRole = "Type2"
        });
        card.CardAspects.Add(new CardAspect
        {
            Card = card,
            Aspect = new Aspect { Game = game, Name = aspectName },
            Count = aspectCount
        });
        db.Cards.Add(card);
        db.CardPrintings.Add(new CardPrinting
        {
            Card = card,
            Set = "Legacy set",
            Number = "SOR-001",
            Rarity = "Common",
            Style = "Standard",
            ImageUrl = "/front.png",
            BackImageUrl = "/back.png",
            ArtFrontHorizontal = true,
            ArtBackHorizontal = false,
            CatalogSet = set,
            RarityLookup = rarity
        });
        await db.SaveChangesAsync();
        return card.Id;
    }

    private static void AddPrinting(
        Card card,
        Game game,
        string setCode,
        string setName,
        DateOnly? setReleaseDate,
        string printingReleaseDate)
    {
        card.Printings.Add(new CardPrinting
        {
            Card = card,
            Set = setCode,
            Number = $"{setCode}-001",
            Rarity = "Common",
            Style = "Standard",
            CatalogSet = new Set
            {
                Game = game,
                Code = setCode,
                Name = setName,
                ReleasedAt = setReleaseDate
            },
            ReleasedAt = DateOnly.Parse(printingReleaseDate)
        });
    }
}
