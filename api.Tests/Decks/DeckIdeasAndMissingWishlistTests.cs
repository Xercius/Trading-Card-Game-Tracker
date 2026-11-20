using api.Features.Decks.Dtos;
using api.Features.Wishlists.Dtos;
using api.Shared;
using api.Tests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Decks;

public class DeckIdeasAndMissingWishlistTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task GetDeckCardsWithAvailability_IncludesIdeaOnlyCards()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // Patch GoblinPrintingId with QuantityIdea = 2 and QuantityInDeck = 0
        var patchRes = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.GoblinPrintingId}",
            new { quantityIdea = 2 });
        patchRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cards = await client.GetFromJsonAsync<List<DeckCardWithAvailabilityResponse>>(
            $"/api/decks/{Seed.AdminDeckId}/cards-with-availability?includeIdeas=true");

        cards.Should().NotBeNull();
        var ideaCard = cards!.Should().ContainSingle(c => c.PrintingId == Seed.GoblinPrintingId).Subject;
        ideaCard.QuantityInDeck.Should().Be(0);
        ideaCard.QuantityIdea.Should().Be(2);
    }

    [Fact]
    public async Task MtgValidation_CopyLimit_IncludesIdeas_ExcludesFromDeckSize()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // AdminDeckId has 2x LightningBetaPrintingId; add 3x LightningAlphaPrintingId as Ideas => 5 total > 4
        var patchRes = await client.PatchAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/cards/{Seed.LightningAlphaPrintingId}",
            new { quantityIdea = 3 });
        patchRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var deck = await db.Decks.FindAsync(Seed.AdminDeckId);
            deck!.Game = "Magic The Gathering";
            deck.Format = "Constructed";
            await db.SaveChangesAsync();
        });

        var validation = await client.GetFromJsonAsync<MtgDeckValidationResponse>(
            $"/api/decks/{Seed.AdminDeckId}/mtg-validation");

        validation.Should().NotBeNull();
        validation!.MainDeckCount.Should().Be(2);
        validation.Violations.Should().Contain(v => v.Contains("exceeds the limit of 4"));
    }

    [Fact]
    public async Task AddMissingToWishlist_AddsDeckSourcedEntries_AndIsIdempotent()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        // Put 6x LightningAlphaPrintingId in AdminDeckId (Seed owns 2 of Alpha => 4 missing)
        var upsertRes = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new
        {
            printingId = Seed.LightningAlphaPrintingId,
            qty = 6
        });
        upsertRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // First call adds missing copies to wishlist tagged with AdminDeckId
        var res1 = await client.PostAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/wishlist-missing",
            new { cardPrintingId = Seed.LightningAlphaPrintingId, includeProxies = false });
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<AddMissingToWishlistResponse>();
        body1!.CardsUpdated.Should().Be(1);
        body1.TotalCopiesNeeded.Should().Be(4);

        // Second call is idempotent (WishlistSource stays 4, not 8)
        var res2 = await client.PostAsJsonAsync(
            $"/api/decks/{Seed.AdminDeckId}/wishlist-missing",
            new { cardPrintingId = Seed.LightningAlphaPrintingId, includeProxies = false });
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        var wishlistPage = await client.GetFromJsonAsync<Paged<WishlistItemResponse>>(
            $"/api/wishlist?cardPrintingId={Seed.LightningAlphaPrintingId}");
        var item = wishlistPage!.Items.Should().ContainSingle().Subject;
        item.QuantityWanted.Should().Be(4);
        item.Sources.Should().ContainSingle(s => s.DeckId == Seed.AdminDeckId && s.Quantity == 4);
    }
}
