using System.Net;
using System.Net.Http.Json;
using api.Features.Decks.Dtos;
using api.Models;
using api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace api.Tests.Features.Decks;

public sealed class MtgDeckValidationEndpointTests(TestingWebAppFactory factory)
    : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task GetMtgDeckValidation_MtgDeck_ReturnsOkWithValidationResult()
    {
        await SeedDataAsync();
        int deckId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var deck = new Deck
            {
                UserId = Seed.AdminUserId,
                Game = "Magic The Gathering",
                Name = "Mono Red",
                Format = "Constructed"
            };
            db.Decks.Add(deck);
            await db.SaveChangesAsync();
            deckId = deck.Id;
        });

        using var client = _factory.CreateClientForUser(Seed.AdminUserId);
        var response = await client.GetAsync($"/api/decks/{deckId}/mtg-validation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MtgDeckValidationResponse>();
        payload.Should().NotBeNull();
        payload!.Format.Should().Be("Constructed");
        payload.IsValid.Should().BeFalse();
        payload.Violations.Should().Contain(v => v.Contains("at least 60 cards"));
    }

    [Fact]
    public async Task GetMtgDeckValidation_NonMtgDeck_ReturnsBadRequest()
    {
        await SeedDataAsync();
        int deckId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var deck = new Deck
            {
                UserId = Seed.AdminUserId,
                Game = "Star Wars Unlimited",
                Name = "SWU Deck",
                Format = "Premier"
            };
            db.Decks.Add(deck);
            await db.SaveChangesAsync();
            deckId = deck.Id;
        });

        using var client = _factory.CreateClientForUser(Seed.AdminUserId);
        var response = await client.GetAsync($"/api/decks/{deckId}/mtg-validation");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMtgDeckValidation_MissingDeck_ReturnsNotFound()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/decks/999999/mtg-validation");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
