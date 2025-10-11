// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Covers /api/value endpoints including refresh and collection summary calculations.

using api.Data;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;


namespace api.Tests;

public class ValueControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Refresh_WithoutUserHeader_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsync("/api/value/refresh?game=Magic", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithNonAdminUser_IsForbidden()
    {
        var client = factory.CreateClientForUser(TestDataSeeder.BobUserId);

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1000L, source = (string?)"test" }
        };

        var res = await client.PostAsJsonAsync("/api/value/refresh?game=Magic", payload);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAdminUser_IsNoContent()
    {
        var client = factory.CreateClientForUser(TestDataSeeder.AdminUserId);

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1500L, source = (string?)"seed" }
        };

        var res = await client.PostAsJsonAsync("/api/value/refresh?game=Magic", payload);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_IsUnauthorized()
    {
        var client = factory.CreateClient();

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1000L, source = (string?)"test" }
        };

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
        var res = await client.PostAsJsonAsync("/api/value/refresh?game=Magic", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private sealed record CollectionSummaryResponse(long totalCents, GameSliceResponse[] byGame);

    private sealed record GameSliceResponse(string game, long cents);

    private sealed record DeckSummaryResponse(int deckId, long totalCents);

    [Fact]
    public async Task Value_Refresh_CountsDuplicateValidRowsAndInvalidOnesSeparately()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1000L, source = (string?)"test" },
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1100L, source = (string?)null },
            new { cardPrintingId = 999999, priceCents = 9999L, source = (string?)"invalid" },
            new { cardPrintingId = TestDataSeeder.ElsaPrintingId, priceCents = 2000L, source = (string?)"wrong-game" }
        };


        var response = await client.PostAsJsonAsync("/api/value/refresh?game=Magic", payload);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
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

        var invalidCount = await db.ValueHistories.CountAsync(v => v.ScopeType == ValueScopeType.CardPrinting && v.ScopeId == TestDataSeeder.ElsaPrintingId);
        Assert.Equal(0, invalidCount);
    }

    [Fact]
    public async Task Value_CollectionSummary_UsesLatestPricesPerGame()
    {
        await factory.ResetDatabaseAsync();
        using var adminClient = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var magicPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 1234L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.GoblinGuidePrintingId, priceCents = 4321L, source = "initial" }
        };
        var magicResponse = await adminClient.PostAsJsonAsync("/api/value/refresh?game=Magic", magicPrices);
        Assert.Equal(HttpStatusCode.NoContent, magicResponse.StatusCode);

        var lorcanaPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.ElsaPrintingId, priceCents = 5678L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.MickeyPrintingId, priceCents = 8765L, source = "initial" }
        };
        var lorcanaResponse = await adminClient.PostAsJsonAsync("/api/value/refresh?game=Lorcana", lorcanaPrices);
        Assert.Equal(HttpStatusCode.NoContent, lorcanaResponse.StatusCode);

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var summary = await client.GetFromJsonAsync<CollectionSummaryResponse>("/api/value/collection/summary");

        Assert.NotNull(summary);
        var expectedMagic = 1234L * 5; // Alice owns five Lightning Bolt Alpha copies
        var expectedLorcana = 5678L * 1; // Alice owns one Elsa printing
        Assert.Equal(expectedMagic + expectedLorcana, summary!.totalCents);

        var magicSlice = Assert.Single(summary.byGame, s => s.game == "Magic");
        Assert.Equal(expectedMagic, magicSlice.cents);

        var lorcanaSlice = Assert.Single(summary.byGame, s => s.game == "Lorcana");
        Assert.Equal(expectedLorcana, lorcanaSlice.cents);
    }

    [Fact]
    public async Task Value_DeckValue_UsesLatestPricesForCardsInDeck()
    {
        await factory.ResetDatabaseAsync();
        using var adminClient = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var initialMagicPrices = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 200L, source = "initial" },
            new { cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId, priceCents = 300L, source = "initial" }
        };
        var initResponse = await adminClient.PostAsJsonAsync("/api/value/refresh?game=Magic", initialMagicPrices);
        Assert.Equal(HttpStatusCode.NoContent, initResponse.StatusCode);

        var updatedAlphaPrice = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, priceCents = 250L, source = "update" }
        };
        var updateResponse = await adminClient.PostAsJsonAsync("/api/value/refresh?game=Magic", updatedAlphaPrice);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var deckValue = await client.GetFromJsonAsync<DeckSummaryResponse>(
            $"/api/value/deck/{TestDataSeeder.AliceMagicDeckId}");

        Assert.NotNull(deckValue);
        var expectedTotal = 250L * 4 + 300L * 1; // Deck has four Alpha and one Beta copies
        Assert.Equal(expectedTotal, deckValue!.totalCents);
    }

    [Fact]
    public async Task Value_DeckValue_ReturnsNotFound_ForMissingDeck()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync("/api/value/deck/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Value_DeckValue_Forbids_WhenNotOwner()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.BobUserId);
        var response = await client.GetAsync($"/api/value/deck/{TestDataSeeder.AliceMagicDeckId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Value_DeckValue_AllowsAdminForAnyDeck()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);
        var response = await client.GetAsync($"/api/value/deck/{TestDataSeeder.AliceMagicDeckId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
