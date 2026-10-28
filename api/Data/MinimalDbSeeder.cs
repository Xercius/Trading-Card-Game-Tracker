using api.Models;
using api.Importing;

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

        var cards = CreateSampleCards(context);
        context.Cards.AddRange(cards);
        context.SaveChanges();
        Console.WriteLine("Minimal seed complete: added sample cards and printings.");
    }

    private static IReadOnlyList<Card> CreateSampleCards(AppDbContext context) => new List<Card>
    {
        new()
        {
            Game = "Magic: The Gathering",
            Name = "Lightning Bolt",
            CardType = "Instant",
            Printings =
            {
                new CardPrinting
                {
                    Set = "Limited Edition Alpha",
                    Number = "150",
                    Rarity = "Common",
                    Treatment = TreatmentLookup.Upsert(context, "Magic: The Gathering", "Standard"),
                },
            },
        },
        new()
        {
            Game = "Pokemon TCG",
            Name = "Pikachu",
            CardType = "Pokemon",
            Printings =
            {
                new CardPrinting
                {
                    Set = "Base Set",
                    Number = "58",
                    Rarity = "Common",
                    Treatment = TreatmentLookup.Upsert(context, "Pokemon TCG", "Standard"),
                },
            },
        },
        new()
        {
            Game = "Star Wars Unlimited",
            Name = "Luke Skywalker, Hope of the Rebellion",
            CardType = "Unit",
            Printings =
            {
                new CardPrinting
                {
                    Set = "Spark of Rebellion",
                    Number = "001",
                    Rarity = "Legendary",
                    Treatment = TreatmentLookup.Upsert(context, "Star Wars Unlimited", "Standard"),
                },
            },
        },
    };
}
