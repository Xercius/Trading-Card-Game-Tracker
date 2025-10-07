using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Data;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Value;

public sealed class ValueHistoryTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private sealed record DailyPoint(string d, decimal? v);

    [Fact]
    public async Task CollectionHistory_AggregatesDailyTotalsAndSkipsProxyOnlyPrintings()
    {
        await factory.ResetDatabaseAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var dayMinusTwo = today.AddDays(-2);
        var dayMinusOne = today.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.CardPriceHistories.AddRange(
                new CardPriceHistory
                {
                    CardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    CapturedAt = dayMinusTwo,
                    Price = 1.00m
                },
                new CardPriceHistory
                {
                    CardPrintingId = TestDataSeeder.ElsaPrintingId,
                    CapturedAt = dayMinusTwo,
                    Price = 2.00m
                },
                new CardPriceHistory
                {
                    CardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    CapturedAt = dayMinusOne,
                    Price = 1.50m
                },
                new CardPriceHistory
                {
                    CardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                    CapturedAt = dayMinusOne,
                    Price = 99.00m
                });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync("/api/collection/value/history?days=5");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<DailyPoint>>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);

        var totalsByDate = payload!
            .Where(p => p.v.HasValue)
            .ToDictionary(p => DateOnly.Parse(p.d), p => p.v!.Value);

        Assert.True(totalsByDate.ContainsKey(dayMinusTwo));
        Assert.True(totalsByDate.ContainsKey(dayMinusOne));

        // Alice owns five alpha copies and one Elsa copy; Lightning Bolt Beta is proxy-only.
        Assert.Equal(7.00m, totalsByDate[dayMinusTwo]);
        Assert.Equal(7.50m, totalsByDate[dayMinusOne]);
    }

    [Fact]
    public async Task DeckHistory_ExcludesProxyStylesAndUsesQuantities()
    {
        await factory.ResetDatabaseAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var captureDate = today.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var proxyPrinting = new CardPrinting
            {
                CardId = TestDataSeeder.LightningBoltCardId,
                Set = "Proxy Trials",
                Number = "PX1",
                Rarity = "Common",
                Style = "Proxy",
                ImageUrl = null
            };
            db.CardPrintings.Add(proxyPrinting);
            await db.SaveChangesAsync();

            db.DeckCards.Add(new DeckCard
            {
                DeckId = TestDataSeeder.AliceMagicDeckId,
                CardPrintingId = proxyPrinting.Id,
                QuantityInDeck = 3,
                QuantityProxy = 3
            });

            db.CardPriceHistories.AddRange(
                new CardPriceHistory
                {
                    CardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    CapturedAt = captureDate,
                    Price = 2.00m
                },
                new CardPriceHistory
                {
                    CardPrintingId = proxyPrinting.Id,
                    CapturedAt = captureDate,
                    Price = 5.00m
                });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var response = await client.GetAsync($"/api/decks/{TestDataSeeder.AliceMagicDeckId}/value/history?days=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<DailyPoint>>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);

        var totalsByDate = payload!
            .Where(p => p.v.HasValue)
            .ToDictionary(p => DateOnly.Parse(p.d), p => p.v!.Value);

        Assert.True(totalsByDate.TryGetValue(captureDate, out var deckValue));
        // Deck has four Alpha copies at $2 each; proxy printing should be skipped entirely.
        Assert.Equal(8.00m, deckValue);
    }

    [Fact]
    public async Task AdminIngest_IsIdempotentOnExistingRows()
    {
        await factory.ResetDatabaseAsync();

        var captureDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, capturedAt = captureDate, price = 12.34m },
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, capturedAt = captureDate, price = 12.34m }
        };

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var first = await client.PostAsJsonAsync("/api/admin/prices/ingest", payload);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/prices/ingest", payload);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entries = await db.CardPriceHistories
            .Where(h => h.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId)
            .ToListAsync();

        Assert.Single(entries);
        Assert.Equal(12.34m, entries[0].Price);
        Assert.Equal(captureDate, entries[0].CapturedAt);
    }
}
