using System.Text.Json;
using api.Features.Decks.Services;
using api.Models;
using Xunit;

namespace api.Tests.Features.Decks;

public sealed class MtgDeckValidationServiceTests
{
    private readonly MtgDeckValidationService _service = new();

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(75, true)]
    public void Validate_ConstructedMainDeckSize_EnforcesMinimumSixty(int mainCount, bool expectedValid)
    {
        var cards = new List<DeckCard>
        {
            CreateDeckCard(CreateMtgCard("Forest", "Basic Land — Forest", ["G"]), mainCount)
        };

        var result = _service.Validate("Constructed", cards);

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(mainCount, result.MainDeckCount);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void Validate_ConstructedSideboard_EnforcesMaxFifteen(int sideboardCount, bool expectedValid)
    {
        var cards = new List<DeckCard>
        {
            CreateDeckCard(CreateMtgCard("Forest", "Basic Land — Forest", ["G"]), 60),
            CreateDeckCard(CreateMtgCard("Plains", "Basic Land — Plains", ["W"]), 0, quantitySideboard: sideboardCount)
        };

        var result = _service.Validate("Constructed", cards);

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(sideboardCount, result.SideboardCount);
    }

    [Fact]
    public void Validate_ConstructedCopyLimit_CountsMainAndSideboardCombined()
    {
        var bolt = CreateMtgCard("Lightning Bolt", "Instant", ["R"]);
        var cards = new List<DeckCard>
        {
            CreateDeckCard(CreateMtgCard("Mountain", "Basic Land — Mountain", ["R"]), 57),
            CreateDeckCard(bolt, 3, quantitySideboard: 2)
        };

        var result = _service.Validate("Constructed", cards);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("Lightning Bolt") && v.Contains("4 copies"));
    }

    [Fact]
    public void Validate_ConstructedAnyNumberAndUpToNCards_RespectsRulesText()
    {
        var rats = CreateMtgCard("Relentless Rats", "Creature — Rat", ["B"], rules: "A deck can have any number of cards named Relentless Rats.");
        var nazgul = CreateMtgCard("Nazgûl", "Creature — Wraith Knight", ["B"], rules: "A deck can have up to nine cards named Nazgûl.");
        var cards = new List<DeckCard>
        {
            CreateDeckCard(CreateMtgCard("Swamp", "Basic Land — Swamp", ["B"]), 30),
            CreateDeckCard(rats, 21),
            CreateDeckCard(nazgul, 9)
        };

        var validResult = _service.Validate("Constructed", cards);
        Assert.True(validResult.IsValid);

        cards[2].QuantityInDeck = 10;
        var invalidResult = _service.Validate("Constructed", cards);
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Violations, v => v.Contains("Nazgûl") && v.Contains("9 copies"));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(105, true)]
    public void Validate_CommanderDeckSize_EnforcesMinimumOneHundredIncludingCommander(int totalCount, bool expectedValid)
    {
        var commander = CreateMtgCard("Krenko, Mob Boss", "Legendary Creature — Goblin Warrior", ["R"]);
        var mountain = CreateMtgCard("Mountain", "Basic Land — Mountain", ["R"]);
        var cards = new List<DeckCard>
        {
            CreateDeckCard(commander, 1, isCommander: true),
            CreateDeckCard(mountain, totalCount - 1)
        };

        var result = _service.Validate("Commander", cards);

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(totalCount, result.MainDeckCount);
    }

    [Fact]
    public void Validate_CommanderEligibility_AllowsPlaneswalkerWithCanBeYourCommander()
    {
        var teferi = CreateMtgCard(
            "Teferi, Temporal Archmage",
            "Legendary Planeswalker — Teferi",
            ["U"],
            rules: "Teferi, Temporal Archmage can be your commander.");
        var island = CreateMtgCard("Island", "Basic Land — Island", ["U"]);
        var cards = new List<DeckCard>
        {
            CreateDeckCard(teferi, 1, isCommander: true),
            CreateDeckCard(island, 99)
        };

        var result = _service.Validate("Commander", cards);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CommanderEligibility_RejectsNonLegendaryCreatureWithoutRule()
    {
        var bear = CreateMtgCard("Grizzly Bears", "Creature — Bear", ["G"]);
        var forest = CreateMtgCard("Forest", "Basic Land — Forest", ["G"]);
        var cards = new List<DeckCard>
        {
            CreateDeckCard(bear, 1, isCommander: true),
            CreateDeckCard(forest, 99)
        };

        var result = _service.Validate("Commander", cards);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("not eligible to be a Commander"));
    }

    [Fact]
    public void Validate_CommanderColorIdentity_RejectsCardsOutsideCommanderIdentity()
    {
        var commander = CreateMtgCard("Prossh, Skyraider of Kher", "Legendary Creature — Dragon", ["B", "R", "G"]);
        var offColor = CreateMtgCard("Atraxa, Praetors' Voice", "Legendary Creature — Phyrexian Angel Horror", ["W", "U", "B", "G"]);
        var colorless = CreateMtgCard("Sol Ring", "Artifact", []);
        var forest = CreateMtgCard("Forest", "Basic Land — Forest", ["G"]);

        var cards = new List<DeckCard>
        {
            CreateDeckCard(commander, 1, isCommander: true),
            CreateDeckCard(offColor, 1),
            CreateDeckCard(colorless, 1),
            CreateDeckCard(forest, 97)
        };

        var result = _service.Validate("Commander", cards);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("Atraxa") && v.Contains("outside the Commander's color identity"));
        Assert.DoesNotContain(result.Violations, v => v.Contains("Sol Ring"));
    }

    [Fact]
    public void Validate_CommanderSingleton_RejectsDuplicateNonBasicCards()
    {
        var commander = CreateMtgCard("Krenko, Mob Boss", "Legendary Creature — Goblin Warrior", ["R"]);
        var solRing = CreateMtgCard("Sol Ring", "Artifact", []);
        var mountain = CreateMtgCard("Mountain", "Basic Land — Mountain", ["R"]);

        var cards = new List<DeckCard>
        {
            CreateDeckCard(commander, 1, isCommander: true),
            CreateDeckCard(solRing, 2),
            CreateDeckCard(mountain, 97)
        };

        var result = _service.Validate("Commander", cards);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("Sol Ring") && v.Contains("1 copy"));
    }

    private static Card CreateMtgCard(
        string name,
        string typeLine,
        IReadOnlyList<string> colorIdentity,
        string? rules = null) => new()
        {
            Name = name,
            Rules = rules,
            ExtraCardDataJson = JsonSerializer.Serialize(new { typeLine, colorIdentity })
        };

    private static DeckCard CreateDeckCard(
        Card card,
        int quantityInDeck,
        int quantitySideboard = 0,
        bool isCommander = false) => new()
        {
            QuantityInDeck = quantityInDeck,
            QuantitySideboard = quantitySideboard,
            IsCommander = isCommander,
            CardPrinting = new CardPrinting { Card = card, Number = "1" }
        };
}
