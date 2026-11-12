using System.Text.Json;
using api.Features.Decks.Services;
using api.Models;
using Xunit;

namespace api.Tests.Features.Decks;

public sealed class PokemonDeckValidationServiceTests
{
    private readonly PokemonDeckValidationService _service = new();

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Validate_StandardDeckSize_EnforcesExactSixtyCards(int cardCount, bool isValid)
    {
        var cards = BuildDeck(cardCount, CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1));

        var result = _service.Validate("Standard", cards);

        Assert.Equal(isValid, result.IsValid);
        Assert.Equal(cardCount, result.CardCount);
        Assert.Equal(
            !isValid,
            result.Violations.Any(issue => issue.Contains("exactly 60 cards", StringComparison.Ordinal)));
    }

    [Fact]
    public void Validate_WithoutBasicPokemon_ReturnsViolation()
    {
        var result = _service.Validate("Standard", BuildDeck(60));

        Assert.Contains(result.Violations, issue => issue.Contains("Basic Pokémon", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t")]
    public void Validate_NullOrWhitespaceFormat_DefaultsToStandard(string? format)
    {
        var cards = BuildDeck(60, CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1));

        var result = _service.Validate(format, cards);

        Assert.Equal("Standard", result.Format);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Violations));
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(4, 4, false)]
    public void Validate_SameNameAcrossPrintings_UsesCombinedCopyCount(int firstPrintingCopies, int secondPrintingCopies, bool isValid)
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Charizard", "Pokemon"), firstPrintingCopies, "SET-A"),
            CreateDeckCard(CreateCard("Charizard", "Pokemon"), secondPrintingCopies, "SET-B"));

        var result = _service.Validate("Standard", cards);

        Assert.Equal(isValid, !result.Violations.Any(issue => issue.Contains("\"Charizard\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void Validate_BasicEnergy_IsExemptFromCopyLimit()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Fire Energy", "Energy", energyType: "Basic"), 59));

        var result = _service.Validate("Standard", cards);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Violations));
    }

    [Fact]
    public void Validate_AceSpecCardsWithDifferentNames_ShareOneDeckLimit()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Prime Catcher", "Item"), 1, rarityName: "Ace Spec Rare"),
            CreateDeckCard(CreateCard("Legacy Energy", "Energy"), 1, rarityName: "Ace Spec Rare"));

        var result = _service.Validate("Standard", cards);

        Assert.Contains(result.Violations, issue => issue.Contains("ACE SPEC", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AceSpecNameOrTextWithoutRarity_DoesNotCountTowardDeckLimit()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("ACE SPEC Special Item", "Item"), 1),
            CreateDeckCard(CreateCard("Other Item", "Item", text: "ACE SPEC"), 1));

        var result = _service.Validate("Standard", cards);

        Assert.DoesNotContain(result.Violations, issue => issue.Contains("ACE SPEC", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AceSpecRarityWithoutOtherClassification_EnforcesDeckLimit()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Special card one", "Item"), 1, rarityName: "Ace Spec Rare"),
            CreateDeckCard(CreateCard("Special card two", "Item"), 1, rarityName: "Ace Spec Rare"));

        var result = _service.Validate("Standard", cards);

        Assert.Contains(result.Violations, issue => issue.Contains("ACE SPEC", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RadiantPokemonWithDifferentNames_ShareOneDeckLimit()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Radiant Charizard", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Radiant Greninja", "Pokemon", "Basic"), 1));

        var result = _service.Validate("Standard", cards);

        Assert.Contains(result.Violations, issue => issue.Contains("Radiant Pokémon", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DifferentCardNames_AreCountedIndependently()
    {
        var cards = BuildDeck(
            60,
            CreateDeckCard(CreateCard("Basic Pokémon", "Pokemon", "Basic"), 1),
            CreateDeckCard(CreateCard("Charizard", "Pokemon"), 4),
            CreateDeckCard(CreateCard("Charizard V", "Pokemon"), 4),
            CreateDeckCard(CreateCard("Charizard VMAX", "Pokemon"), 4),
            CreateDeckCard(CreateCard("Charizard VSTAR", "Pokemon"), 4),
            CreateDeckCard(CreateCard("Radiant Charizard", "Pokemon"), 1));

        var result = _service.Validate("Standard", cards);

        Assert.DoesNotContain(result.Violations, issue => issue.Contains("exceeds the limit of 4 copies", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReturnsEveryViolation()
    {
        var cards = BuildDeck(
            61,
            CreateDeckCard(CreateCard("Over-limit", "Trainer"), 5),
            CreateDeckCard(CreateCard("Prime Catcher", "Item"), 1, rarityName: "Ace Spec Rare"),
            CreateDeckCard(CreateCard("Legacy Energy", "Energy"), 1, rarityName: "Ace Spec Rare"),
            CreateDeckCard(CreateCard("Radiant Charizard", "Pokemon"), 1),
            CreateDeckCard(CreateCard("Radiant Greninja", "Pokemon"), 1));

        var result = _service.Validate("Standard", cards);

        Assert.Equal(5, result.Violations.Count);
    }

    private static List<DeckCard> BuildDeck(int targetCount, params DeckCard[] initialCards)
    {
        var cards = initialCards.ToList();
        var currentCount = cards.Sum(card => card.QuantityInDeck);
        for (var index = 0; currentCount < targetCount; index++, currentCount++)
        {
            cards.Add(CreateDeckCard(CreateCard($"Filler {index}", "Trainer"), 1));
        }

        return cards;
    }

    private static Card CreateCard(
        string name,
        string cardType,
        string? stage = null,
        string? text = null,
        string? energyType = null)
    {
        var card = new Card
        {
            Name = name,
            Stage = stage,
            Text = text,
            ExtraCardDataJson = energyType is null ? null : JsonSerializer.Serialize(new { energyType })
        };
        card.CardTypes.Add(new CardCardType
        {
            Card = card,
            CardType = new CardType { Name = cardType, GameId = 1 },
            SourceRole = "Type"
        });

        return card;
    }

    private static DeckCard CreateDeckCard(
        Card card,
        int quantity,
        string? setCode = null,
        string? rarityName = null)
    {
        var printing = new CardPrinting
        {
            Card = card,
            Number = "1",
            CatalogSet = setCode is null ? null : new Set { Code = setCode, Name = setCode, GameId = 1 },
            RarityLookup = rarityName is null ? null : new Rarity { Name = rarityName, GameId = 1 }
        };
        return new DeckCard
        {
            CardPrinting = printing,
            QuantityInDeck = quantity
        };
    }
}
