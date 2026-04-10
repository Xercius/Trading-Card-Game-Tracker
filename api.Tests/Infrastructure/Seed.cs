using api.Data;
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
            Game = "Magic",
            Name = "Lightning Bolt",
            CardType = "Instant",
            JasonsCardId = 200,
            Arena = "N/A",
            Unique = false,
            Cost = 1,
            Hp = 0,
            Power = 0
        };

        var goblin = new Card
        {
            Id = GoblinCardId,
            Game = "Magic",
            Name = "Goblin Guide",
            CardType = "Creature",
            JasonsCardId = 201,
            Arena = "N/A",
            Unique = false,
            Cost = 1,
            Hp = 2,
            Power = 2
        };

        var phoenix = new Card
        {
            Id = PhoenixCardId,
            Game = "Magic",
            Name = "Flameborn Phoenix",
            CardType = "Creature",
            JasonsCardId = 202,
            Arena = "N/A",
            Unique = false,
            Cost = 4,
            Hp = 2,
            Power = 2
        };

        var dragon = new Card
        {
            Id = DragonCardId,
            Game = "Magic",
            Name = "Shivan Dragon",
            CardType = "Creature",
            JasonsCardId = 203,
            Arena = "N/A",
            Unique = false,
            Cost = 6,
            Hp = 5,
            Power = 5
        };

        db.Cards.AddRange(lightning, goblin, phoenix, dragon);

        db.CardPrintings.AddRange(
            new CardPrinting
            {
                Id = LightningAlphaPrintingId,
                CardId = LightningCardId,
                Set = "Alpha",
                Number = "A1",
                Rarity = "Common",
                Style = "Standard",
                ImageUrl = "https://example.com/lightning-alpha.png"
            },
            new CardPrinting
            {
                Id = LightningBetaPrintingId,
                CardId = LightningCardId,
                Set = "Beta",
                Number = "B2",
                Rarity = "Uncommon",
                Style = "Foil",
                ImageUrl = "https://example.com/lightning-beta.png"
            },
            new CardPrinting
            {
                Id = GoblinPrintingId,
                CardId = GoblinCardId,
                Set = "Zendikar",
                Number = "Z3",
                Rarity = "Rare",
                Style = "Standard",
                ImageUrl = "https://example.com/goblin.png"
            },
            new CardPrinting
            {
                Id = PhoenixPrintingId,
                CardId = PhoenixCardId,
                Set = "Mythic Flames",
                Number = "M4",
                Rarity = "Mythic",
                Style = "Standard",
                ImageUrl = "https://example.com/phoenix.png"
            },
            new CardPrinting
            {
                Id = DragonPrintingId,
                CardId = DragonCardId,
                Set = "Legends",
                Number = "L5",
                Rarity = "Rare",
                Style = "Standard",
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
