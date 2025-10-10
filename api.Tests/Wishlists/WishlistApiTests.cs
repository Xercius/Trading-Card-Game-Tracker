using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Xunit;
using api.Tests.Infrastructure;

namespace api.Tests.Wishlists;

public class WishlistApiTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task GetWishlist_ReturnsOnlyCurrentUserEntries()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/wishlist");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<WishlistItemContract>>();
        items.Should().NotBeNull();
        items!.Should().HaveCount(2);
        items.Should().Contain(i => i.CardPrintingId == Seed.LightningBetaPrintingId);
        items.Should().Contain(i => i.CardPrintingId == Seed.PhoenixPrintingId);
    }

    [Fact]
    public async Task QuickAdd_AddsNewWishlistQuantity()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/wishlist/items", new
        {
            printingId = Seed.GoblinPrintingId,
            quantity = 2
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickAddResponseContract>();
        result.Should().NotBeNull();
        result!.PrintingId.Should().Be(Seed.GoblinPrintingId);
        result.QuantityWanted.Should().Be(2);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var card = await db.UserCards.FindAsync(Seed.AdminUserId, Seed.GoblinPrintingId);
            card.Should().NotBeNull();
            card!.QuantityWanted.Should().Be(2);
        });
    }

    [Fact]
    public async Task QuickAdd_ForExistingPrinting_AccumulatesQuantity()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        await client.PostAsJsonAsync("/api/wishlist/items", new { printingId = Seed.LightningBetaPrintingId, quantity = 2 });
        var response = await client.PostAsJsonAsync("/api/wishlist/items", new { printingId = Seed.LightningBetaPrintingId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickAddResponseContract>();
        result.Should().NotBeNull();
        result!.QuantityWanted.Should().Be(4); // initial 1 + 2 + 1
    }

    [Fact]
    public async Task MoveToCollection_ReducesWishlistAndUpdatesOwned()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/wishlist/move-to-collection", new
        {
            cardPrintingId = Seed.PhoenixPrintingId,
            quantity = 1,
            useProxy = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MoveToCollectionResponseContract>();
        result.Should().NotBeNull();
        result!.CardPrintingId.Should().Be(Seed.PhoenixPrintingId);
        result.QuantityWanted.Should().Be(1);
        result.QuantityOwned.Should().Be(1);
        result.Availability.Should().Be(1);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var card = await db.UserCards.FindAsync(Seed.AdminUserId, Seed.PhoenixPrintingId);
            card.Should().NotBeNull();
            card!.QuantityWanted.Should().Be(1);
            card.QuantityOwned.Should().Be(1);
        });
    }

    private sealed record WishlistItemContract(int CardPrintingId, int QuantityWanted, int CardId, string CardName, string Game, string Set, string Number, string Rarity, string Style, string? ImageUrl);

    private sealed record QuickAddResponseContract(int PrintingId, int QuantityWanted);

    private sealed record MoveToCollectionResponseContract(int CardPrintingId, int QuantityWanted, int QuantityOwned, int QuantityProxyOwned, int Availability, int AvailabilityWithProxies);
}
