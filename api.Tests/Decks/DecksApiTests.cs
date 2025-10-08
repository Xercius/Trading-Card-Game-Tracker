using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using api.Tests.Infrastructure;

namespace api.Tests.Decks;

public class DecksApiTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task CreateDeck_ReturnsCreatedDeck()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/deck", new
        {
            game = "Magic",
            name = "Test Deck",
            description = "Integration test deck"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var deck = await response.Content.ReadFromJsonAsync<DeckResponseContract>();
        deck.Should().NotBeNull();
        deck!.UserId.Should().Be(Seed.AdminUserId);
        deck.Name.Should().Be("Test Deck");

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var stored = await db.Decks.FindAsync(deck.Id);
            stored.Should().NotBeNull();
            stored!.Name.Should().Be("Test Deck");
        });
    }

    [Fact]
    public async Task AddCardToDeck_AddsEntryAndReturnsAvailability()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new
        {
            printingId = Seed.GoblinPrintingId,
            qty = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DeckCardAvailabilityContract>();
        payload.Should().NotBeNull();
        payload!.PrintingId.Should().Be(Seed.GoblinPrintingId);
        payload.QuantityInDeck.Should().Be(1);
        payload.Availability.Should().Be(0);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var deckCard = await db.DeckCards.FirstOrDefaultAsync(dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.GoblinPrintingId);
            deckCard.Should().NotBeNull();
            deckCard!.QuantityInDeck.Should().Be(1);
        });
    }

    [Fact]
    public async Task GetDeckById_ReturnsDeckForOwner()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync($"/api/deck/{Seed.AdminDeckId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deck = await response.Content.ReadFromJsonAsync<DeckResponseContract>();
        deck.Should().NotBeNull();
        deck!.Id.Should().Be(Seed.AdminDeckId);
        deck.UserId.Should().Be(Seed.AdminUserId);
    }

    [Fact]
    public async Task GetDeckById_WithDifferentUser_ReturnsForbidden()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.SecondaryUserId);

        var response = await client.GetAsync($"/api/deck/{Seed.AdminDeckId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCardFromDeck_RemovesEntry()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        await client.PostAsJsonAsync($"/api/decks/{Seed.AdminDeckId}/cards/upsert", new { printingId = Seed.GoblinPrintingId, qty = 1 });
        var response = await client.DeleteAsync($"/api/decks/{Seed.AdminDeckId}/cards/{Seed.GoblinPrintingId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var deckCard = await db.DeckCards.FirstOrDefaultAsync(dc => dc.DeckId == Seed.AdminDeckId && dc.CardPrintingId == Seed.GoblinPrintingId);
            deckCard.Should().BeNull();
        });
    }

    private sealed record DeckResponseContract(int Id, int UserId, string Game, string Name, string? Description, DateTime CreatedUtc, DateTime? UpdatedUtc);

    private sealed record DeckCardAvailabilityContract(int PrintingId, string CardName, string? ImageUrl, int QuantityInDeck, int Availability, int AvailabilityWithProxies);
}
