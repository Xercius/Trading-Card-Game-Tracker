using api.Data;
using api.Models;
using Microsoft.AspNetCore.Identity;

namespace api.Tests.Infrastructure;

public static class Seed
{
    // Using same IDs as TestDataSeeder for consistency with HttpClientExtensions
    public const int AdminUserId = 999;
    public const int SecondaryUserId = 1; // Alice

    public const int LightningCardId = 200;
    public const int GoblinCardId = 201;
    public const int PhoenixCardId = 202;
    public const int DragonCardId = 203;

    public const int LightningAlphaPrintingId = 3001;
    public const int LightningBetaPrintingId = 3002;
    public const int GoblinPrintingId = 3003;
    public const int PhoenixPrintingId = 3004;
    public const int DragonPrintingId = 3005;

    public const int AdminDeckId = 4001;
    public const int SecondaryDeckId = 5001;

    public static async Task SeedAsync(AppDbContext db)
    {
        var hasher = new PasswordHasher<User>();
        
        var admin = new User
        {
            Id = AdminUserId,
            Username = "admin",
            DisplayName = "Admin",
            IsAdmin = true
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Password123!");

        var userTwo = new User
        {
            Id = SecondaryUserId,
            Username = "alice", // Changed from "user2" to match HttpClientExtensions
            DisplayName = "Alice", // Changed from "User Two"
            IsAdmin = false
        };
        userTwo.PasswordHash = hasher.HashPassword(userTwo, "Password123!");

        db.Users.AddRange(admin, userTwo);

        var lightning = new Card
        {
            Id = LightningCardId,
            Game = "Magic",
            Name = "Lightning Bolt",
            CardType = "Instant"
        };

        var goblin = new Card
        {
            Id = GoblinCardId,
            Game = "Magic",
            Name = "Goblin Guide",
            CardType = "Creature"
        };

        var phoenix = new Card
        {
            Id = PhoenixCardId,
            Game = "Magic",
            Name = "Flameborn Phoenix",
            CardType = "Creature"
        };

        var dragon = new Card
        {
            Id = DragonCardId,
            Game = "Magic",
            Name = "Shivan Dragon",
            CardType = "Creature"
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
                UserId = AdminUserId,
                CardPrintingId = LightningAlphaPrintingId,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = AdminUserId,
                CardPrintingId = LightningBetaPrintingId,
                QuantityOwned = 3,
                QuantityWanted = 1,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = AdminUserId,
                CardPrintingId = PhoenixPrintingId,
                QuantityOwned = 0,
                QuantityWanted = 2,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = AdminUserId,
                CardPrintingId = GoblinPrintingId,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = SecondaryUserId,
                CardPrintingId = LightningAlphaPrintingId,
                QuantityOwned = 1,
                QuantityWanted = 1,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = SecondaryUserId,
                CardPrintingId = LightningBetaPrintingId,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = SecondaryUserId,
                CardPrintingId = DragonPrintingId,
                QuantityOwned = 2,
                QuantityWanted = 1,
                QuantityProxyOwned = 0
            });

        var adminDeck = new Deck
        {
            Id = AdminDeckId,
            UserId = AdminUserId,
            Game = "Magic",
            Name = "Admin Main",
            Description = "Primary testing deck"
        };

        var userTwoDeck = new Deck
        {
            Id = SecondaryDeckId,
            UserId = SecondaryUserId,
            Game = "Magic",
            Name = "User Two Deck",
            Description = "Secondary deck"
        };

        db.Decks.AddRange(adminDeck, userTwoDeck);

        db.DeckCards.AddRange(
            new DeckCard
            {
                DeckId = AdminDeckId,
                CardPrintingId = LightningBetaPrintingId,
                QuantityInDeck = 2,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 0
            },
            new DeckCard
            {
                DeckId = SecondaryDeckId,
                CardPrintingId = DragonPrintingId,
                QuantityInDeck = 1,
                QuantityIdea = 0,
                QuantityAcquire = 0,
                QuantityProxy = 0
            });

        await db.SaveChangesAsync();
    }
}
