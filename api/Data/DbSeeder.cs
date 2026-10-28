using api.Models;
using api.Importing;
using System.Diagnostics;
using CardPrinting = api.Data.DbSeeder.SeedCardPrinting;

namespace api.Data
{
    public static class DbSeeder
    {
        /// <summary>
        /// The single application owner. All collection, deck, and wishlist data belongs to this user.
        /// </summary>
        public const int DefaultUserId = 1;

        public static void Seed(AppDbContext context)
        {
            // Ensure the single default user exists
            if (!context.Users.Any(u => u.Id == DefaultUserId))
            {
                context.Users.Add(new User { Id = DefaultUserId, Username = "owner", DisplayName = "Owner" });
                context.SaveChanges();
            }

            // Skip card seeding if already seeded
            if (context.Cards.Any()) return;

            // Seed Cards
            var card1 = CreateCard(context, "Disabling Fang Fighter", "Star Wars Unlimited", "Unit");
            var card2 = CreateCard(context, "Shin Hati, Overeager Apprentice", "Star Wars Unlimited", "Unit");
            var card3 = CreateCard(context, "Mega Manectric ex", "Pokemon TCG", "Pokemon");
            var card4 = CreateCard(context, "Parallel Lives", "Magic: The Gathering", "Enchantment");
            var card5 = CreateCard(context, "Darth Maul, Revenge At Last", "Star Wars Unlimited", "Unit");
            var card6 = CreateCard(context, "Maul, Shadow Collective Visionary", "Star Wars Unlimited", "Unit");
            var card7 = CreateCard(context, "Darth Maul's Lightsaber", "Star Wars Unlimited", "Upgrade");
            var card8 = CreateCard(context, "Battle Fury", "Star Wars Unlimited", "Upgrade");
            var card9 = CreateCard(context, "Shadowed Intentions", "Star Wars Unlimited", "Upgrade");
            var card10 = CreateCard(context, "Ataru Onslaught", "Star Wars Unlimited", "Event");
            var card11 = CreateCard(context, "Calm in the Storm", "Star Wars Unlimited", "Event");
            var card12 = CreateCard(context, "In the Shadows", "Star Wars Unlimited", "Event");
            var card13 = CreateCard(context, "Unleash Rage", "Star Wars Unlimited", "Event");
            var card14 = CreateCard(context, "Face Off", "Star Wars Unlimited", "Event");
            var card15 = CreateCard(context, "Unnatural Life", "Star Wars Unlimited", "Event");
            var card16 = CreateCard(context, "Baylan Skoll, Enigmatic Master", "Star Wars Unlimited", "Unit");
            context.Cards.AddRange(card1, card2, card3, card4, card5, card6, card7, card8, card9, card10, card11, card12, card13, card14, card15, card16);

            context.SaveChanges(); // Save to get IDs for relationships

            // Seed CardPrintings
            var printing1a = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Standard"), ImageUrl = "images/swu/SOR/162.png" };
            var printing1b = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "162", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Standard Foil"), ImageUrl = "images/swu/SOR/162.png" };
            var printing1c = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Hyperspace"), ImageUrl = "images/swu/SOR/425.png" };
            var printing1d = new CardPrinting { CardId = card1.Id, Set = "Spark of Rebellion", Number = "425", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Hyperspace Foil"), ImageUrl = "images/swu/SOR/425.png" };
            var printing1e = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Standard"), ImageUrl = "images/swu/SHD/166.png" };
            var printing1f = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "166", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Standard Foil"), ImageUrl = "images/swu/SHD/166.png" };
            var printing1g = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Hyperspace"), ImageUrl = "images/swu/SHD/435.png" };
            var printing1h = new CardPrinting { CardId = card1.Id, Set = "Shadows of the Galaxy", Number = "435", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card1.Game, "Hyperspace Foil"), ImageUrl = "images/swu/SHD/435.png" };
            var printing2a = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "183", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Standard"), ImageUrl = "images/swu/LOF/183.png" };
            var printing2b = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "685", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/685.png" };
            var printing2c = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "447", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/447.png" };
            var printing2d = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "923", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/923.png" };
            var printing2e = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1066", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Standard Prestige"), ImageUrl = "images/swu/LOF/1066.png" };
            var printing2f = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1112", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Foil Prestige"), ImageUrl = "images/swu/LOF/1112.png" };
            var printing2g = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force", Number = "1158", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Serialized Prestige"), ImageUrl = "images/swu/LOF/1158.png" };
            var printing2h = new CardPrinting { CardId = card2.Id, Set = "2025 Promo", Number = "70", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "GC Prize Wall"), ImageUrl = "images/swu/2025p/70.png" };
            var printing2i = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force Weekly Play", Number = "13", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Weekly Play"), ImageUrl = "images/swu/LOFwp/13.png" };
            var printing2j = new CardPrinting { CardId = card2.Id, Set = "Legends of the Force Weekly Play", Number = "33", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card2.Game, "Weekly Play Foil"), ImageUrl = "images/swu/LOFwp/33.png" };
            var printing3a = new CardPrinting { CardId = card3.Id, Set = "Mega Evolution", Number = "50", Rarity = "Double Rare", Treatment = TreatmentLookup.Upsert(context, card3.Game, "Standard"), ImageUrl = "https://assets.pokemon.com/static-assets/content-assets/cms2/img/cards/web/ME01/ME01_EN_50.png" };
            var printing3b = new CardPrinting { CardId = card3.Id, Set = "Mega Evolution", Number = "158", Rarity = "Ultra Rare", Treatment = TreatmentLookup.Upsert(context, card3.Game, "Holo"), ImageUrl = "https://assets.pokemon.com/static-assets/content-assets/cms2/img/cards/web/ME01/ME01_EN_158.png" };
            var printing4a = new CardPrinting { CardId = card4.Id, Set = "Innistrad", Number = "199", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/0/1/01033dae-fec1-41f2-b7f2-cc6a43331790.jpg?1562825348" };
            var printing4b = new CardPrinting { CardId = card4.Id, Set = "Jumpstart: Historic Horizons", Number = "615", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/8/1/818a6d06-65e7-4649-85aa-5b0672d2185f.jpg?1630250676" };
            var printing4c = new CardPrinting { CardId = card4.Id, Set = "Wilds of Eldraine: Enchanting Tales", Number = "103", Rarity = "Mythic", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Foil"), ImageUrl = "https://cards.scryfall.io/large/front/e/d/ede3f0ae-dcca-409f-a2f9-7a86424d8e7c.jpg?1733759987" };
            var printing4d = new CardPrinting { CardId = card4.Id, Set = "Judge Gift Cards 2022", Number = "3", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Foil"), ImageUrl = "https://cards.scryfall.io/large/front/c/5/c532c697-2a66-4c69-a785-59822581b379.jpg?1651636215" };
            var printing4e = new CardPrinting { CardId = card4.Id, Set = "Wilds of Eldraine: Enchanting Tales", Number = "83", Rarity = "Mythic", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/9/3/93ee144f-3c76-444f-8a9a-318ee6407526.jpg?1692933286" };
            var printing4f = new CardPrinting { CardId = card4.Id, Set = "Wilds of Eldraine: Enchanting Tales", Number = "58", Rarity = "Mythic", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/7/3/73558b8d-fb08-4df5-99e3-d4c11c9ccfa7.jpg?1692932890" };
            var printing4g = new CardPrinting { CardId = card4.Id, Set = "Through the Omenpaths Bonus Sheet", Number = "36", Rarity = "Mythic", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/5/0/5047bf16-982d-44b4-ae8a-9c5a271cae0a.jpg?1757551057" };
            var printing4h = new CardPrinting { CardId = card4.Id, Set = "Marvel Universe", Number = "36", Rarity = "Mythic", Treatment = TreatmentLookup.Upsert(context, card4.Game, "Standard"), ImageUrl = "https://cards.scryfall.io/large/front/0/c/0c9c514f-f506-4b2c-af58-79922834cde7.jpg?1757376707" };
            var printing5a = new CardPrinting { CardId = card5.Id, Set = "Twilight of the Republic", Number = "135", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Standard"), ImageUrl = "images/swu/TWI/135.png" };
            var printing5b = new CardPrinting { CardId = card5.Id, Set = "Twilight of the Republic", Number = "135", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Standard Foil"), ImageUrl = "images/swu/TWI/135.png" };
            var printing5c = new CardPrinting { CardId = card5.Id, Set = "Twilight of the Republic", Number = "403", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Hyperspace"), ImageUrl = "images/swu/TWI/403.png" };
            var printing5d = new CardPrinting { CardId = card5.Id, Set = "Twilight of the Republic", Number = "403", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Hyperspace Foil"), ImageUrl = "images/swu/TWI/403.png" };
            var printing5e = new CardPrinting { CardId = card5.Id, Set = "2024 Convention Exclusive", Number = "6", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Convention Exclusive"), ImageUrl = "images/swu/2024ce/6.png" };
            var printing5f = new CardPrinting { CardId = card5.Id, Set = "2025 Judge Program", Number = "5", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "Judge Program"), ImageUrl = "images/swu/2025jp/5.png" };
            var printing5g = new CardPrinting { CardId = card5.Id, Set = "2025 Promo", Number = "91", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card5.Game, "GC Event Pack"), ImageUrl = "images/swu/2025p/91.png" };
            var printing6a = new CardPrinting { CardId = card6.Id, Set = "Shadows of the Galaxy", Number = "90", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "Standard"), ImageUrl = "images/swu/SHD/90.png" };
            var printing6b = new CardPrinting { CardId = card6.Id, Set = "Shadows of the Galaxy", Number = "90", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "Standard Foil"), ImageUrl = "images/swu/SHD/90.png" };
            var printing6c = new CardPrinting { CardId = card6.Id, Set = "Shadows of the Galaxy", Number = "359", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "Hyperspace"), ImageUrl = "images/swu/SHD/359.png" };
            var printing6d = new CardPrinting { CardId = card6.Id, Set = "Shadows of the Galaxy", Number = "359", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "Hyperspace Foil"), ImageUrl = "images/swu/SHD/359.png" };
            var printing6e = new CardPrinting { CardId = card6.Id, Set = "2024 Judge Program", Number = "4", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "Judge Program"), ImageUrl = "images/swu/2024jp/4.png" };
            var printing6f = new CardPrinting { CardId = card6.Id, Set = "2025 Promo", Number = "155", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "RQ Prize Wall"), ImageUrl = "images/swu/2025p/155.png" };
            var printing6g = new CardPrinting { CardId = card6.Id, Set = "2025 Promo", Number = "165", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card6.Game, "RQ Event Pack"), ImageUrl = "images/swu/2025p/165.png" };
            var printing7a = new CardPrinting { CardId = card7.Id, Set = "Legends of the Force", Number = "140", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card7.Game, "Standard"), ImageUrl = "images/swu/LOF/140.png" };
            var printing7b = new CardPrinting { CardId = card7.Id, Set = "Legends of the Force", Number = "642", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card7.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/642.png" };
            var printing7c = new CardPrinting { CardId = card7.Id, Set = "Legends of the Force", Number = "404", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card7.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/404.png" };
            var printing7d = new CardPrinting { CardId = card7.Id, Set = "Legends of the Force", Number = "880", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card7.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/880.png" };
            var printing7e = new CardPrinting { CardId = card7.Id, Set = "2025 Promo", Number = "108", Rarity = "Special", Treatment = TreatmentLookup.Upsert(context, card7.Game, "GC Top 64"), ImageUrl = "images/swu/2025p/108.png" };
            var printing8a = new CardPrinting { CardId = card8.Id, Set = "Legends of the Force", Number = "139", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card8.Game, "Standard"), ImageUrl = "images/swu/LOF/139.png" };
            var printing8b = new CardPrinting { CardId = card8.Id, Set = "Legends of the Force", Number = "641", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card8.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/641.png" };
            var printing8c = new CardPrinting { CardId = card8.Id, Set = "Legends of the Force", Number = "403", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card8.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/403.png" };
            var printing8d = new CardPrinting { CardId = card8.Id, Set = "Legends of the Force", Number = "879", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card8.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/879.png" };
            var printing9a = new CardPrinting { CardId = card9.Id, Set = "Twilight of the Republic", Number = "220", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card9.Game, "Standard"), ImageUrl = "images/swu/TWI/220.png" };
            var printing9b = new CardPrinting { CardId = card9.Id, Set = "Twilight of the Republic", Number = "220", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card9.Game, "Standard Foil"), ImageUrl = "images/swu/TWI/220.png" };
            var printing9c = new CardPrinting { CardId = card9.Id, Set = "Twilight of the Republic", Number = "485", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card9.Game, "Hyperspace"), ImageUrl = "images/swu/TWI/485.png" };
            var printing9d = new CardPrinting { CardId = card9.Id, Set = "Twilight of the Republic", Number = "485", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card9.Game, "Hyperspace Foil"), ImageUrl = "images/swu/TWI/485.png" };
            var printing10a = new CardPrinting { CardId = card10.Id, Set = "Legends of the Force", Number = "174", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card10.Game, "Standard"), ImageUrl = "images/swu/LOF/174.png" };
            var printing10b = new CardPrinting { CardId = card10.Id, Set = "Legends of the Force", Number = "676", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card10.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/676.png" };
            var printing10c = new CardPrinting { CardId = card10.Id, Set = "Legends of the Force", Number = "438", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card10.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/438.png" };
            var printing10d = new CardPrinting { CardId = card10.Id, Set = "Legends of the Force", Number = "914", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card10.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/914.png" };
            var printing11a = new CardPrinting { CardId = card11.Id, Set = "Legends of the Force", Number = "54", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card11.Game, "Standard"), ImageUrl = "images/swu/LOF/54.png" };
            var printing11b = new CardPrinting { CardId = card11.Id, Set = "Legends of the Force", Number = "556", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card11.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/556.png" };
            var printing11c = new CardPrinting { CardId = card11.Id, Set = "Legends of the Force", Number = "318", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card11.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/318.png" };
            var printing11d = new CardPrinting { CardId = card11.Id, Set = "Legends of the Force", Number = "794", Rarity = "Uncommon", Treatment = TreatmentLookup.Upsert(context, card11.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/794.png" };
            var printing12a = new CardPrinting { CardId = card12.Id, Set = "Legends of the Force", Number = "241", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card12.Game, "Standard"), ImageUrl = "images/swu/LOF/241.png" };
            var printing12b = new CardPrinting { CardId = card12.Id, Set = "Legends of the Force", Number = "743", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card12.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/743.png" };
            var printing12c = new CardPrinting { CardId = card12.Id, Set = "Legends of the Force", Number = "505", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card12.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/505.png" };
            var printing12d = new CardPrinting { CardId = card12.Id, Set = "Legends of the Force", Number = "981", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card12.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/981.png" };
            var printing13a = new CardPrinting { CardId = card13.Id, Set = "Legends of the Force", Number = "173", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card13.Game, "Standard"), ImageUrl = "images/swu/LOF/173.png" };
            var printing13b = new CardPrinting { CardId = card13.Id, Set = "Legends of the Force", Number = "675", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card13.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/675.png" };
            var printing13c = new CardPrinting { CardId = card13.Id, Set = "Legends of the Force", Number = "437", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card13.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/437.png" };
            var printing13d = new CardPrinting { CardId = card13.Id, Set = "Legends of the Force", Number = "913", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card13.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/913.png" };
            var printing14a = new CardPrinting { CardId = card14.Id, Set = "Jump to Lightspeed", Number = "178", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card14.Game, "Standard"), ImageUrl = "images/swu/JTL/178.png" };
            var printing14b = new CardPrinting { CardId = card14.Id, Set = "Jump to Lightspeed", Number = "676", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card14.Game, "Standard Foil"), ImageUrl = "images/swu/JTL/676.png" };
            var printing14c = new CardPrinting { CardId = card14.Id, Set = "Jump to Lightspeed", Number = "440", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card14.Game, "Hyperspace"), ImageUrl = "images/swu/JTL/440.png" };
            var printing14d = new CardPrinting { CardId = card14.Id, Set = "Jump to Lightspeed", Number = "912", Rarity = "Common", Treatment = TreatmentLookup.Upsert(context, card14.Game, "Hyperspace Foil"), ImageUrl = "images/swu/JTL/912.png" };
            var printing15a = new CardPrinting { CardId = card15.Id, Set = "Twilight of the Republic", Number = "189", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card15.Game, "Standard"), ImageUrl = "images/swu/TWI/189.png" };
            var printing15b = new CardPrinting { CardId = card15.Id, Set = "Twilight of the Republic", Number = "189", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card15.Game, "Standard Foil"), ImageUrl = "images/swu/TWI/189.png" };
            var printing15c = new CardPrinting { CardId = card15.Id, Set = "Twilight of the Republic", Number = "454", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card15.Game, "Hyperspace"), ImageUrl = "images/swu/TWI/454.png" };
            var printing15d = new CardPrinting { CardId = card15.Id, Set = "Twilight of the Republic", Number = "454", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card15.Game, "Hyperspace Foil"), ImageUrl = "images/swu/TWI/454.png" };
            var printing15e = new CardPrinting { CardId = card15.Id, Set = "2025 Promo", Number = "56", Rarity = "Rare", Treatment = TreatmentLookup.Upsert(context, card15.Game, "RQ Prize Wall"), ImageUrl = "images/swu/2025p/56.png" };
            var printing16a = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "185", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Standard"), ImageUrl = "images/swu/LOF/185.png" };
            var printing16b = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "687", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Standard Foil"), ImageUrl = "images/swu/LOF/687.png" };
            var printing16c = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "449", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Hyperspace"), ImageUrl = "images/swu/LOF/449.png" };
            var printing16d = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "925", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Hyperspace Foil"), ImageUrl = "images/swu/LOF/925.png" };
            var printing16e = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "1065", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Standard Prestige"), ImageUrl = "images/swu/LOF/1065.png" };
            var printing16f = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "1111", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Foil Prestige"), ImageUrl = "images/swu/LOF/1111.png" };
            var printing16g = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force", Number = "1157", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Serialized Prestige"), ImageUrl = "images/swu/LOF/1157.png" };
            var printing16h = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force Weekly Play", Number = "14", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Weekly Play"), ImageUrl = "images/swu/LOFwp/14.png" };
            var printing16i = new CardPrinting { CardId = card16.Id, Set = "Legends of the Force Weekly Play", Number = "34", Rarity = "Legendary", Treatment = TreatmentLookup.Upsert(context, card16.Game, "Weekly Play Foil"), ImageUrl = "images/swu/LOFwp/34.png" };
            var seedPrintings = new[]
            {
                printing1a, printing1b, printing1c, printing1d, printing1e, printing1f, printing1g, printing1h,
                printing2a, printing2b, printing2c, printing2d, printing2e, printing2f, printing2g, printing2h,
                printing2i, printing2j, printing3a, printing4a, printing5a, printing5b, printing5c, printing5d,
                printing5e, printing5f, printing5g, printing6a, printing6b, printing6c, printing6d, printing6e,
                printing6f, printing6g, printing7a, printing7b, printing7c, printing7d, printing7e, printing8a,
                printing8b, printing8c, printing9a, printing9b, printing9c, printing9d, printing10a, printing10b,
                printing10c, printing10d, printing11a, printing11b, printing11c, printing11d, printing12a,
                printing12b, printing12c, printing12d, printing13a, printing13b, printing13c, printing13d,
                printing14a, printing14b, printing14c, printing14d, printing15a, printing15b, printing15c,
                printing15d, printing15e, printing16a, printing16b, printing16c, printing16d, printing16e,
                printing16f, printing16g, printing16h, printing16i
            };
            var cardPrintings = seedPrintings.Select(seedPrinting =>
            {
                var card = context.Cards.Local.Single(candidate => candidate.Id == seedPrinting.CardId);
                return new api.Models.CardPrinting
                {
                    CardId = seedPrinting.CardId,
                    Number = seedPrinting.Number,
                    CatalogSet = TreatmentLookup.UpsertSet(context, card.Game, seedPrinting.Set),
                    RarityLookup = TreatmentLookup.UpsertRarity(context, card.Game, seedPrinting.Rarity),
                    Treatment = seedPrinting.Treatment,
                    ImageUrl = seedPrinting.ImageUrl
                };
            }).ToArray();
            context.CardPrintings.AddRange(cardPrintings);
            context.SaveChanges();
            for (var index = 0; index < seedPrintings.Length; index++)
                seedPrintings[index].Id = cardPrintings[index].Id;

            // Seed UserCards (collection data)
            var userCard1a = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1a.Id,
                QuantityOwned = 8,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1b = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1b.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1c = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1c.Id,
                QuantityOwned = 0,
                QuantityWanted = 1,
                QuantityProxyOwned = 1
            };
            var userCard1d = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1d.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1e = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1e.Id,
                QuantityOwned = 9,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1f = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1f.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1g = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1g.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard1h = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing1h.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2a = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2a.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2b = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2b.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2c = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2c.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2d = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2d.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2e = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2e.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2f = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2f.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2g = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2g.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2h = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2h.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2i = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2i.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard2j = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing2j.Id,
                QuantityOwned = 4,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard3a = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing3a.Id,
                QuantityOwned = 2,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            var userCard4a = new UserCard
            {
                UserId = DefaultUserId,
                CardPrintingId = printing4a.Id,
                QuantityOwned = 1,
                QuantityWanted = 0,
                QuantityProxyOwned = 1
            };
            context.UserCards.AddRange(userCard1a, userCard1b, userCard1c, userCard1d, userCard1e, userCard1f, userCard1g, userCard1h, userCard2a, userCard2b, userCard2c, userCard2d, userCard2e, userCard2f, userCard2g, userCard2h, userCard2i, userCard2j, userCard3a, userCard4a);

            context.SaveChanges();

            // Seed Decks
            var deck1 = new Deck { UserId = DefaultUserId, Name = "My Deck", Description = "A sample deck.", Game = "Star Wars Unlimited" };
            var deck2 = new Deck { UserId = DefaultUserId, Name = "My Second Deck", Description = "Another sample deck.", Game = "Star Wars Unlimited" };
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

            var deck1Card1 = new DeckCard { Deck = deck1, CardPrintingId = printing1a.Id, QuantityInDeck = 2, QuantityIdea = 1, QuantityAcquire = 1, QuantityProxy = 0 };
            var deck1Card2 = new DeckCard { Deck = deck1, CardPrintingId = printing1b.Id, QuantityInDeck = 1, QuantityIdea = 0, QuantityAcquire = 0, QuantityProxy = 0 };
            var deck2Card1 = new DeckCard { Deck = deck2, CardPrintingId = printing2a.Id, QuantityInDeck = 3, QuantityIdea = 3, QuantityAcquire = 0, QuantityProxy = 0 };
            var deck2Card2 = new DeckCard { Deck = deck2, CardPrintingId = printing2b.Id, QuantityInDeck = 2, QuantityIdea = 1, QuantityAcquire = 2, QuantityProxy = 0 };
            context.DeckCards.AddRange(deck1Card1, deck1Card2, deck2Card1, deck2Card2);
            context.SaveChanges();
        }

        private static Card CreateCard(AppDbContext context, string name, string gameName, string type)
        {
            var card = new Card
            {
                Name = name,
                Game = TreatmentLookup.GetOrAddGame(context, gameName)
            };
            TreatmentLookup.UpsertCardType(context, card, type);
            return card;
        }

        internal sealed class SeedCardPrinting
        {
            public int Id { get; set; }
            public int CardId { get; init; }
            public required string Set { get; init; }
            public required string Number { get; init; }
            public required string Rarity { get; init; }
            public required Treatment Treatment { get; init; }
            public string? ImageUrl { get; init; }
        }
    }
}
