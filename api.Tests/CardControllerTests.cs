using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Controllers;
using api.Data;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api.Tests;

public class CardControllerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"cards_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Update_PreservesNewPrintings()
    {
        await using var context = CreateContext();

        var card = new Card
        {
            Game = "Test Game",
            Name = "Test Card",
            CardType = "Spell",
            Description = "A test card",
            Printings = new List<CardPrinting>
            {
                new CardPrinting
                {
                    Set = "Base",
                    Number = "001",
                    Rarity = "Common",
                    Style = "Standard",
                    ImageUrl = "http://example.com/base"
                }
            }
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var existingPrinting = card.Printings.Single();

        var controller = new CardController(context);
        var updateDto = new UpdateCardDto(
            card.Game,
            card.Name,
            card.CardType,
            card.Description,
            new List<UpdateCardPrintingDto>
            {
                new(existingPrinting.Id, existingPrinting.Set, existingPrinting.Number, existingPrinting.Rarity, existingPrinting.Style, existingPrinting.ImageUrl),
                new(null, "Expansion", "010", "Rare", "Foil", "http://example.com/expansion")
            }
        );

        var result = await controller.Update(card.Id, updateDto);

        Assert.IsType<NoContentResult>(result);

        var updatedCard = await context.Cards.Include(c => c.Printings).SingleAsync(c => c.Id == card.Id);

        Assert.Equal(2, updatedCard.Printings.Count);
        Assert.Contains(updatedCard.Printings, p => p.Id == existingPrinting.Id);
        Assert.Contains(updatedCard.Printings, p => p.Set == "Expansion" && p.Number == "010");
    }
}
