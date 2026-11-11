using api.Data;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Features.Decks;

public class DeckConstructionTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private sealed record AvailabilityRow(int PrintingId, int QuantityInDeck, int Availability, int AvailabilityWithProxies);

    [Theory]
    [InlineData("Star Wars Unlimited", false, 0, 0)]
    [InlineData("Star Wars Unlimited", true, 0, 0)]
    [InlineData("Star Wars Unlimited", false, 1, 2)]
    [InlineData("Star Wars Unlimited", true, 1, 2)]
    [InlineData("Star Wars CCG", false, 0, 0)]
    [InlineData("Star Wars CCG", true, 0, 0)]
    [InlineData("Star Wars CCG", false, 1, 2)]
    [InlineData("Star Wars CCG", true, 1, 2)]
    [InlineData("Magic", false, 0, 0)]
    [InlineData("Magic", true, 1, 2)]
    public async Task QuantityDelta_UnownedOrAboveOwned_PersistsDeckWithoutChangingCollection(string game, bool includeProxies, int owned, int proxies)
    {
        var (deckId, printingId) = await SeedDeckAsync(game, owned, proxies);
        using var client = factory.CreateClient();

        for (var quantity = 1; quantity <= 4; quantity++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/decks/{deckId}/cards/quantity-delta?includeProxies={includeProxies}",
                new { printingId, qtyDelta = 1 });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var row = await response.Content.ReadFromJsonAsync<AvailabilityRow>();
            Assert.NotNull(row);
            Assert.Equal(quantity, row.QuantityInDeck);
        }

        var cards = await client.GetFromJsonAsync<List<AvailabilityRow>>(
            $"/api/decks/{deckId}/cards-with-availability?includeProxies={includeProxies}");
        var persisted = Assert.Single(Assert.IsType<List<AvailabilityRow>>(cards));
        Assert.Equal(printingId, persisted.PrintingId);
        Assert.Equal(4, persisted.QuantityInDeck);
        Assert.Equal(0, persisted.Availability);
        Assert.Equal(0, persisted.AvailabilityWithProxies);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var collection = await db.UserCards.SingleOrDefaultAsync(c => c.CardPrintingId == printingId);
        if (owned == 0 && proxies == 0)
        {
            Assert.Null(collection);
        }
        else
        {
            Assert.NotNull(collection);
            Assert.Equal(owned, collection.QuantityOwned);
            Assert.Equal(proxies, collection.QuantityProxyOwned);
        }
    }

    [Theory]
    [InlineData("Star Wars Unlimited", false)]
    [InlineData("Star Wars Unlimited", true)]
    [InlineData("Star Wars CCG", false)]
    [InlineData("Star Wars CCG", true)]
    [InlineData("Magic", false)]
    [InlineData("Magic", true)]
    public async Task Upsert_UnownedPrinting_PersistsAbsoluteQuantity(string game, bool includeProxies)
    {
        var (deckId, printingId) = await SeedDeckAsync(game, 0, 0);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/decks/{deckId}/cards/upsert?includeProxies={includeProxies}",
                new { printingId, qty = 3 });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var row = await response.Content.ReadFromJsonAsync<AvailabilityRow>();
            Assert.NotNull(row);
            Assert.Equal(3, row.QuantityInDeck);
            Assert.Equal(0, row.Availability);
        }

        var cards = await client.GetFromJsonAsync<List<AvailabilityRow>>(
            $"/api/decks/{deckId}/cards-with-availability");
        Assert.Equal(3, Assert.Single(Assert.IsType<List<AvailabilityRow>>(cards)).QuantityInDeck);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.UserCards.AnyAsync(c => c.CardPrintingId == printingId));
    }

    [Fact]
    public async Task QuantityDelta_UnownedPrinting_RemovesAtZeroAndRejectsInvalidPrinting()
    {
        var (deckId, printingId) = await SeedDeckAsync("Star Wars Unlimited", 0, 0);
        using var client = factory.CreateClient();
        var add = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta", new { printingId, qtyDelta = 1 });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var remove = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta", new { printingId, qtyDelta = -10 });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var cards = await client.GetFromJsonAsync<List<AvailabilityRow>>(
            $"/api/decks/{deckId}/cards-with-availability");
        Assert.Empty(Assert.IsType<List<AvailabilityRow>>(cards));

        var missing = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta", new { printingId = int.MaxValue, qtyDelta = 1 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var invalid = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta", new { printingId = 0, qtyDelta = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("quantity-delta")]
    [InlineData("upsert")]
    public async Task DeckCardUpdate_DifferentGame_ReturnsValidationErrorWithoutAddingCard(string endpoint)
    {
        var (deckId, _) = await SeedDeckAsync("Star Wars CCG", 0, 0);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/{endpoint}",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, qtyDelta = 1, qty = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Card game does not match deck game.",
            problem.GetProperty("errors").GetProperty("cardPrintingId")[0].GetString());

        var cards = await client.GetFromJsonAsync<List<AvailabilityRow>>(
            $"/api/decks/{deckId}/cards-with-availability");
        Assert.Empty(Assert.IsType<List<AvailabilityRow>>(cards));
    }

    private async Task<(int DeckId, int PrintingId)> SeedDeckAsync(string gameName, int owned, int proxies)
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await db.Games.SingleOrDefaultAsync(g => g.Name == gameName) ?? new Game { Name = gameName };
        var printing = new CardPrinting
        {
            Card = new Card { Name = "Construction Test Card", Game = game },
            Number = "001"
        };
        var deck = new Deck { UserId = TestDataSeeder.UserId, Game = game.Name, Name = "Deck Construction" };
        db.CardPrintings.Add(printing);
        db.Decks.Add(deck);
        if (owned > 0 || proxies > 0)
        {
            db.UserCards.Add(new UserCard
            {
                UserId = TestDataSeeder.UserId,
                CardPrinting = printing,
                QuantityOwned = owned,
                QuantityProxyOwned = proxies
            });
        }
        await db.SaveChangesAsync();
        return (deck.Id, printing.Id);
    }
}
