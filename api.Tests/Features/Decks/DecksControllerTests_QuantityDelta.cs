using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api.Tests.Fixtures;
using api.Tests.Helpers;
using Xunit;

namespace api.Tests.Features.Decks;

public class DecksControllerTests_QuantityDelta(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
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
    public async Task QuantityDelta_AdjustsQuantitiesAndClampsAtZero()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deckId = TestDataSeeder.AliceMagicDeckId;
        var printingId = TestDataSeeder.LightningBoltBetaPrintingId;

        var increase = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta",
            new { printingId, qtyDelta = 1 }
        );
        Assert.Equal(HttpStatusCode.OK, increase.StatusCode);

        var increasedRow = await increase.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(increasedRow);
        Assert.Equal(2, increasedRow!.QuantityInDeck);
        Assert.Equal(0, increasedRow.Availability);
        Assert.Equal(0, increasedRow.AvailabilityWithProxies);

        var removal = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta",
            new { printingId, qtyDelta = -10 }
        );
        Assert.Equal(HttpStatusCode.OK, removal.StatusCode);

        var removalRow = await removal.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.Null(removalRow);

        var cards = await client.GetFromJsonAsync<List<DeckCardAvailabilityDto>>(
            $"/api/decks/{deckId}/cards-with-availability",
            JsonOptions
        );
        Assert.DoesNotContain(cards ?? [], row => row.PrintingId == printingId);
    }

    [Fact]
    public async Task QuantityDelta_WithIncludeProxiesReturnsProxyAvailability()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deckId = TestDataSeeder.AliceMagicDeckId;
        var printingId = TestDataSeeder.LightningBoltAlphaPrintingId;

        var response = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/quantity-delta?includeProxies=true",
            new { printingId, qtyDelta = 0 }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var row = await response.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(row);
        Assert.Equal(4, row!.QuantityInDeck);
        Assert.Equal(1, row.Availability);
        Assert.Equal(2, row.AvailabilityWithProxies);
    }

    [Fact]
    public async Task LegacyEndpoint_StillProcessesDeltaRequests()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceMagicDeckId}/cards",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, qtyDelta = -1 }
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
