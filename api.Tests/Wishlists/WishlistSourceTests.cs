using api.Features.Wishlists.Dtos;
using api.Shared;
using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Wishlists;

public class WishlistSourceTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task WishlistSources_MultipleSources_SumsQuantityWantedAndReturnsBreakdown()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // Add General = 1 and Deck = 2 for GoblinPrintingId
        var resGeneral = await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 1
        });
        resGeneral.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resDeck = await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2,
            deckId = Seed.AdminDeckId
        });
        resDeck.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await client.GetFromJsonAsync<Paged<WishlistItemResponse>>(
            $"/api/wishlist?cardPrintingId={Seed.GoblinPrintingId}");
        page.Should().NotBeNull();
        var item = page!.Items.Should().ContainSingle().Subject;
        item.QuantityWanted.Should().Be(3);
        item.Sources.Should().HaveCount(2);
        item.Sources.Should().Contain(s => s.DeckId == null && s.DeckName == "General" && s.Quantity == 1);
        item.Sources.Should().Contain(s => s.DeckId == Seed.AdminDeckId && s.Quantity == 2);
    }

    [Fact]
    public async Task MoveToCollection_MultipleSourcesWithoutDeckId_Returns400()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 1
        });
        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2,
            deckId = Seed.AdminDeckId
        });

        var res = await client.PostAsJsonAsync("/api/wishlist/move-to-collection", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantity = 1
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MoveToCollection_MultipleSourcesWithDeckId_DeductsOnlyThatSource()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 1
        });
        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2,
            deckId = Seed.AdminDeckId
        });

        var res = await client.PostAsJsonAsync("/api/wishlist/move-to-collection", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantity = 1,
            deckId = Seed.AdminDeckId,
            sourceSpecified = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var moved = await res.Content.ReadFromJsonAsync<MoveToCollectionResponse>();
        moved!.WantedAfter.Should().Be(2);
        moved.OwnedAfter.Should().Be(2);

        var page = await client.GetFromJsonAsync<Paged<WishlistItemResponse>>(
            $"/api/wishlist?cardPrintingId={Seed.GoblinPrintingId}");
        var item = page!.Items.Should().ContainSingle().Subject;
        item.Sources.Should().Contain(s => s.DeckId == null && s.Quantity == 1);
        item.Sources.Should().Contain(s => s.DeckId == Seed.AdminDeckId && s.Quantity == 1);
    }

    [Fact]
    public async Task DeleteDeck_RemovesDeckWishlistSourcesAndRecalculatesQuantityWanted()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 1
        });
        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2,
            deckId = Seed.AdminDeckId
        });

        var del = await client.DeleteAsync($"/api/decks/{Seed.AdminDeckId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var remainingSources = await db.WishlistSources
                .Where(w => w.CardPrintingId == Seed.GoblinPrintingId)
                .ToListAsync();
            remainingSources.Should().ContainSingle();
            remainingSources[0].DeckId.Should().BeNull();
            remainingSources[0].Quantity.Should().Be(1);

            var uc = await db.UserCards.FindAsync(Seed.GoblinPrintingId);
            uc!.QuantityWanted.Should().Be(1);
        });
    }

    [Fact]
    public async Task CollectionWantedEdit_ThenWishlistQuickAdd_PreservesEditedQuantityInSources()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // Start with General source = 2
        var initial = await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2
        });
        initial.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Edit QuantityWanted to 5 through collection upsert (same path as EditCardModal / CollectionSetTable)
        var edit = await client.PostAsJsonAsync("/api/collection", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityOwned = 1,
            quantityProxyOwned = 0,
            quantityWanted = 5
        });
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Quick-add 1 more to wishlist: must become 6 (not 3)
        var quickAdd = await client.PostAsJsonAsync("/api/wishlist/items", new
        {
            printingId = Seed.GoblinPrintingId,
            quantity = 1
        });
        quickAdd.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var uc = await db.UserCards.FindAsync(Seed.GoblinPrintingId);
            uc!.QuantityWanted.Should().Be(6);

            var sources = await db.WishlistSources
                .Where(w => w.CardPrintingId == Seed.GoblinPrintingId)
                .ToListAsync();
            sources.Should().ContainSingle();
            sources[0].DeckId.Should().BeNull();
            sources[0].Quantity.Should().Be(6);
        });
    }

    [Fact]
    public async Task CollectionRemove_RemovesOrphanedWishlistSources()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/wishlist", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityWanted = 2
        });

        var del = await client.DeleteAsync($"/api/collection/{Seed.GoblinPrintingId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var sources = await db.WishlistSources
                .Where(w => w.CardPrintingId == Seed.GoblinPrintingId)
                .ToListAsync();
            sources.Should().BeEmpty();
        });
    }
}
