// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Covers /api/value endpoints including refresh and collection summary calculations.

using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Data;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests;

public class ValueControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ValueControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed record RefreshResponse(int inserted, int ignored);

    private sealed record CollectionSummaryResponse(long totalCents, GameSliceResponse[] byGame);

    private sealed record GameSliceResponse(string game, long cents);

    private sealed record DeckSummaryResponse(int deckId, long totalCents);

    [Fact]
    public async Task Value_Refresh_CountsDuplicateValidRowsAndInvalidOnesSeparately()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1000L, source = (string?)"test" },
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1100L, source = (string?)null },
            new { cardPrintingId = 999999, priceCents = 9999L, source = (string?)"invalid" },
            new { cardPrintingId = TestDataSeeder.ElsaPrintingId, priceCents = 2000L, source = (string?)"wrong-game" }
        };


        var response = await client.PostAsJsonAsync("/api/value/refresh?game=Magic", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.inserted);
        Assert.Equal(2, result.ignored);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var histories = await db.ValueHistories
            .Where(v => v.ScopeType == ValueScopeType.CardPrinting && v.ScopeId == TestDataSeeder.LightningBoltAlphaPrintingId)
            .OrderBy(v => v.PriceCents)
            .ToListAsync();

        Assert.Equal(2, histories.Count);
        Assert.Collection(
            histories,
            h => Assert.Equal(1000L, h.PriceCents),
            h => Assert.Equal(1100L, h.PriceCents));
    }

    [Fact]
    public async Task Value_CollectionSummary_UsesLatestPricesPerGame()
    {
        await _factory.ResetDatabaseAsync();
        using var unauthenticated = _factory.CreateClient();

        var magicPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1234L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.GoblinGuidePrintingId, priceCents = 4321L, source = "initial" }
        };
        var magicResponse = await unauthenticated.PostAsJsonAsync("/api/value/refresh?game=Magic", magicPrices);
        magicResponse.EnsureSuccessStatusCode();

        var lorcanaPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.ElsaPrintingId, priceCents = 5678L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.MickeyPrintingId, priceCents = 8765L, source = "initial" }
        };
        var lorcanaResponse = await unauthenticated.PostAsJsonAsync("/api/value/refresh?game=Lorcana", lorcanaPrices);
        lorcanaResponse.EnsureSuccessStatusCode();

        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var summary = await client.GetFromJsonAsync<CollectionSummaryResponse>("/api/value/collection/summary");

        Assert.NotNull(summary);
        var expectedMagic = 1234L * 5; // Alice owns five Lightning Bolt Alpha copies
        var expectedLorcana = 5678L * 1; // Alice owns one Elsa printing
        Assert.Equal(expectedMagic + expectedLorcana, summary!.totalCents);

        var magicSlice = Assert.Single(summary.byGame.Where(s => s.game == "Magic"));
        Assert.Equal(expectedMagic, magicSlice.cents);

        var lorcanaSlice = Assert.Single(summary.byGame.Where(s => s.game == "Lorcana"));
        Assert.Equal(expectedLorcana, lorcanaSlice.cents);
    }

    [Fact]
    public async Task Value_DeckValue_UsesLatestPricesForCardsInDeck()
    {
        await _factory.ResetDatabaseAsync();
        using var unauthenticated = _factory.CreateClient();

        var initialMagicPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 200L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId, priceCents = 300L, source = "initial" }
        };
        var initResponse = await unauthenticated.PostAsJsonAsync("/api/value/refresh?game=Magic", initialMagicPrices);
        initResponse.EnsureSuccessStatusCode();

        var updatedAlphaPrice = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 250L, source = "update" }
        };
        var updateResponse = await unauthenticated.PostAsJsonAsync("/api/value/refresh?game=Magic", updatedAlphaPrice);
        updateResponse.EnsureSuccessStatusCode();

        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var deckValue = await client.GetFromJsonAsync<DeckSummaryResponse>(
            $"/api/value/deck/{TestDataSeeder.AliceMagicDeckId}");

        Assert.NotNull(deckValue);
        var expectedTotal = 250L * 4 + 300L * 1; // Deck has four Alpha and one Beta copies
        Assert.Equal(expectedTotal, deckValue!.totalCents);
    }
}
