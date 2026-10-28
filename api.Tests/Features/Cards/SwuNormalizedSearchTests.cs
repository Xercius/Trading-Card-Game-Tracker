using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Importing;
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
            "/api/cards/printings?game=Star+Wars+Unlimited&set=SOR&rarity=Rare&type=Leader+Unit&arena=Ground&aspect=Heroism&trait=Rebel");

        var printing = Assert.Single(printings!);
        Assert.Equal("Spark of Rebellion", printing.SetName);
        Assert.Equal("SOR", printing.SetCode);
        Assert.Equal("Rare", printing.Rarity);
        Assert.Contains("Leader", printing.CardTypes);
        Assert.Contains("Leader Unit", printing.CardTypes);
        Assert.Equal(new CardAspectResponse("Heroism", 2), Assert.Single(printing.Aspects));
        Assert.Equal(new DateOnly(2025, 10, 3), printing.ReleaseDate);

        using var cardsResponse = await client.GetAsync(
            "/api/cards?game=Star+Wars+Unlimited&set=SOR&rarity=Rare&type=Leader+Unit&arena=Ground&aspect=Heroism&trait=Rebel");
        Assert.True(cardsResponse.IsSuccessStatusCode, await cardsResponse.Content.ReadAsStringAsync());
        var cards = await cardsResponse.Content.ReadFromJsonAsync<CardListPageResponse>();
        var card = Assert.Single(cards!.Items);
        Assert.Equal(cardId, card.CardId);
        Assert.Contains("Leader Unit", card.CardTypes);
        Assert.Equal(2, Assert.Single(card.Aspects).Count);
        Assert.Equal(1, card.PrintingsCount);
        Assert.Equal("SOR", card.Primary!.SetCode);
        Assert.Equal(new DateOnly(2025, 10, 3), card.Primary.ReleaseDate);

        var codeSearch = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?game=Star+Wars+Unlimited&q=SOR");
        Assert.Single(codeSearch!);
        var codeSearchFacets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Star+Wars+Unlimited&q=SOR&printingSearch=true");
        Assert.Contains(codeSearchFacets!.Sets, option => option.Value == "SOR");

        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            "/api/cards/facets?game=Star+Wars+Unlimited");
        Assert.Contains(facets!.Sets, option => option.Value == "SOR");
        Assert.Contains(facets.Rarities, option => option.Value == "Rare");
        Assert.Contains(facets.Types, option => option.Value == "Leader");
        Assert.Contains(facets.Types, option => option.Value == "Leader Unit");
        Assert.Contains(facets.Arenas, option => option.Value == "Ground");
        Assert.Contains(facets.Aspects, option => option.Value == "Heroism");
        Assert.Contains(facets.Traits, option => option.Value == "Rebel");
        Assert.Contains(facets.Treatments, option => option.Value == "Standard");

        var details = await client.GetFromJsonAsync<CardDetailResponse>($"/api/cards/{cardId}");
        Assert.NotNull(details);
        Assert.Equal("Leader Unit", details!.CardType2);
        Assert.Equal("Ground", details.Arena);
        Assert.Equal(["Force", "Rebel"], details.Traits);
        Assert.Equal("<p><strong>On Attack:</strong> Give a Shield token.</p>", details.TextStyled);
        Assert.Equal("<p><strong>Epic Action:</strong> Deploy Luke.</p>", details.EpicActionStyled);
        Assert.Equal("<p><strong>When Deployed:</strong> Give a Shield token.</p>", details.DeployBoxStyled);
        Assert.Equal(6, details.Cost);
        Assert.Equal(7, details.Hp);
        Assert.Equal(4, details.Power);
        Assert.Contains("Leader Unit", details.CardTypes!);
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
            var card = new Card { Game = game, Name = "Reprinted Card" };
            card.CardTypes.Add(new CardCardType { Card = card, CardType = cardType, SourceRole = "Type" });
            db.Cards.Add(card);
            AddPrinting(db, card, game, "LATE", "Late set", new DateOnly(2024, 1, 1), "2018-01-01");
            AddPrinting(db, card, game, "EARLY", "Early set", new DateOnly(2021, 1, 1), "2030-01-01");
            AddPrinting(db, card, game, "UNDATED", "Undated set", null, "2010-01-01");

            var secondCard = new Card { Game = game, Name = "Undated Preferred" };
            secondCard.CardTypes.Add(new CardCardType
            {
                Card = secondCard,
                CardType = cardType,
                SourceRole = "Type"
            });
            db.Cards.Add(secondCard);
            AddPrinting(db, secondCard, game, "UNKNOWN", "Unknown date", null, "2010-01-01");
            AddPrinting(db, secondCard, game, "KNOWN", "Known date", new DateOnly(2025, 1, 1), "2025-01-01");

            var setOnlyCard = new Card
            {
                Game = TreatmentLookup.GetOrAddGame(db, "Legacy Game"),
                Name = "Set-associated card"
            };
            db.Cards.Add(setOnlyCard);
            AddPrinting(db, setOnlyCard, game, "SET-LATE", "Later set", new DateOnly(2024, 1, 1), "2018-01-01");
            AddPrinting(db, setOnlyCard, game, "SET-EARLY", "Earlier set", new DateOnly(2021, 1, 1), "2030-01-01");
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

    [Fact]
    public async Task GetCardPrintings_OrdersBySetReleaseDateAndLeavesUndatedSetsLast()
    {
        await factory.ResetDatabaseAsync();
        int cardId;
        int legacyCardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var game = await db.Games.SingleAsync(candidate => candidate.Name == SwuGame);
            var cardType = new CardType { Game = game, Name = "Unit" };
            var card = new Card { Game = game, Name = "Ordered Reprints" };
            card.CardTypes.Add(new CardCardType { Card = card, CardType = cardType, SourceRole = "Type" });
            db.Cards.Add(card);
            AddPrinting(db, card, game, "LATE", "Later set", new DateOnly(2024, 1, 1), "2018-01-01");
            AddPrinting(db, card, game, "TIE-Z", "Zulu set", new DateOnly(2024, 1, 1), "2010-01-01");
            AddPrinting(db, card, game, "EARLY", "Earlier set", new DateOnly(2021, 1, 1), "2030-01-01");
            AddPrinting(db, card, game, "TIE-A", "Alpha set", new DateOnly(2024, 1, 1), "2010-01-01");
            AddPrinting(db, card, game, "UNDATED", "Undated set", null, "2010-01-01");

            var legacyCard = new Card
            {
                Game = TreatmentLookup.GetOrAddGame(db, "Legacy Game"),
                Name = "Legacy Reprints"
            };
            legacyCard.Printings.Add(new CardPrinting
            {
                Card = legacyCard,
                Set = "Zulu",
                Number = "2",
                Rarity = "Rare",
                Treatment = TreatmentLookup.Upsert(db, legacyCard.Game, "Standard")
            });
            legacyCard.Printings.Add(new CardPrinting
            {
                Card = legacyCard,
                Set = "Alpha",
                Number = "1",
                Rarity = "Rare",
                Treatment = TreatmentLookup.Upsert(db, legacyCard.Game, "Standard")
            });
            db.Cards.Add(legacyCard);
            await db.SaveChangesAsync();
            cardId = card.Id;
            legacyCardId = legacyCard.Id;
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var printings = await client.GetFromJsonAsync<List<PrintingDto>>($"/api/cards/{cardId}/printings");
        var legacyPrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
            $"/api/cards/{legacyCardId}/printings");

        Assert.Equal(["EARLY", "TIE-A", "LATE", "TIE-Z", "UNDATED"],
            printings!.Select(printing => printing.SetCode));
        Assert.Equal(["Alpha", "Zulu"], legacyPrintings!.Select(printing => printing.SetName));
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
        var card = new Card
        {
            Game = game,
            Name = name,
            Cost = 6,
            Power = 4,
            Hp = 7,
            TextStyled = "<p><strong>On Attack:</strong> Give a Shield token.</p>",
            EpicActionStyled = "<p><strong>Epic Action:</strong> Deploy Luke.</p>",
            DeployBoxStyled = "<p><strong>When Deployed:</strong> Give a Shield token.</p>"
        };
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
        card.CardArenas.Add(new CardArena
        {
            Card = card,
            Arena = new Arena { Game = game, Name = "Ground" }
        });
        card.CardAspects.Add(new CardAspect
        {
            Card = card,
            Aspect = new Aspect { Game = game, Name = aspectName },
            Count = aspectCount
        });
        card.CardTraits.Add(new CardTrait
        {
            Card = card,
            Trait = new Trait { Game = game, Name = "Force" }
        });
        card.CardTraits.Add(new CardTrait
        {
            Card = card,
            Trait = new Trait { Game = game, Name = "Rebel" }
        });
        db.Cards.Add(card);
        db.CardPrintings.Add(new CardPrinting
        {
            Card = card,
            Set = "Legacy set",
            Number = "SOR-001",
            Rarity = "Common",
            Treatment = TreatmentLookup.Upsert(db, SwuGame, "Standard"),
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
        AppDbContext db,
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
            Treatment = TreatmentLookup.Upsert(db, card.Game, "Standard"),
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
