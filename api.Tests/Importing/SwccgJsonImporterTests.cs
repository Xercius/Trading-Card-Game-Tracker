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
        [
          {
            "id": 634,
            "gempId": "1_168",
            "side": "Dark",
            "rarity": "R1",
            "set": "Premiere",
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
              "icons": ["Pilot", "Warrior"],
              "characteristics": ["Leader", "Dark Jedi"]
            }
          }
        ]
        """;

        var importer = CreateImporter(db);
        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false));

        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.Errors);

        var card = await db.Cards
            .Include(c => c.Printings)
            .Include(c => c.CardTypes).ThenInclude(ct => ct.CardType)
            .Include(c => c.CardTraits).ThenInclude(ct => ct.Trait)
            .Include(c => c.CardKeywords).ThenInclude(ck => ck.Keyword)
            .Include(c => c.CardAspects).ThenInclude(ca => ca.Aspect)
            .FirstOrDefaultAsync(c => c.CardUid == "1_168");

        Assert.NotNull(card);
        Assert.Equal("•Darth Vader", card.Name);
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

        // Printing checks
        var printing = Assert.Single(card.Printings);
        Assert.Equal("168", printing.Number);
        Assert.Equal("https://res.starwarsccg.org/cards/Premiere-Dark/large/darthvader.gif", printing.ImageUrl);
        Assert.False(printing.ArtFrontHorizontal);

        // Associations
        Assert.Contains(card.CardTypes, t => t.CardType.Name == "Character" && t.SourceRole == "Type");
        Assert.Contains(card.CardTypes, t => t.CardType.Name == "Imperial" && t.SourceRole == "Type2");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Leader");
        Assert.Contains(card.CardTraits, tr => tr.Trait.Name == "Dark Jedi");
        Assert.Contains(card.CardKeywords, kw => kw.Keyword.Name == "Pilot");
        Assert.Contains(card.CardKeywords, kw => kw.Keyword.Name == "Warrior");
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

        var cards = await db.Cards.Where(c => c.Name == "•Sense").ToListAsync();
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

    private sealed class StubSwccgApiClient : ISwccgApiClient
    {
        public Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgSet>
            {
                new("1", "Premiere"),
                new("7", "Special Edition"),
                new("9", "Death Star II")
            });

        public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());

        public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());

        public Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<SwccgCardRecord>());
    }
}
