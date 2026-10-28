using api.Data;
using api.Importing;
using api.Models;

namespace api.Tests.Infrastructure;

public static class Seed
{
    public const int UserId = DbSeeder.DefaultUserId;

    public const int LightningCardId = 200;
    public const int GoblinCardId = 201;
    public const int PhoenixCardId = 202;
    public const int DragonCardId = 203;

    public const int LightningAlphaPrintingId = 3001;
    public const int LightningBetaPrintingId = 3002;
    public const int GoblinPrintingId = 3003;
    public const int PhoenixPrintingId = 3004;
    public const int DragonPrintingId = 3005;

    public const int DeckId = 4001;

    // Keep these aliases for tests that use them
    public const int AdminUserId = UserId;
    public const int SecondaryUserId = UserId;
    public const int AdminDeckId = DeckId;
    public const int SecondaryDeckId = DeckId;

    public static async Task SeedAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = UserId,
            Username = "owner",
            DisplayName = "Owner"
        };
        db.Users.Add(user);

        var lightning = new Card
        {
            Id = LightningCardId,
            Game = TreatmentLookup.GetOrAddGame(db, "Magic"),
            Name = "Lightning Bolt",
        };

        var goblin = new Card
        {
            Id = GoblinCardId,
            Game = TreatmentLookup.GetOrAddGame(db, "Magic"),
            Name = "Goblin Guide",
        };

        var phoenix = new Card
        {
            Id = PhoenixCardId,
            Game = TreatmentLookup.GetOrAddGame(db, "Magic"),
            Name = "Flameborn Phoenix",
        };

        var dragon = new Card
        {
            Id = DragonCardId,
            Game = TreatmentLookup.GetOrAddGame(db, "Magic"),
            Name = "Shivan Dragon",
        };

        TreatmentLookup.UpsertCardType(db, lightning, "Instant");
        TreatmentLookup.UpsertCardType(db, goblin, "Creature");
        TreatmentLookup.UpsertCardType(db, phoenix, "Creature");
        TreatmentLookup.UpsertCardType(db, dragon, "Creature");
        db.Cards.AddRange(lightning, goblin, phoenix, dragon);

        db.CardPrintings.AddRange(
            new CardPrinting
            {
                Id = LightningAlphaPrintingId,
                CardId = LightningCardId,
                Set = "Alpha",
                Number = "A1",
                Rarity = "Common",
                Treatment = TreatmentLookup.Upsert(db, "Magic", "Standard"),
                ImageUrl = "https://example.com/lightning-alpha.png"
            },
            new CardPrinting
            {
                Id = LightningBetaPrintingId,
                CardId = LightningCardId,
                Set = "Beta",
                Number = "B2",
                Rarity = "Uncommon",
                Treatment = TreatmentLookup.Upsert(db, "Magic", "Foil"),
                ImageUrl = "https://example.com/lightning-beta.png"
            },
            new CardPrinting
            {
                Id = GoblinPrintingId,
                CardId = GoblinCardId,
                Set = "Zendikar",
                Number = "Z3",
                Rarity = "Rare",
                Treatment = TreatmentLookup.Upsert(db, "Magic", "Standard"),
                ImageUrl = "https://example.com/goblin.png"
            },
            new CardPrinting
            {
                Id = PhoenixPrintingId,
                CardId = PhoenixCardId,
                Set = "Mythic Flames",
                Number = "M4",
                Rarity = "Mythic",
                Treatment = TreatmentLookup.Upsert(db, "Magic", "Standard"),
                ImageUrl = "https://example.com/phoenix.png"
            },
            new CardPrinting
            {
                Id = DragonPrintingId,
                CardId = DragonCardId,
                Set = "Legends",
                Number = "L5",
                Rarity = "Rare",
                Treatment = TreatmentLookup.Upsert(db, "Magic", "Standard"),
                ImageUrl = "https://example.com/dragon.png"
            });

        db.UserCards.AddRange(
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = LightningAlphaPrintingId,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = LightningBetaPrintingId,
                QuantityOwned = 3,
                QuantityWanted = 1,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = PhoenixPrintingId,
                QuantityOwned = 0,
                QuantityWanted = 2,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = UserId,
                CardPrintingId = GoblinPrintingId,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            });

        var deck = new Deck
        {
            Id = DeckId,
            UserId = UserId,
            Game = "Magic",
            Name = "My Deck",
            Description = "Primary testing deck"
        };

        db.Decks.Add(deck);

        db.DeckCards.AddRange(
            new DeckCard
            {
                DeckId = DeckId,
                CardPrintingId = LightningBetaPrintingId,
                QuantityInDeck = 2,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 0
            });

        await db.SaveChangesAsync();
    }
}
