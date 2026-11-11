using System.Net;
using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public class PrintingsAspectBudgetTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Get_AspectBudget_FiltersBeforePaginationAndCountsEverySymbol()
    {
        await SeedAsync();
        using var client = factory.CreateClient();
        var names = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await client.GetAsync(
                $"/api/cards/printings?game=Star%20Wars%20Unlimited&type=Unit&uniqueCards=true&sort=name&pageSize=2&page={page}&availableAspects=command,COMMAND,Villainy");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("5", Assert.Single(response.Headers.GetValues("X-Total-Count")));
            var rows = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
            Assert.NotNull(rows);
            Assert.Equal(page == 3 ? 1 : 2, rows.Count);
            names.AddRange(rows.Select(row => row.CardName));
        }

        Assert.Equal(new[] { "Command", "Double Command", "Mixed", "Neutral", "Villainy" }, names);

        var combined = await client.GetAsync(
            "/api/cards/printings?game=Star%20Wars%20Unlimited&availableAspects=Command,Command,Villainy&aspect=Villainy");
        Assert.Equal(HttpStatusCode.OK, combined.StatusCode);
        Assert.Equal("2", Assert.Single(combined.Headers.GetValues("X-Total-Count")));
        var combinedRows = await combined.Content.ReadFromJsonAsync<List<PrintingDto>>();
        Assert.NotNull(combinedRows);
        Assert.All(combinedRows, row => Assert.Contains(row.Aspects, aspect => aspect.Name == "Villainy"));

        var unfiltered = await client.GetAsync("/api/cards/printings?game=Star%20Wars%20Unlimited");
        Assert.Equal("25", Assert.Single(unfiltered.Headers.GetValues("X-Total-Count")));
    }

    [Fact]
    public async Task Get_EmptyAspectBudget_ReturnsOnlyNeutralCards()
    {
        await SeedAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            "/api/cards/printings?game=Star%20Wars%20Unlimited&availableAspects=");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1", Assert.Single(response.Headers.GetValues("X-Total-Count")));
        var rows = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        Assert.NotNull(rows);
        Assert.Equal("Neutral", Assert.Single(rows).CardName);
    }

    [Fact]
    public async Task Get_PrintingMetadata_UsesParentCardValues()
    {
        await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var card = await db.Cards.SingleAsync(c => c.Name == "Neutral");
            card.Cost = 4;
            card.Subtitle = "Card subtitle";
            card.Destiny = "3";
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            "/api/cards/printings?game=Star%20Wars%20Unlimited&q=Neutral");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        Assert.NotNull(rows);
        var printing = Assert.Single(rows);
        Assert.Equal(4, printing.Cost);
        Assert.Equal("Card subtitle", printing.Subtitle);
        Assert.Equal("3", printing.Destiny);
    }

    private async Task SeedAsync()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await db.Games.SingleAsync(g => g.Name == "Star Wars Unlimited");
        var command = new Aspect { Name = "Command", GameId = game.Id };
        var villainy = new Aspect { Name = "Villainy", GameId = game.Id };
        var aggression = new Aspect { Name = "Aggression", GameId = game.Id };
        var unit = new CardType { Name = "Unit", GameId = game.Id };

        void Add(string name, params (Aspect Aspect, int Count)[] aspects)
        {
            var card = new Card { Name = name, GameId = game.Id };
            card.CardTypes.Add(new CardCardType { CardType = unit, SourceRole = "Type" });
            foreach (var (aspect, count) in aspects)
            {
                card.CardAspects.Add(new CardAspect { Aspect = aspect, Count = count });
            }
            db.CardPrintings.Add(new CardPrinting { Card = card, Number = name });
        }

        for (var i = 0; i < 20; i++)
        {
            Add($"00 Out of aspect {i:D2}", i % 2 == 0 ? (command, 3) : (aggression, 1));
        }
        Add("Command", (command, 1));
        Add("Double Command", (command, 2));
        Add("Mixed", (command, 1), (villainy, 1));
        Add("Neutral");
        Add("Villainy", (villainy, 1));
        await db.SaveChangesAsync();
    }
}
