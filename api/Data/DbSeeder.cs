using api.Data;
using api.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace api.Data // Update to match your folder/namespace
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Skip if already seeded
            if (context.Cards.Any()) return;

            // Seed Users
            var user1 = new User { Username = "Jason", DisplayName = "Xercius", IsAdmin = true };
            var user2 = new User { Username = "Grayson", DisplayName = "Astroracer", IsAdmin = true };
            var user3 = new User { Username = "Perrin", DisplayName = "DinoRoar", IsAdmin = true };

            context.Users.AddRange(user1, user2, user3);
            context.SaveChanges();

            // Seed Cards
            var card1 = new Card { Name = "Disabling Fang Fighter", Game = "Star Wars Unlimited", CardType = "Unit" };
            var card2 = new Card { Name = "Shin Hati, Overeager Apprentice", Game = "Star Wars Unlimited", CardType = "Unit" };
            var card3 = new Card { Name = "Pikachu", Game = "Pokemon TCG", CardType = "Pokemon" };
            var card4 = new Card { Name = "Parallel Lives", Game = "Magic: The Gathering", CardType = "Enchantment" };
            var card5 = new Card { Name = "Darth Maul, Revenge At Last", Game = "Star Wars Unlimited", CardType = "Unit" };
            var card6 = new Card { Name = "Maul, Shadow Collective Visionary", Game = "Star Wars Unlimited", CardType = "Unit" };
            var card7 = new Card { Name = "Darth Maul's Lightsaber", Game = "Star Wars Unlimited", CardType = "Upgrade" };
            var card8 = new Card { Name = "Battle Fury", Game = "Star Wars Unlimited", CardType = "Upgrade" };
            var card9 = new Card { Name = "Shadowed Intentions", Game = "Star Wars Unlimited", CardType = "Upgrade" };
            var card10 = new Card { Name = "Ataru Onslaught", Game = "Star Wars Unlimited", CardType = "Event" };
            var card11 = new Card { Name = "Calm in the Storm", Game = "Star Wars Unlimited", CardType = "Event" };
            var card12 = new Card { Name = "In the Shadows", Game = "Star Wars Unlimited", CardType = "Event" };
            var card13 = new Card { Name = "Unleash Rage", Game = "Star Wars Unlimited", CardType = "Event" };
            var card14 = new Card { Name = "Face Off", Game = "Star Wars Unlimited", CardType = "Event" };
            var card15 = new Card { Name = "Unnatural Life", Game = "Star Wars Unlimited", CardType = "Event" };
            var card16 = new Card { Name = "Baylan Skoll, Enigmatic Master", Game = "Star Wars Unlimited", CardType = "Unit" };
            context.Cards.AddRange(card1, card2, card3, card4, card5, card6, card7, card8, card9, card10, card11, card12, card13, card14, card15, card16);

            context.SaveChanges(); // Save to get IDs for relationships

            // Seed CardPrintings
            var printing1a = new CardPrinting { CardId = card1.CardId, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Style = "Standard" };
            var printing1b = new CardPrinting { CardId = card1.CardId, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Style = "Standard Foil" };
            var printing1c = new CardPrinting { CardId = card1.CardId, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Style = "Hyperspace" };
            var printing1d = new CardPrinting { CardId = card1.CardId, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing1e = new CardPrinting { CardId = card1.CardId, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Style = "Standard" };
            var printing1f = new CardPrinting { CardId = card1.CardId, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Style = "Standard Foil" };
            var printing1g = new CardPrinting { CardId = card1.CardId, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Style = "Hyperspace" };
            var printing1h = new CardPrinting { CardId = card1.CardId, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing2a = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "183", Rarity = "Uncommon", Style = "Standard" };
            var printing2b = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "685", Rarity = "Uncommon", Style = "Standard Foil" };
            var printing2c = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "447", Rarity = "Uncommon", Style = "Hyperspace" };
            var printing2d = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "923", Rarity = "Uncommon", Style = "Hyperspace Foil" };
            var printing2e = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "1066", Rarity = "Uncommon", Style = "Standard Prestige" };
            var printing2f = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "1112", Rarity = "Uncommon", Style = "Foil Prestige" };
            var printing2g = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force", Number = "1158", Rarity = "Uncommon", Style = "Serialized Prestige" };
            var printing2h = new CardPrinting { CardId = card2.CardId, Set = "2025 Promo", Number = "70", Rarity = "Uncommon", Style = "GC Prize Wall" };
            var printing2i = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force Weekly Play", Number = "13", Rarity = "Uncommon", Style = "Weekly Play" };
            var printing2j = new CardPrinting { CardId = card2.CardId, Set = "Legends of the Force Weekly Play", Number = "33", Rarity = "Uncommon", Style = "Weekly Play Foil" };
            var printing3a = new CardPrinting { CardId = card3.CardId, Set = "Mega Evolution", Number = "58", Rarity = "Common", Style = "Standard" };
            var printing4a = new CardPrinting { CardId = card4.CardId, Set = "Marvel Eternal-Legal", Number = "36", Rarity = "Mythic", Style = "Borderless" };
            var printing5a = new CardPrinting { CardId = card5.CardId, Set = "Twilight of the Republic", Number = "135", Rarity = "Legendary", Style = "Standard" };
            var printing5b = new CardPrinting { CardId = card5.CardId, Set = "Twilight of the Republic", Number = "135", Rarity = "Legendary", Style = "Standard Foil" };
            var printing5c = new CardPrinting { CardId = card5.CardId, Set = "Twilight of the Republic", Number = "403", Rarity = "Legendary", Style = "Hyperspace" };
            var printing5d = new CardPrinting { CardId = card5.CardId, Set = "Twilight of the Republic", Number = "403", Rarity = "Legendary", Style = "Hyperspace Foil" };
            var printing5e = new CardPrinting { CardId = card5.CardId, Set = "2024 Convention Exclusive", Number = "6", Rarity = "Special", Style = "Convention Exclusive" };
            var printing5f = new CardPrinting { CardId = card5.CardId, Set = "2025 Judge Program", Number = "5", Rarity = "Legendary", Style = "Judge Program" };
            var printing5g = new CardPrinting { CardId = card5.CardId, Set = "2025 Promo", Number = "91", Rarity = "Legendary", Style = "GC Event Pack" };
            var printing6a = new CardPrinting { CardId = card6.CardId, Set = "Shadows of the Galaxy", Number = "90", Rarity = "Rare", Style = "Standard" };
            var printing6b = new CardPrinting { CardId = card6.CardId, Set = "Shadows of the Galaxy", Number = "90", Rarity = "Rare", Style = "Standard Foil" };
            var printing6c = new CardPrinting { CardId = card6.CardId, Set = "Shadows of the Galaxy", Number = "359", Rarity = "Rare", Style = "Hyperspace" };
            var printing6d = new CardPrinting { CardId = card6.CardId, Set = "Shadows of the Galaxy", Number = "359", Rarity = "Rare", Style = "Hyperspace Foil" };
            var printing6e = new CardPrinting { CardId = card6.CardId, Set = "2024 Judge Program", Number = "4", Rarity = "Rare", Style = "Judge Program" };
            var printing6f = new CardPrinting { CardId = card6.CardId, Set = "2025 Promo", Number = "155", Rarity = "Rare", Style = "RQ Prize Wall" };
            var printing6g = new CardPrinting { CardId = card6.CardId, Set = "2025 Promo", Number = "165", Rarity = "Rare", Style = "RQ Event Pack" };
            var printing7a = new CardPrinting { CardId = card7.CardId, Set = "Legends of the Force", Number = "140", Rarity = "Special", Style = "Standard" };
            var printing7b = new CardPrinting { CardId = card7.CardId, Set = "Legends of the Force", Number = "642", Rarity = "Special", Style = "Standard Foil" };
            var printing7c = new CardPrinting { CardId = card7.CardId, Set = "Legends of the Force", Number = "404", Rarity = "Special", Style = "Hyperspace" };
            var printing7d = new CardPrinting { CardId = card7.CardId, Set = "Legends of the Force", Number = "880", Rarity = "Special", Style = "Hyperspace Foil" };
            var printing7e = new CardPrinting { CardId = card7.CardId, Set = "2025 Promo", Number = "108", Rarity = "Special", Style = "GC Top 64" };
            var printing8a = new CardPrinting { CardId = card8.CardId, Set = "Legends of the Force", Number = "139", Rarity = "Uncommon", Style = "Standard" };
            var printing8b = new CardPrinting { CardId = card8.CardId, Set = "Legends of the Force", Number = "641", Rarity = "Uncommon", Style = "Standard Foil" };
            var printing8c = new CardPrinting { CardId = card8.CardId, Set = "Legends of the Force", Number = "403", Rarity = "Uncommon", Style = "Hyperspace" };
            var printing8d = new CardPrinting { CardId = card8.CardId, Set = "Legends of the Force", Number = "879", Rarity = "Uncommon", Style = "Hyperspace Foil" };
            var printing9a = new CardPrinting { CardId = card9.CardId, Set = "Twilight of the Republic", Number = "220", Rarity = "Rare", Style = "Standard" };
            var printing9b = new CardPrinting { CardId = card9.CardId, Set = "Twilight of the Republic", Number = "220", Rarity = "Rare", Style = "Standard Foil" };
            var printing9c = new CardPrinting { CardId = card9.CardId, Set = "Twilight of the Republic", Number = "485", Rarity = "Rare", Style = "Hyperspace" };
            var printing9d = new CardPrinting { CardId = card9.CardId, Set = "Twilight of the Republic", Number = "485", Rarity = "Rare", Style = "Hyperspace Foil" };
            var printing10a = new CardPrinting { CardId = card10.CardId, Set = "Legends of the Force", Number = "174", Rarity = "Uncommon", Style = "Standard" };
            var printing10b = new CardPrinting { CardId = card10.CardId, Set = "Legends of the Force", Number = "676", Rarity = "Uncommon", Style = "Standard Foil" };
            var printing10c = new CardPrinting { CardId = card10.CardId, Set = "Legends of the Force", Number = "438", Rarity = "Uncommon", Style = "Hyperspace" };
            var printing10d = new CardPrinting { CardId = card10.CardId, Set = "Legends of the Force", Number = "914", Rarity = "Uncommon", Style = "Hyperspace Foil" };
            var printing11a = new CardPrinting { CardId = card11.CardId, Set = "Legends of the Force", Number = "54", Rarity = "Uncommon", Style = "Standard" };
            var printing11b = new CardPrinting { CardId = card11.CardId, Set = "Legends of the Force", Number = "556", Rarity = "Uncommon", Style = "Standard Foil" };
            var printing11c = new CardPrinting { CardId = card11.CardId, Set = "Legends of the Force", Number = "318", Rarity = "Uncommon", Style = "Hyperspace" };
            var printing11d = new CardPrinting { CardId = card11.CardId, Set = "Legends of the Force", Number = "794", Rarity = "Uncommon", Style = "Hyperspace Foil" };
            var printing12a = new CardPrinting { CardId = card12.CardId, Set = "Legends of the Force", Number = "171", Rarity = "Rare", Style = "Standard" };
            var printing12b = new CardPrinting { CardId = card12.CardId, Set = "Legends of the Force", Number = "673", Rarity = "Rare", Style = "Standard Foil" };
            var printing12c = new CardPrinting { CardId = card12.CardId, Set = "Legends of the Force", Number = "435", Rarity = "Rare", Style = "Hyperspace" };
            var printing12d = new CardPrinting { CardId = card12.CardId, Set = "Legends of the Force", Number = "911", Rarity = "Rare", Style = "Hyperspace Foil" };
            var printing13a = new CardPrinting { CardId = card13.CardId, Set = "Legends of the Force", Number = "173", Rarity = "Common", Style = "Standard" };
            var printing13b = new CardPrinting { CardId = card13.CardId, Set = "Legends of the Force", Number = "675", Rarity = "Common", Style = "Standard Foil" };
            var printing13c = new CardPrinting { CardId = card13.CardId, Set = "Legends of the Force", Number = "437", Rarity = "Common", Style = "Hyperspace" };
            var printing13d = new CardPrinting { CardId = card13.CardId, Set = "Legends of the Force", Number = "913", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing14a = new CardPrinting { CardId = card14.CardId, Set = "Legends of the Force", Number = "172", Rarity = "Common", Style = "Standard" };
            var printing14b = new CardPrinting { CardId = card14.CardId, Set = "Legends of the Force", Number = "674", Rarity = "Common", Style = "Standard Foil" };
            var printing14c = new CardPrinting { CardId = card14.CardId, Set = "Legends of the Force", Number = "436", Rarity = "Common", Style = "Hyperspace" };
            var printing14d = new CardPrinting { CardId = card14.CardId, Set = "Legends of the Force", Number = "912", Rarity = "Common", Style = "Hyperspace Foil" };
            var printing15a = new CardPrinting { CardId = card15.CardId, Set = "Legends of the Force", Number = "175", Rarity = "Rare", Style = "Standard" };
            var printing15b = new CardPrinting { CardId = card15.CardId, Set = "Legends of the Force", Number = "677", Rarity = "Rare", Style = "Standard Foil" };
            var printing15c = new CardPrinting { CardId = card15.CardId, Set = "Legends of the Force", Number = "439", Rarity = "Rare", Style = "Hyperspace" };
            var printing15d = new CardPrinting { CardId = card15.CardId, Set = "Legends of the Force", Number = "915", Rarity = "Rare", Style = "Hyperspace Foil" };
            var printing16a = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "121", Rarity = "Legendary", Style = "Standard" };
            var printing16b = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "121", Rarity = "Legendary", Style = "Standard Foil" };
            var printing16c = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "357", Rarity = "Legendary", Style = "Hyperspace" };
            var printing16d = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "357", Rarity = "Legendary", Style = "Hyperspace Foil" };
            var printing16e = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "1065", Rarity = "Legendary", Style = "Standard Prestige" };
            var printing16f = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "1111", Rarity = "Legendary", Style = "Foil Prestige" };
            var printing16g = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force", Number = "1157", Rarity = "Legendary", Style = "Serialized Prestige" };
            var printing16h = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force Weekly Play", Number = "69", Rarity = "Legendary", Style = "Weekly Play" };
            var printing16i = new CardPrinting { CardId = card16.CardId, Set = "Legends of the Force Weekly Play", Number = "89", Rarity = "Legendary", Style = "Weekly Play Foil" };
            context.CardPrintings.AddRange(printing1a, printing1b, printing1c, printing1d, printing1e, printing1f, printing1g, printing1h, printing2a, printing2b, printing2c, printing2d, printing2e, printing2f, printing2g, printing2h, printing2i, printing2j, printing3a, printing4a, printing5a, printing5b, printing5c, printing5d, printing5e, printing5f, printing5g, printing6a, printing6b, printing6c, printing6d, printing6e, printing6f, printing6g, printing7a, printing7b, printing7c, printing7d, printing7e, printing8a, printing8b, printing8c, printing8d, printing9a, printing9b, printing9c, printing9d, printing10a, printing10b, printing10c, printing10d, printing11a, printing11b, printing11c, printing11d, printing12a, printing12b, printing12c, printing12d, printing13a, printing13b, printing13c, printing13d, printing14a, printing14b, printing14c, printing14d, printing15a, printing15b, printing15c, printing15d, printing16a, printing16b, printing16c, printing16d, printing16e, printing16f, printing16g,printing16h ,printing16i);

            context.SaveChanges();

            // Seed UserCards (collection data)
            var userCard1a = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1a.Id,
                QuantityOwned = 8,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1b = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1b.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1c = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1c.Id,
                QuantityOwned = 0,
                QuantityWanted = 1,
                QuantityProxyOwned = 1
            };
            var userCard1d = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1d.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1e = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1e.Id,
                QuantityOwned = 9,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1f = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1f.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1g = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1g.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1h = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing1h.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2a = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2a.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2b = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2b.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2c = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2c.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2d = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2d.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2e = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2e.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2f = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2f.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2g = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2g.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2h = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2h.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2i = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2i.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2j = new UserCard
            {
                UserId = user2.Id,
                CardPrintingId = printing2j.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard3a = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing3a.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard4a = new UserCard
            {
                UserId = user1.Id,
                CardPrintingId = printing4a.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            context.UserCards.AddRange(userCard1a, userCard1b, userCard1c, userCard1d, userCard1e, userCard1f, userCard1g, userCard1h, userCard2a, userCard2b, userCard2c, userCard2d, userCard2e, userCard2f, userCard2g, userCard2h, userCard2i, userCard2j, userCard3a, userCard4a);

            context.SaveChanges();

            // Seed Decks
            var deck1 = new Deck { UserId = user2.Id, Name = "Grayson's Deck", Description = "A sample deck for Grayson.", Game = "Star Wars Unlimited" };
            var deck2 = new Deck { UserId = user3.Id, Name = "Perrin's Deck", Description = "A sample deck for Perrin.", Game = "Star Wars Unlimited" };
            context.Decks.AddRange(deck1, deck2);
            context.SaveChanges(); // Save to get IDs for relationships

            // Seed DeckCards
            Debug.Assert(deck1.Id != 0, "Deck 1 must have a persisted key before seeding deck cards.");
            Debug.Assert(deck2.Id != 0, "Deck 2 must have a persisted key before seeding deck cards.");
            Debug.Assert(printing1a.Id != 0 && printing1b.Id != 0 && printing2a.Id != 0 && printing2b.Id != 0, "Card printing keys must be generated before deck cards are added.");
            Debug.Assert(context.Decks.Any(d => d.Id == deck1.Id), "Deck 1 could not be found in the database prior to seeding deck cards.");
            Debug.Assert(context.Decks.Any(d => d.Id == deck2.Id), "Deck 2 could not be found in the database prior to seeding deck cards.");
            Debug.Assert(context.CardPrintings.Any(p => p.Id == printing1a.Id), "Printing 1A is missing before deck cards are added.");
            Debug.Assert(context.CardPrintings.Any(p => p.Id == printing1b.Id), "Printing 1B is missing before deck cards are added.");
            Debug.Assert(context.CardPrintings.Any(p => p.Id == printing2a.Id), "Printing 2A is missing before deck cards are added.");
            Debug.Assert(context.CardPrintings.Any(p => p.Id == printing2b.Id), "Printing 2B is missing before deck cards are added.");

            Console.WriteLine($"Seeding DeckCards for Decks {deck1.Id} and {deck2.Id}.");

            var deck1Card1 = new DeckCard { Deck = deck1, CardPrinting = printing1a, QuantityInDeck = 2, QuantityIdea = 1, QuantityAcquire = 1, QuantityProxy = 0 };
            var deck1Card2 = new DeckCard { Deck = deck1, CardPrinting = printing1b, QuantityInDeck = 1, QuantityIdea = 0, QuantityAcquire = 0, QuantityProxy = 0 };
            var deck2Card1 = new DeckCard { Deck = deck2, CardPrinting = printing2a, QuantityInDeck = 3, QuantityIdea = 3, QuantityAcquire = 0, QuantityProxy = 0 };
            var deck2Card2 = new DeckCard { Deck = deck2, CardPrinting = printing2b, QuantityInDeck = 2, QuantityIdea = 1, QuantityAcquire = 2, QuantityProxy = 0 };
            context.DeckCards.AddRange(deck1Card1, deck1Card2, deck2Card1, deck2Card2);
            context.SaveChanges();
        }
    }
}
