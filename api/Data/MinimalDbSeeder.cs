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

    private static IReadOnlyList<Card> CreateSampleCards(AppDbContext context)
    {
        var cards = new List<Card>
        {
        new()
        {
            Game = TreatmentLookup.GetOrAddGame(context, "Magic: The Gathering"),
            Name = "Lightning Bolt",
            Printings =
            {
                new CardPrinting
                {
                    Number = "150",
                    CatalogSet = TreatmentLookup.UpsertSet(
                        context, TreatmentLookup.GetOrAddGame(context, "Magic: The Gathering"), "Limited Edition Alpha"),
                    RarityLookup = TreatmentLookup.UpsertRarity(
                        context, TreatmentLookup.GetOrAddGame(context, "Magic: The Gathering"), "Common"),
                    Treatment = TreatmentLookup.Upsert(context, "Magic: The Gathering", "Standard"),
                },
            },
        },
        new()
        {
            Game = TreatmentLookup.GetOrAddGame(context, "Pokemon TCG"),
            Name = "Pikachu",
            Printings =
            {
                new CardPrinting
                {
                    Number = "58",
                    CatalogSet = TreatmentLookup.UpsertSet(
                        context, TreatmentLookup.GetOrAddGame(context, "Pokemon TCG"), "Base Set"),
                    RarityLookup = TreatmentLookup.UpsertRarity(
                        context, TreatmentLookup.GetOrAddGame(context, "Pokemon TCG"), "Common"),
                    Treatment = TreatmentLookup.Upsert(context, "Pokemon TCG", "Standard"),
                },
            },
        },
        new()
        {
            Game = TreatmentLookup.GetOrAddGame(context, "Star Wars Unlimited"),
            Name = "Luke Skywalker, Hope of the Rebellion",
            Printings =
            {
                new CardPrinting
                {
                    Number = "001",
                    CatalogSet = TreatmentLookup.UpsertSet(
                        context, TreatmentLookup.GetOrAddGame(context, "Star Wars Unlimited"), "Spark of Rebellion"),
                    RarityLookup = TreatmentLookup.UpsertRarity(
                        context, TreatmentLookup.GetOrAddGame(context, "Star Wars Unlimited"), "Legendary"),
                    Treatment = TreatmentLookup.Upsert(context, "Star Wars Unlimited", "Standard"),
                },
            },
        },
        };
        TreatmentLookup.UpsertCardType(context, cards[0], "Instant");
        TreatmentLookup.UpsertCardType(context, cards[1], "Pokemon");
        TreatmentLookup.UpsertCardType(context, cards[2], "Unit");
        return cards;
    }
}
