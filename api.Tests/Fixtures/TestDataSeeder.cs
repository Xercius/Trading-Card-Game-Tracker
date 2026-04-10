using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Tests.Fixtures;

public static class TestDataSeeder
{
    public const int UserId = DbSeeder.DefaultUserId;

    // Keep aliases for backward compatibility with tests
    public const int AdminUserId = UserId;
    public const int AliceUserId = UserId;
    public const int BobUserId = UserId;

    public const int LightningBoltCardId = 100;
    public const int GoblinGuideCardId = 101;
    public const int ElsaCardId = 200;
    public const int MickeyCardId = 201;

    public const int LightningBoltAlphaPrintingId = 1001;
    public const int LightningBoltBetaPrintingId = 1002;
    public const int GoblinGuidePrintingId = 1003;
    public const int ElsaPrintingId = 2001;
    public const int MickeyPrintingId = 2002;
    public const int ExtraMagicPrintingId = 3001;

    public const int AliceMagicDeckId = 500;
    public const int AliceLorcanaDeckId = 501;
    public const int AliceEmptyDeckId = 502;
    public const int BobMagicDeckId = 600;

    public static async Task SeedAsync(AppDbContext db)
    {
        await ClearDatabaseAsync(db);

        db.Users.Add(new User { Id = UserId, Username = "owner", DisplayName = "Owner" });

        var lightningBolt = new Card
        {
            Id = LightningBoltCardId,
            Game = "Magic",
            Name = "Lightning Bolt",
            CardType = "Spell",
            Description = "Deal 3 damage to any target",
            Arena = ""
        };

        var goblinGuide = new Card
        {
            Id = GoblinGuideCardId,
            Game = "Magic",
            Name = "Goblin Guide",
            CardType = "Creature",
            Description = "Fast and furious",
            Arena = ""
        };

        var elsa = new Card
        {
            Id = ElsaCardId,
            Game = "Lorcana",
            Name = "Elsa, Ice Sculptor",
            CardType = "Character",
            Description = "Freezes opponents",
            Arena = ""
        };

        var mickey = new Card
        {
            Id = MickeyCardId,
            Game = "Lorcana",
            Name = "Mickey, Brave Tailor",
            CardType = "Character",
            Description = "Sews victory",
            Arena = ""
        };

        db.Cards.AddRange(lightningBolt, goblinGuide, elsa, mickey);

        db.CardPrintings.AddRange(
            new CardPrinting
            {
                Id = LightningBoltAlphaPrintingId,
                CardId = LightningBoltCardId,
                Set = "Alpha",
                Number = "A1",
                Rarity = "Common",
                Style = "Standard",
                ImageUrl = "https://img.example.com/bolt-alpha.png"
            },
            new CardPrinting
            {
                Id = LightningBoltBetaPrintingId,
                CardId = LightningBoltCardId,
                Set = "Beta",
                Number = "B2",
                Rarity = "Uncommon",
                Style = "Foil",
                ImageUrl = "https://img.example.com/bolt-beta.png"
            },
            new CardPrinting
            {
                Id = GoblinGuidePrintingId,
                CardId = GoblinGuideCardId,
                Set = "Zendikar",
                Number = "Z3",
                Rarity = "Rare",
                Style = "Standard",
                ImageUrl = "https://img.example.com/goblin-guide.png"
            },
            new CardPrinting
            {
                Id = ElsaPrintingId,
                CardId = ElsaCardId,
                Set = "Rise of the Floodborn",
                Number = "R1",
                Rarity = "Legendary",
                Style = "Standard",
                ImageUrl = "https://img.example.com/elsa.png"
            },
            new CardPrinting
            {
                Id = MickeyPrintingId,
                CardId = MickeyCardId,
                Set = "Spark of Imagination",
                Number = "S1",
                Rarity = "Rare",
                Style = "Standard",
                ImageUrl = "https://img.example.com/mickey.png"
            },
            new CardPrinting
            {
                Id = ExtraMagicPrintingId,
                CardId = LightningBoltCardId,
                Set = "Collectors",
                Number = "C4",
                Rarity = "Mythic",
                Style = "Borderless",
                ImageUrl = "https://img.example.com/bolt-collectors.png"
            }
        );

        db.UserCards.AddRange(
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = LightningBoltAlphaPrintingId,
                QuantityOwned = 5,
                QuantityWanted = 1,
                QuantityProxyOwned = 1
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = LightningBoltBetaPrintingId,
                QuantityOwned = 0,
                QuantityWanted = 2,
                QuantityProxyOwned = 2
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = ElsaPrintingId,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = GoblinGuidePrintingId,
                QuantityOwned = 2,
                QuantityWanted = 1,
                QuantityProxyOwned = 1
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = MickeyPrintingId,
                QuantityOwned = 0,
                QuantityWanted = 3,
                QuantityProxyOwned = 0
            }
        );

        var magicDeck = new Deck
        {
            Id = AliceMagicDeckId,
            UserId = UserId,
            Game = "Magic",
            Name = "Alice Aggro",
            Description = "Fast red spells"
        };
        var lorcanaDeck = new Deck
        {
            Id = AliceLorcanaDeckId,
            UserId = UserId,
            Game = "Lorcana",
            Name = "Alice Control",
            Description = "Frosty defenses"
        };
        var emptyDeck = new Deck
        {
            Id = AliceEmptyDeckId,
            UserId = UserId,
            Game = "Magic",
            Name = "Alice Empty",
            Description = "Testing deck with no cards"
        };
        var burnDeck = new Deck
        {
            Id = BobMagicDeckId,
            UserId = UserId,
            Game = "Magic",
            Name = "Bob Burn",
            Description = "Lots of fire"
        };

        db.Decks.AddRange(magicDeck, lorcanaDeck, emptyDeck, burnDeck);

        db.DeckCards.AddRange(
            new DeckCard
            {
                DeckId = AliceMagicDeckId,
                CardPrintingId = LightningBoltAlphaPrintingId,
                QuantityInDeck = 4,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 0
            },
            new DeckCard
            {
                DeckId = AliceMagicDeckId,
                CardPrintingId = LightningBoltBetaPrintingId,
                QuantityInDeck = 1,
                QuantityIdea = 2,
                QuantityAcquire = 1,
                QuantityProxy = 1
            },
            new DeckCard
            {
                DeckId = AliceLorcanaDeckId,
                CardPrintingId = ElsaPrintingId,
                QuantityInDeck = 2,
                QuantityIdea = 1,
                QuantityAcquire = 0,
                QuantityProxy = 1
            },
            new DeckCard
            {
                DeckId = BobMagicDeckId,
                CardPrintingId = GoblinGuidePrintingId,
                QuantityInDeck = 3,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 1
            }
        );

        await db.SaveChangesAsync();
    }

    private static async Task ClearDatabaseAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

        foreach (var et in db.Model.GetEntityTypes())
        {
            if (et.IsOwned() || et.GetTableName() is null) continue;

            var set = db.GetType()
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(et.ClrType)
                .Invoke(db, null)!;

            var queryable = (IQueryable<object>)set;
            await queryable.ExecuteDeleteAsync();
        }

        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }
}
