using System.Text;
using api.Data;
using api.Importing;
using api.Importing.Swccg;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Importing;

public sealed class SwccgJsonImporterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{

    private static Stream ToStream(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static SwccgJsonImporter CreateImporter(AppDbContext db, ISwccgApiClient? client = null)
        => new(db, client ?? new StubSwccgApiClient(), NullLogger<SwccgJsonImporter>.Instance);

    [Fact]
    public async Task ImportFromFileAsync_ValidCardWithStatsAndExtraData_PersistsSuccessfully()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        {
          "sets": [
            {
              "id": "1",
              "name": "Premiere",
              "gempName": "Premiere",
              "abbr": "P",
              "date_release": "1995-12-01"
            }
          ],
          "cards": [
            {
              "id": 634,
              "gempId": "1_168",
              "side": "Dark",
              "rarity": "R1",
              "set": "1",
              "abbr": ["Math Vader", "Space Vader"],
              "combo": ["Darth Vader + Grand Moff Tarkin: Once per battle..."],
              "matching": ["Vader's Custom TIE", "Vader's Personal Shuttle"],
              "matchingWeapon": ["Darth Vader's Lightsaber", "Vader's Lightsaber"],
              "pulledBy": ["Blizzard 4", "The Empire's Back"],
              "underlyingCardFor": ["Darth Vader (V)"],
              "personas": ["Vader"],
              "rulings": ["This card is a Black Squadron pilot."],
              "misc": "Character: Imperial",
              "front": {
                "title": "•Darth Vader",
                "type": "Character",
                "subType": "Imperial",
                "uniqueness": "*",
                "destiny": "1",
                "deploy": "6",
                "forfeit": "8",
                "power": "6",
                "ability": "6",
                "armor": "5",
                "gametext": "Adds 3 to power of anything he pilots.",
                "lore": "Dark Lord of the Sith.",
                "imageUrl": "https://res.starwarsccg.org/cards/Premiere-Dark/large/darthvader.gif",
                "icons": ["Pilot", "Warrior", "Premiere", "Set 1", "Virtual Set 2", "Virtual"],
                "characteristics": ["Leader", "Dark Jedi"]
              }
            }
          ]
        }
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.Errors);

        var card = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .Include(c => c.CardTypes).ThenInclude(ct => ct.CardType)
            .Include(c => c.CardSubTypes).ThenInclude(cst => cst.SubType)
            .Include(c => c.CardTraits).ThenInclude(ct => ct.Trait)
            .Include(c => c.CardKeywords).ThenInclude(ck => ck.Keyword)
            .Include(c => c.CardAspects).ThenInclude(ca => ca.Aspect)
            .FirstOrDefaultAsync(c => c.CardUid == "1_168");

        Assert.NotNull(card);
        Assert.Equal("Darth Vader", card.Name);
        Assert.Equal("Dark", card.Side);
        Assert.Equal("*", card.Uniqueness);
        Assert.Equal("1", card.Destiny);
        Assert.Equal("6", card.Deploy);
        Assert.Equal("8", card.Forfeit);
        Assert.Equal(6, card.Power);
        Assert.Equal(6, card.Ability);
        Assert.Equal("5", card.Armor);
        Assert.Equal("Adds 3 to power of anything he pilots.", card.Text);
        Assert.Equal("Dark Lord of the Sith.", card.Lore);
        Assert.Contains("This card is a Black Squadron pilot.", card.RulesStyled);
        Assert.Contains("Pulled By", card.RulesStyled);
        Assert.Contains("Blizzard 4", card.RulesStyled);
        Assert.Contains("Combos", card.RulesStyled);
        Assert.Contains("Matching", card.RulesStyled);
        Assert.Contains("Personas", card.RulesStyled);
        Assert.NotNull(card.ExtraCardDataJson);

        // Printing, Set, and Rarity checks
        var printing = Assert.Single(card.Printings);
        Assert.Equal("168", printing.Number);
        Assert.Equal("https://res.starwarsccg.org/cards/Premiere-Dark/large/darthvader.gif", printing.ImageUrl);
        Assert.False(printing.ArtFrontHorizontal);
        Assert.NotNull(printing.CatalogSet);
        Assert.Equal("P", printing.CatalogSet.Code);
        Assert.Equal("Premiere", printing.CatalogSet.Name);
        Assert.Equal(new DateOnly(1995, 12, 1), printing.CatalogSet.ReleasedAt);
        Assert.NotNull(printing.RarityLookup);
        Assert.Equal("Rare 1", printing.RarityLookup.Name);
        Assert.Equal("R1", printing.RarityLookup.Character);
        Assert.Equal("Rare", printing.RarityLookup.RarityType);

        // Associations
        Assert.Contains(card.CardTypes, t => t.CardType.Name == "Character" && t.SourceRole == "Type");
        Assert.DoesNotContain(card.CardTypes, t => t.CardType.Name == "Imperial");
        Assert.DoesNotContain(card.CardTypes, t => t.SourceRole == "Type2");
        Assert.Contains(card.CardSubTypes, st => st.SubType.Name == "Imperial");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Leader");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Dark Jedi");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Pilot");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Warrior");
        Assert.DoesNotContain(card.CardTraits, tr => tr.Trait.Name == "Premiere");
        Assert.DoesNotContain(card.CardTraits, tr => tr.Trait.Name == "Set 1");
        Assert.DoesNotContain(card.CardTraits, tr => tr.Trait.Name == "Virtual Set 2");
        Assert.DoesNotContain(card.CardTraits, tr => tr.Trait.Name == "Virtual");
        Assert.Empty(card.CardKeywords);
        Assert.Contains(card.CardAspects, a => a.Aspect.Name == "Dark Side");
    }

    [Fact]
    public async Task ImportFromFileAsync_DuplicateCardNamesAcrossSidesAndSets_ImportsWithoutViolation()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 1001,
            "gempId": "1_50",
            "side": "Light",
            "rarity": "U1",
            "set": "Premiere",
            "front": {
              "title": "•Sense",
              "type": "Interrupt",
              "gametext": "Cancel an opponent's Sense."
            }
          },
          {
            "id": 1002,
            "gempId": "1_150",
            "side": "Dark",
            "rarity": "U1",
            "set": "Premiere",
            "front": {
              "title": "•Sense",
              "type": "Interrupt",
              "gametext": "Cancel an opponent's Sense."
            }
          },
          {
            "id": 1003,
            "gempId": "9_50",
            "side": "Light",
            "rarity": "R2",
            "set": "Death Star II",
            "front": {
              "title": "•Sense",
              "type": "Interrupt",
              "gametext": "Death Star II version with completely different gametext."
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(3, summary.CardsCreated);
        Assert.Equal(3, summary.PrintingsCreated);
        Assert.Equal(0, summary.Errors);

        var cards = await db.Cards.Where(c => c.Name == "Sense").ToListAsync();
        Assert.Equal(3, cards.Count);
    }

    [Fact]
    public async Task ImportFromFileAsync_LocationCard_SetsArtFrontHorizontalTrue()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 2001,
            "gempId": "1_200",
            "side": "Dark",
            "rarity": "C",
            "set": "Premiere",
            "front": {
              "title": "Tatooine: Cantina",
              "type": "Location",
              "subType": "Site",
              "parsec": "4",
              "darkSideIcons": 1,
              "lightSideIcons": 2,
              "gametext": "Deploys on Tatooine.",
              "imageUrl": "https://res.starwarsccg.org/cards/Premiere-Dark/large/tatooinecantina.gif"
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(0, summary.Errors);
        var printing = await db.CardPrintings.FirstOrDefaultAsync(p => p.CardUid == "1_200");
        Assert.NotNull(printing);
        Assert.True(printing.ArtFrontHorizontal);

        var card = await db.Cards
            .Include(c => c.CardTypes).ThenInclude(ct => ct.CardType)
            .Include(c => c.CardSubTypes).ThenInclude(cst => cst.SubType)
            .FirstOrDefaultAsync(c => c.CardUid == "1_200");
        Assert.NotNull(card);
        Assert.Contains(card.CardTypes, t => t.CardType.Name == "Location");
        Assert.DoesNotContain(card.CardTypes, t => t.CardType.Name == "Site");
        Assert.Contains(card.CardSubTypes, st => st.SubType.Name == "Site");
        Assert.Equal(1, card.DarkSideIcons);
        Assert.Equal(2, card.LightSideIcons);
    }

    [Fact]
    public async Task ImportFromFileAsync_DualSidedObjectiveCard_PersistsBothFaces()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 3001,
            "gempId": "7_1",
            "side": "Light",
            "rarity": "R",
            "set": "Special Edition",
            "front": {
              "title": "Mind What You Have Learned",
              "type": "Objective",
              "destiny": "0",
              "gametext": "Deploy Dagobah system.",
              "imageUrl": "https://res.starwarsccg.org/cards/SpecialEdition-Light/large/mindwhatyouhavelearned.gif"
            },
            "back": {
              "title": "Save You It Can",
              "destiny": "7",
              "darkSideIcons": 2,
              "lightSideIcons": 3,
              "gametext": "Flip this card if Luke on Dagobah.",
              "imageUrl": "https://res.starwarsccg.org/cards/SpecialEdition-Light/large/saveyouitcan.gif"
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(0, summary.Errors);
        var card = await db.Cards.Include(c => c.Printings).FirstOrDefaultAsync(c => c.CardUid == "7_1");
        Assert.NotNull(card);
        Assert.Equal("Mind What You Have Learned", card.Name);
        Assert.Equal("Save You It Can", card.BackTitle);
        Assert.Equal("Flip this card if Luke on Dagobah.", card.BackText);
        Assert.Equal(2, card.BackDarkSideIcons);
        Assert.Equal(3, card.BackLightSideIcons);

        var printing = Assert.Single(card.Printings);
        Assert.Equal("https://res.starwarsccg.org/cards/SpecialEdition-Light/large/mindwhatyouhavelearned.gif", printing.ImageUrl);
        Assert.Equal("https://res.starwarsccg.org/cards/SpecialEdition-Light/large/saveyouitcan.gif", printing.BackImageUrl);
    }

    [Fact]
    public async Task ImportFromFileAsync_DryRun_DoesNotPersistRecords()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 9999,
            "gempId": "99_99",
            "side": "Dark",
            "rarity": "R",
            "set": "Premiere",
            "front": {
              "title": "Dry Run Test Card",
              "type": "Character",
              "gametext": "Should not be saved."
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: true));

        Assert.Equal(1, summary.CardsCreated);
        Assert.True(summary.DryRun);

        var exists = await db.Cards.AnyAsync(c => c.CardUid == "99_99");
        Assert.False(exists);
    }

    [Fact]
    public async Task ImportFromFileAsync_WithUnderlyingCardForObjects_DeserializesCleanly()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 10,
            "gempId": "3_40",
            "side": "Dark",
            "rarity": "U",
            "set": "Special Edition",
            "underlyingCardFor": [
              { "cardId": 4019, "title": "4-LOM With Concussion Rifle (V)" },
              { "cardId": 7133, "title": "4-LOM With Concussion Rifle (V) (AI)" }
            ],
            "front": {
              "title": "•4-LOM With Concussion Rifle",
              "type": "Character",
              "subType": "Alien",
              "gametext": "A bounty hunter.",
              "imageUrl": "https://res.starwarsccg.org/cards/SpecialEdition-Dark/large/4lomwithconcussionrifle.gif"
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(0, summary.Errors);

        var card = await db.Cards.FirstOrDefaultAsync(c => c.CardUid == "3_40");
        Assert.NotNull(card);
        Assert.Contains("4-LOM With Concussion Rifle (V)", card.RulesStyled);
    }

    [Theory]
    [InlineData("Virtual", true)]
    [InlineData("Virtual Set", true)]
    [InlineData("Virtual Set 0", true)]
    [InlineData("Virtual Set 1", true)]
    [InlineData("Virtual Set 18", true)]
    [InlineData("Virtual Set #2", true)]
    [InlineData("Virtual Premium Set", true)]
    [InlineData("Virtual Defensive Shields", true)]
    [InlineData("Set 0", true)]
    [InlineData("Set 1", true)]
    [InlineData("Set 18", true)]
    [InlineData("Set 27", true)]
    [InlineData("Set P", true)]
    [InlineData("Set D", true)]
    [InlineData("Pilot", false)]
    [InlineData("Warrior", false)]
    [InlineData("Leader", false)]
    [InlineData("Permanent Weapon", false)]
    [InlineData("Nav Computer", false)]
    [InlineData("Scomp Link", false)]
    public void IsVirtualSetName_IdentifiesVirtualSetsAndExcludesRegularTraits(string candidate, bool expected)
    {
        var result = SwccgCatalog.IsVirtualSetName(candidate);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("••Presence Of The Force", "Presence Of The Force", "**")]
    [InlineData("<><><>Asteroid Field", "Asteroid Field", "<><><>")]
    [InlineData("•Darth Vader", "Darth Vader", "*")]
    [InlineData("• Darth Vader", "Darth Vader", "*")]
    [InlineData("•••Luke Skywalker", "Luke Skywalker", "***")]
    [InlineData("<>Tatooine: Cantina", "Tatooine: Cantina", "<>")]
    [InlineData("<><>Death Star", "Death Star", "<><>")]
    [InlineData("Sense", "Sense", null)]
    [InlineData("", "", null)]
    [InlineData(null, "", null)]
    public void ParseTitleUniqueness_CleansTitlesAndExtractsUniqueness(string? input, string expectedTitle, string? expectedUniqueness)
    {
        var (cleanTitle, extractedUniqueness) = SwccgCatalog.ParseTitleUniqueness(input);
        Assert.Equal(expectedTitle, cleanTitle);
        Assert.Equal(expectedUniqueness, extractedUniqueness);
        Assert.Equal(expectedTitle.Length == 0 ? null : expectedTitle, SwccgCatalog.CleanTitle(input));
    }

    [Fact]
    public async Task ImportFromFileAsync_CardsWithDotsAndDiamondsUniqueness_StripsUniquenessFromTitle()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = """
        [
          {
            "id": 9001,
            "gempId": "1_9001",
            "side": "Light",
            "rarity": "R1",
            "set": "Premiere",
            "front": {
              "title": "••Presence Of The Force",
              "uniqueness": "**",
              "type": "Effect",
              "gametext": "Deploy on any location."
            }
          },
          {
            "id": 9002,
            "gempId": "1_9002",
            "side": "Dark",
            "rarity": "U1",
            "set": "Premiere",
            "front": {
              "title": "<><><>Asteroid Field",
              "uniqueness": "<><><>",
              "type": "Sector",
              "gametext": "Asteroid rules in effect."
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(2, summary.CardsCreated);
        Assert.Equal(0, summary.Errors);

        var presenceCard = await db.Cards.FirstOrDefaultAsync(c => c.CardUid == "1_9001");
        Assert.NotNull(presenceCard);
        Assert.Equal("Presence Of The Force", presenceCard.Name);
        Assert.Equal("**", presenceCard.Uniqueness);

        var asteroidCard = await db.Cards.FirstOrDefaultAsync(c => c.CardUid == "1_9002");
        Assert.NotNull(asteroidCard);
        Assert.Equal("Asteroid Field", asteroidCard.Name);
        Assert.Equal("<><><>", asteroidCard.Uniqueness);
    }

    private sealed class StubSwccgApiClient : ISwccgApiClient
    {
        public Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgSet>
            {
                new("1", "Premiere", "P", "1995-12-01", "Premiere"),
                new("7", "Special Edition", "SE", "1998-11-18", "Special Edition"),
                new("9", "Death Star II", "DS2", "2000-07-19", "Death Star II")
            });

        public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());

        public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());

        public Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());
    }
}
