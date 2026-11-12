using api.Features.Collections.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests;

public class CollectionSetSummaryTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetSetsSummary_ReturnsAggregatedSetAndTreatmentData()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.UserId);

        var response = await client.GetAsync("/api/collection/sets-summary?game=Magic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<CollectionSetSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.NotEmpty(summary.Sets);

        // Verify Magic sets (Alpha, Beta, Zendikar)
        var alpha = summary.Sets.FirstOrDefault(s => s.SetName == "Alpha");
        Assert.NotNull(alpha);
        Assert.Equal(1, alpha.TotalCards);
        Assert.Equal(1, alpha.OwnedCards);
        Assert.Equal(100.0, alpha.CompletionPercentage);

        // Verify Treatment subsets
        Assert.NotEmpty(alpha.Subsets);
        var stdSubset = alpha.Subsets.FirstOrDefault(ss => ss.TreatmentName == "Standard");
        Assert.NotNull(stdSubset);
        Assert.Equal(1, stdSubset.TotalCards);
        Assert.Equal(1, stdSubset.OwnedCards);
        Assert.Equal(100.0, stdSubset.CompletionPercentage);
    }

    [Theory]
    [InlineData(false, 2, 66.7, 3, 50.0)]
    [InlineData(true, 3, 100.0, 4, 66.7)]
    public async Task GetSetsSummary_MultipleTreatments_CountsEachPrintingOnce(
        bool ownsFoil,
        int expectedSetOwned,
        double expectedSetCompletion,
        int expectedGameOwned,
        double expectedGameCompletion)
    {
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var standard = await db.CardPrintings.Include(p => p.Card)
                .SingleAsync(p => p.Id == TestDataSeeder.LightningBoltAlphaPrintingId);
            Assert.NotNull(standard.Card.GameId);

            var hyperspace = new Treatment { GameId = standard.Card.GameId.Value, Name = "Hyperspace" };
            var hyperspacePrinting = new CardPrinting
            {
                CardId = standard.CardId,
                SetId = standard.SetId,
                Number = "A2",
                Treatment = hyperspace
            };
            var foilPrinting = new CardPrinting
            {
                CardId = standard.CardId,
                SetId = standard.SetId,
                Number = "A3",
                Treatment = hyperspace,
                IsFoil = true
            };
            db.UserCards.AddRange(
                new UserCard
                {
                    UserId = TestDataSeeder.UserId,
                    CardPrinting = hyperspacePrinting,
                    QuantityOwned = 3
                },
                new UserCard
                {
                    UserId = TestDataSeeder.UserId,
                    CardPrinting = foilPrinting,
                    QuantityOwned = ownsFoil ? 2 : 0,
                    QuantityProxyOwned = 2,
                    QuantityWanted = 1
                });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.UserId);
        var response = await client.GetAsync("/api/collection/sets-summary?game=Magic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<CollectionSetSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        var alpha = Assert.Single(summary.Sets, s => s.SetName == "Alpha");
        Assert.Equal(3, alpha.TotalCards);
        Assert.Equal(expectedSetOwned, alpha.OwnedCards);
        Assert.Equal(expectedSetCompletion, alpha.CompletionPercentage);
        Assert.Equal(alpha.TotalCards, alpha.Subsets.Sum(s => s.TotalCards));
        Assert.Equal(alpha.OwnedCards, alpha.Subsets.Sum(s => s.OwnedCards));

        var hyperspaceSubset = Assert.Single(alpha.Subsets, s => s.TreatmentName == "Hyperspace");
        Assert.Equal(2, hyperspaceSubset.TotalCards);
        Assert.Equal(ownsFoil ? 2 : 1, hyperspaceSubset.OwnedCards);
        Assert.Equal(ownsFoil ? 100.0 : 50.0, hyperspaceSubset.CompletionPercentage);

        Assert.Equal(6, summary.TotalUniqueCardsInGame);
        Assert.Equal(expectedGameOwned, summary.TotalUniqueCardsOwned);
        Assert.Equal(expectedGameCompletion, summary.OverallCompletionPercentage);
        Assert.Equal(summary.Sets.Sum(s => s.TotalCards), summary.TotalUniqueCardsInGame);
        Assert.Equal(summary.Sets.Sum(s => s.OwnedCards), summary.TotalUniqueCardsOwned);
    }

    [Fact]
    public async Task GetSetsSummary_WhenGameDoesNotMatch_ReturnsNoResults()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.UserId);

        var response = await client.GetAsync("/api/collection/sets-summary?game=Unknown Game");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<CollectionSetSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Empty(summary.Sets);
        Assert.Equal(0, summary.TotalUniqueCardsInGame);
        Assert.Equal(0, summary.TotalUniqueCardsOwned);
    }

    [Fact]
    public async Task GetSetsSummary_SwccgVirtualSet_CountsProxiesTowardCompletion()
    {
        await factory.ResetDatabaseAsync();

        // Seed an SWCCG Virtual set with proxy-only ownership
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<api.Data.AppDbContext>();
            var swccgGame = new Game { Name = "Star Wars Customizable Card Game" };
            db.Games.Add(swccgGame);
            await db.SaveChangesAsync();

            var vSet = new Set
            {
                Code = "V1",
                Name = "Virtual Set 1",
                GameId = swccgGame.Id
            };
            db.Sets.Add(vSet);
            await db.SaveChangesAsync();

            var card = new Card
            {
                Name = "Virtual Luke",
                GameId = swccgGame.Id
            };
            db.Cards.Add(card);
            await db.SaveChangesAsync();

            var printing = new CardPrinting
            {
                CardId = card.Id,
                SetId = vSet.Id,
                Number = "1"
            };
            db.CardPrintings.Add(printing);
            await db.SaveChangesAsync();

            // Add user card with 0 owned, 2 proxy
            db.UserCards.Add(new UserCard
            {
                UserId = TestDataSeeder.UserId,
                CardPrintingId = printing.Id,
                QuantityOwned = 0,
                QuantityProxyOwned = 2,
                QuantityWanted = 0
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.UserId);
        var response = await client.GetAsync("/api/collection/sets-summary?game=Star Wars Customizable Card Game");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<CollectionSetSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);

        var virtualSet = summary.Sets.FirstOrDefault(s => s.SetCode == "V1");
        Assert.NotNull(virtualSet);
        Assert.True(virtualSet.IsVirtualSet);
        Assert.Equal(1, virtualSet.TotalCards);
        Assert.Equal(1, virtualSet.OwnedCards);
        Assert.Equal(100.0, virtualSet.CompletionPercentage);
    }
}
