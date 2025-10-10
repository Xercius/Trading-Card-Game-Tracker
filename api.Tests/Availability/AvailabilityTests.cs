using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Xunit;
using api.Tests.Infrastructure;

namespace api.Tests.Availability;

public class AvailabilityTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task CollectionAvailability_AccountsForDeckAssignments()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var payload = await client.GetFromJsonAsync<PagedResponse<CollectionItemContract>>("/api/collection");
        payload.Should().NotBeNull();
        var card = payload!.Items.Should().ContainSingle(i => i.CardPrintingId == Seed.LightningBetaPrintingId).Which;
        card.QuantityOwned.Should().Be(3);
        card.Availability.Should().Be(1);
    }

    [Fact]
    public async Task DeckCardsWithAvailability_ReturnCurrentDeckSnapshot()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync($"/api/decks/{Seed.AdminDeckId}/cards-with-availability");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<DeckCardAvailabilityContract>>();
        items.Should().NotBeNull();
        var card = items!.Should().ContainSingle(i => i.PrintingId == Seed.LightningBetaPrintingId).Which;
        card.QuantityInDeck.Should().Be(2);
        card.Availability.Should().Be(1);
    }

    [Fact]
    public async Task WishlistOnlyEntries_DoNotContributeToAvailability()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var payload = await client.GetFromJsonAsync<PagedResponse<CollectionItemContract>>("/api/collection");
        payload.Should().NotBeNull();
        var card = payload!.Items.Should().ContainSingle(i => i.CardPrintingId == Seed.PhoenixPrintingId).Which;
        card.QuantityOwned.Should().Be(0);
        card.QuantityWanted.Should().Be(2);
        card.Availability.Should().Be(0);
    }

    [Fact]
    public async Task AvailabilityReachesZeroWhenUsageMatchesOwnership()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var upsert = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new
        {
            printingId = Seed.LightningBetaPrintingId,
            qty = 3
        });
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await client.GetFromJsonAsync<List<DeckCardAvailabilityContract>>($"/api/decks/{Seed.AdminDeckId}/cards-with-availability");
        snapshot.Should().NotBeNull();
        var card = snapshot!.Should().ContainSingle(i => i.PrintingId == Seed.LightningBetaPrintingId).Which;
        card.QuantityInDeck.Should().Be(3);
        card.Availability.Should().Be(0);

        var over = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/quantity-delta", new
        {
            printingId = Seed.LightningBetaPrintingId,
            qtyDelta = 1
        });
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = await over.Content.ReadAsStringAsync();
        message.Should().Contain("Insufficient availability");
    }

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    private sealed record CollectionItemContract(int CardPrintingId, int QuantityOwned, int QuantityWanted, int QuantityProxyOwned, int Availability, int AvailabilityWithProxies, int CardId, string CardName, string Game, string Set, string Number, string Rarity, string Style, string? ImageUrl);

    private sealed record DeckCardAvailabilityContract(int PrintingId, string CardName, string? ImageUrl, int QuantityInDeck, int Availability, int AvailabilityWithProxies);
}
