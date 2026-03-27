using api.Data;
using api.Features.Prices.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests;

public class PricesControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task History_ReturnsEmptyArray_WhenNoRows()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/prices/{TestDataSeeder.LightningBoltAlphaPrintingId}/history");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Points);
    }

    [Fact]
    public async Task History_ReturnsAscendingPoints_WhenDataExists()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.CardPriceHistories.AddRange(
            new CardPriceHistory
            {
                CardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                CapturedAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                Price = 15.00m
            },
            new CardPriceHistory
            {
                CardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                CapturedAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                Price = 25.00m
            });

        await db.SaveChangesAsync();

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync($"/api/prices/{TestDataSeeder.LightningBoltAlphaPrintingId}/history?days=10");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PriceHistoryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Points.Count);
        Assert.Collection(
            payload.Points,
            first =>
            {
                Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)).ToString("yyyy-MM-dd"), first.Date);
                Assert.Equal(15.00m, first.Price);
            },
            second =>
            {
                Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"), second.Date);
                Assert.Equal(25.00m, second.Price);
            });
    }

    [Fact]
    public async Task DeckValueHistory_ReturnsNotFound_ForMissingDeck()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync("/api/decks/999999/value/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeckValueHistory_AllowsOwner()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/decks/{TestDataSeeder.AliceMagicDeckId}/value/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeckValueHistory_AllowsAdmin()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);
        var response = await client.GetAsync($"/api/decks/{TestDataSeeder.AliceMagicDeckId}/value/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
