using api.Data;
using api.Models;
using System;
using System.Linq;

namespace api.Data // Update to match your folder/namespace
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Ensure DB is created
            context.Database.EnsureCreated();

            // Skip if already seeded
            if (context.Cards.Any()) return;

            // Seed Users
            var user1 = new User { Username = "Grayson" };
            var user2 = new User { Username = "Perrin" };
            context.Users.AddRange(user1, user2);

            // Seed Cards
            var card1 = new Card { Name = "Disabling Fang Fighter", Game = "Star Wars Unlimited", CardType = "Unit" };
            var card2 = new Card { Name = "Shin Hati", Game = "Star Wars Unlimited", CardType = "Unit" };
            context.Cards.AddRange(card1, card2);

            context.SaveChanges(); // Save to get IDs for relationships

            // Seed CardPrintings
            var printing1a = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Style = "Standard" };
            var printing1b = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Style = "Standard Foil" };
            var printing1c = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Style = "Hyperspace" };
            var printing1d = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing1e = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Style = "Standard" };
            var printing1f = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Style = "Standard Foil" };
            var printing1g = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Style = "Hyperspace" };
            var printing1h = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing2a = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "183", Rarity = "Uncommon", Style = "Standard" };
            var printing2b = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "685", Rarity = "Uncommon", Style = "Standard Foil" };
            var printing2c = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "447", Rarity = "Uncommon", Style = "Hyperspace" };
            var printing2d = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "923", Rarity = "Uncommon", Style = "Hyperspace Foil" };
            var printing2e = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1066", Rarity = "Uncommon", Style = "Standard Prestige" };
            var printing2f = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1112", Rarity = "Uncommon", Style = "Foil Prestige" };
            var printing2g = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1158", Rarity = "Uncommon", Style = "Serialized Prestige" };
            var printing2h = new CardPrinting { CardId = card2.Id, Set = "2025 Promo", Number = "70", Rarity = "Uncommon", Style = "GC Prize Wall" };
            var printing2i = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force Weekly Play", Number = "13", Rarity = "Uncommon", Style = "Weekly Play" };
            var printing2j = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force Weekly Play", Number = "33", Rarity = "Uncommon", Style = "Weekly Play Foil" };
            context.CardPrintings.AddRange(printing1a, printing1b, printing1c, printing1d, printing1e, printing1f, printing1g, printing1h, printing2a, printing2b, printing2c, printing2d, printing2e, printing2f, printing2g, printing2h, printing2i, printing2j);

            context.SaveChanges();

            // Seed UserCards (collection data)
            var userCard1a = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1a.Id,
                QuantityOwned = 8,
                QuantityWanted = 0
            };
            var userCard1b = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1b.Id,
                QuantityOwned = 1,
                QuantityWanted = 0
            };
            var userCard1c = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1c.Id,
                QuantityOwned = 0,
                QuantityWanted = 1
            };
            var userCard1d = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1d.Id,
                QuantityOwned = 1,
                QuantityWanted = 0
            };
            var userCard1e = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1e.Id,
                QuantityOwned = 9,
                QuantityWanted = 0
            };
            var userCard1f = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1f.Id,
                QuantityOwned = 2,
                QuantityWanted = 0
            };
            var userCard1g = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1g.Id,
                QuantityOwned = 2,
                QuantityWanted = 0
            };
            var userCard1h = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1h.Id,
                QuantityOwned = 1,
                QuantityWanted = 0
            };
            var userCard2a = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2a.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2b = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2b.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2c = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2c.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2d = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2d.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2e = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2e.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2f = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2f.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2g = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2g.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2h = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2h.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2i = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2i.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            var userCard2j = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2j.Id,
                QuantityOwned = 4,
                QuantityWanted = 0
            };
            context.UserCards.AddRange(userCard1a, userCard1b, userCard1c, userCard1d, userCard1e, userCard1f, userCard1g, userCard1h, userCard2a, userCard2b, userCard2c, userCard2d, userCard2e, userCard2f, userCard2g, userCard2h, userCard2i, userCard2j);

            context.SaveChanges();
        }
    }
}
