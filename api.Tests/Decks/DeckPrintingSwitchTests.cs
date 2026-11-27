using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Decks;

public class DeckPrintingSwitchTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    private static async Task AddAlphaToDeckAsync(HttpClient client, int qty = 1)
    {
        var addRes = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new
        {
            printingId = Seed.LightningAlphaPrintingId,
            qty
        });
        addRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwitchDeckCardPrinting_ValidTarget_UpdatesPrinting()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // AdminDeckId is seeded with LightningBetaPrintingId (qty 2) and no LightningAlphaPrintingId
        var res = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.LightningBetaPrintingId}/printing",
            new { targetCardPrintingId = Seed.LightningAlphaPrintingId });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var beta = await db.DeckCards.FirstOrDefaultAsync(
                dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.LightningBetaPrintingId);
            var alpha = await db.DeckCards.FirstOrDefaultAsync(
                dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.LightningAlphaPrintingId);

            beta.Should().BeNull();
            alpha.Should().NotBeNull();
            alpha!.QuantityInDeck.Should().Be(2);
        });
    }

    [Fact]
    public async Task SwitchDeckCardPrinting_TargetAlreadyInDeck_MergesQuantities()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();
        await AddAlphaToDeckAsync(client, 1);

        // Also add 2 copies of LightningBetaPrintingId to AdminDeckId
        var addRes = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new
        {
            printingId = Seed.LightningBetaPrintingId,
            qty = 2
        });
        addRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.LightningAlphaPrintingId}/printing",
            new { targetCardPrintingId = Seed.LightningBetaPrintingId });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var alpha = await db.DeckCards.FirstOrDefaultAsync(
                dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.LightningAlphaPrintingId);
            var beta = await db.DeckCards.FirstAsync(
                dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.LightningBetaPrintingId);

            alpha.Should().BeNull();
            beta.QuantityInDeck.Should().Be(3);
        });
    }

    [Fact]
    public async Task SwitchDeckCardPrinting_DifferentCard_Returns400()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();
        await AddAlphaToDeckAsync(client, 1);

        var res = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.LightningAlphaPrintingId}/printing",
            new { targetCardPrintingId = Seed.GoblinPrintingId });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SwitchDeckCardPrinting_MissingDeckCard_Returns404()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        var res = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.GoblinPrintingId}/printing",
            new { targetCardPrintingId = Seed.LightningBetaPrintingId });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SwitchDeckCardPrinting_MigratesDeckWishlistSourceToTargetPrinting()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // Create a deck-linked wishlist source on LightningBetaPrintingId (currently in AdminDeckId)
        var wishRes = await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.LightningBetaPrintingId,
            quantityWanted = 2,
            deckId = Seed.AdminDeckId
        });
        wishRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var switchRes = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.LightningBetaPrintingId}/printing",
            new { targetCardPrintingId = Seed.LightningAlphaPrintingId });
        switchRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var oldSource = await db.WishlistSources.FirstOrDefaultAsync(
                w => w.DeckId == Seed.AdminDeckId && w.CardPrintingId == Seed.LightningBetaPrintingId);
            var newSource = await db.WishlistSources.FirstOrDefaultAsync(
                w => w.DeckId == Seed.AdminDeckId && w.CardPrintingId == Seed.LightningAlphaPrintingId);

            oldSource.Should().BeNull();
            newSource.Should().NotBeNull();
            newSource!.Quantity.Should().Be(2);

            var targetUserCard = await db.UserCards.FindAsync(Seed.LightningAlphaPrintingId);
            targetUserCard!.QuantityWanted.Should().Be(2);
        });
    }
}
