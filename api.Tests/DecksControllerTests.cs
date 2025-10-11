using api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests;

public class DecksControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record DeckCardAvailabilityDto(
        int PrintingId,
        string CardName,
        string? ImageUrl,
        int QuantityInDeck,
        int Availability,
        int AvailabilityWithProxies);

    [Fact]
    public async Task CardsWithAvailability_ReturnsRows()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var rows = await GetRowsAsync(client, TestDataSeeder.AliceMagicDeckId);

        Assert.Equal(2, rows.Count);

        var alpha = Assert.Single(rows, r => r.PrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Equal("Lightning Bolt", alpha.CardName);
        Assert.Equal(4, alpha.QuantityInDeck);
        Assert.Equal(1, alpha.Availability);
        Assert.Equal(1, alpha.AvailabilityWithProxies);

        var beta = Assert.Single(rows, r => r.PrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
        Assert.Equal(1, beta.QuantityInDeck);
        Assert.Equal(0, beta.Availability);
        Assert.Equal(0, beta.AvailabilityWithProxies);
    }

    [Fact]
    public async Task CardsWithAvailability_IncludingProxiesUsesProxyCounts()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var rows = await GetRowsAsync(client, TestDataSeeder.AliceMagicDeckId, includeProxies: true);

        var beta = Assert.Single(rows, r => r.PrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
        Assert.Equal(0, beta.Availability);
        Assert.Equal(1, beta.AvailabilityWithProxies);
    }

    [Fact]
    public async Task DeckCardsDelta_UpdatesQuantitiesAndReflectsInAggregate()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var initial = await GetRowsAsync(client, TestDataSeeder.AliceMagicDeckId);
        var alphaBefore = Assert.Single(initial, r => r.PrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Equal(1, alphaBefore.Availability);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceMagicDeckId}/cards/quantity-delta",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, qtyDelta = 1 }
        );
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var addRow = await addResponse.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(addRow);
        Assert.Equal(5, addRow!.QuantityInDeck);

        var afterAdd = await GetRowsAsync(client, TestDataSeeder.AliceMagicDeckId);
        var alphaAfterAdd = Assert.Single(afterAdd, r => r.PrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Equal(5, alphaAfterAdd.QuantityInDeck);
        Assert.Equal(0, alphaAfterAdd.Availability);

        var failResponse = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceMagicDeckId}/cards/quantity-delta",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, qtyDelta = 1 }
        );
        Assert.Equal(HttpStatusCode.BadRequest, failResponse.StatusCode);

        var subtractResponse = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceMagicDeckId}/cards/quantity-delta",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, qtyDelta = -2 }
        );
        Assert.Equal(HttpStatusCode.OK, subtractResponse.StatusCode);
        var subtractRow = await subtractResponse.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(subtractRow);
        Assert.Equal(3, subtractRow!.QuantityInDeck);

        var afterSubtract = await GetRowsAsync(client, TestDataSeeder.AliceMagicDeckId);
        var alphaAfterSubtract = Assert.Single(afterSubtract, r => r.PrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Equal(3, alphaAfterSubtract.QuantityInDeck);
        Assert.Equal(2, alphaAfterSubtract.Availability);
    }

    private static async Task<List<DeckCardAvailabilityDto>> GetRowsAsync(HttpClient client, int deckId, bool includeProxies = false)
    {
        var url = $"/api/decks/{deckId}/cards-with-availability";
        if (includeProxies)
        {
            url += "?includeProxies=true";
        }

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<DeckCardAvailabilityDto>>(JsonOptions);
        return rows ?? new List<DeckCardAvailabilityDto>();
    }
}
