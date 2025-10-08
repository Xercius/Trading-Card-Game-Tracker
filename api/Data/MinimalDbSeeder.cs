using System;
using System.Collections.Generic;
using System.Linq;
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
        if (context.Cards.Any() || context.CardPrintings.Any())
        {
            Console.WriteLine("Minimal seed skipped: database already contains card data.");
            return;
        }

        var cards = CreateSampleCards();
        context.Cards.AddRange(cards);
        context.SaveChanges();
        Console.WriteLine("Minimal seed complete: added sample games and sets.");
    }

    private static IReadOnlyList<Card> CreateSampleCards() => new List<Card>
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
                    Style = "Standard",
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
                    Style = "Standard",
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
                    Style = "Standard",
                },
            },
        },
    };
}
