using api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests.Features.Decks;

public class DecksControllerTests_Upsert(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
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
    public async Task Upsert_SetsAbsoluteQuantityAndIsIdempotent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deckId = TestDataSeeder.AliceMagicDeckId;
        var printingId = TestDataSeeder.LightningBoltAlphaPrintingId;

        var first = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/upsert",
            new { printingId, qty = 2 }
        );
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstRow = await first.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(firstRow);
        Assert.Equal(2, firstRow!.QuantityInDeck);
        Assert.Equal(3, firstRow.Availability);
        Assert.Equal(3, firstRow.AvailabilityWithProxies);

        var second = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/upsert",
            new { printingId, qty = 2 }
        );
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondRow = await second.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(secondRow);
        Assert.Equal(2, secondRow!.QuantityInDeck);
        Assert.Equal(3, secondRow.Availability);

        var tooHigh = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/upsert",
            new { printingId, qty = 6 }
        );
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);

        var withProxies = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/cards/upsert?includeProxies=true",
            new { printingId, qty = 6 }
        );
        Assert.Equal(HttpStatusCode.OK, withProxies.StatusCode);

        var withProxiesRow = await withProxies.Content.ReadFromJsonAsync<DeckCardAvailabilityDto>(JsonOptions);
        Assert.NotNull(withProxiesRow);
        Assert.Equal(6, withProxiesRow!.QuantityInDeck);
        Assert.Equal(0, withProxiesRow.Availability);
        Assert.Equal(0, withProxiesRow.AvailabilityWithProxies);
    }
}
