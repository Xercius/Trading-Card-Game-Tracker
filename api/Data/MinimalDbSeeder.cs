using api.Models;

namespace api.Data;

/// <summary>
/// Provides a light-weight seed for developers that only populates
/// the minimum data required for game/set dropdown testing.
/// </summary>
public static class MinimalDbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (!context.Users.Any(u => u.Id == DbSeeder.DefaultUserId))
        {
            context.Users.Add(new User { Id = DbSeeder.DefaultUserId, Username = "owner", DisplayName = "Owner" });
            context.SaveChanges();
        }

        if (context.Cards.Any() || context.CardPrintings.Any())
        {
            Console.WriteLine("Minimal seed skipped: database already contains card data.");
            return;
        }

        var cards = CreateSampleCards();
        context.Cards.AddRange(cards);
        context.SaveChanges();
        Console.WriteLine("Minimal seed complete: added sample cards and printings.");
    }

    private static IReadOnlyList<Card> CreateSampleCards() => new List<Card>
    {
        new()
        {
            Game = "Magic: The Gathering",
            Name = "Lightning Bolt",
            CardType = "Instant",
            JasonsCardId = 101,
            Arena = "N/A",
            Unique = false,
            Cost = 1,
            Hp = 0,
            Power = 0,
            Printings =
            {
                new CardPrinting
                {
                    Set = "Limited Edition Alpha",
                    Number = "150",
                    Rarity = "Common",
                    Style = "Standard",
                },
            },
        },
        new()
        {
            Game = "Pokemon TCG",
            Name = "Pikachu",
            CardType = "Pokemon",
            JasonsCardId = 102,
            Arena = "N/A",
            Unique = false,
            Cost = 0,
            Hp = 40,
            Power = 0,
            Printings =
            {
                new CardPrinting
                {
                    Set = "Base Set",
                    Number = "58",
                    Rarity = "Common",
                    Style = "Standard",
                },
            },
        },
        new()
        {
            Game = "Star Wars Unlimited",
            Name = "Luke Skywalker, Hope of the Rebellion",
            CardType = "Unit",
            JasonsCardId = 103,
            Arena = "Ground",
            Unique = true,
            Cost = 7,
            Hp = 8,
            Power = 5,
            AspectsJson = "[\"Heroism\"]",
            TraitsJson = "[\"Rebel\",\"Force Sensitive\"]",
            Printings =
            {
                new CardPrinting
                {
                    Set = "Spark of Rebellion",
                    Number = "001",
                    Rarity = "Legendary",
                    Style = "Standard",
                },
            },
        },
    };
}
