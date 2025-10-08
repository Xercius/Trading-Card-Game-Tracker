using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using api.Data;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;


namespace api.Tests;

public class ImportExportControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private record ExportDeckCard(int CardPrintingId, int InDeck, int Idea, int Acquire, int Proxy);
    private record ExportDeck(string Game, string Name, string? Description, List<ExportDeckCard> Cards);
    private record ExportCollectionItem(int CardPrintingId, int QtyOwned, int QtyProxyOwned);
    private record ExportWishlistItem(int CardPrintingId, int Qty);
    private record ExportPayload(int Version, JsonElement? User, List<ExportCollectionItem> Collection, List<ExportWishlistItem> Wishlist, List<ExportDeck> Decks);

    [Fact]
    public async Task ImportExport_ExportJsonAndCsvThenReplaceImport_RestoresState()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var exportResponse = await client.GetAsync("/api/export/json");
        exportResponse.EnsureSuccessStatusCode();
        var exportJson = await exportResponse.Content.ReadAsStringAsync();

        var payload = JsonSerializer.Deserialize<ExportPayload>(exportJson, JsonOptions);
        Assert.NotNull(payload);

        var collectionCsvResponse = await client.GetAsync("/api/export/collection.csv");
        collectionCsvResponse.EnsureSuccessStatusCode();
        var collectionCsv = await collectionCsvResponse.Content.ReadAsStringAsync();
        Assert.Contains("CardPrintingId", collectionCsv);
        Assert.Contains(TestDataSeeder.LightningBoltAlphaPrintingId.ToString(), collectionCsv);

        var wishlistCsvResponse = await client.GetAsync("/api/export/wishlist.csv");
        wishlistCsvResponse.EnsureSuccessStatusCode();
        var wishlistCsv = await wishlistCsvResponse.Content.ReadAsStringAsync();
        Assert.Contains("Quantity", wishlistCsv);
        Assert.Contains(TestDataSeeder.LightningBoltBetaPrintingId.ToString(), wishlistCsv);

        var decksCsvResponse = await client.GetAsync("/api/export/decks.csv");
        decksCsvResponse.EnsureSuccessStatusCode();
        var decksCsv = await decksCsvResponse.Content.ReadAsStringAsync();
        Assert.Contains("DeckName", decksCsv);
        Assert.Contains("Alice Aggro", decksCsv);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cards = db.UserCards.Where(uc => uc.UserId == TestDataSeeder.AliceUserId);
            db.UserCards.RemoveRange(cards);
            var decks = db.Decks.Where(d => d.UserId == TestDataSeeder.AliceUserId);
            db.Decks.RemoveRange(decks);
            await db.SaveChangesAsync();
        }

        using var replaceDoc = JsonDocument.Parse(exportJson);
        var importResponse = await client.PostAsync(
            "/api/import/json?mode=replace",
            JsonContent.Create(replaceDoc.RootElement.Clone()));
        importResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var expectedCards = payload!.Collection.ToDictionary(
                c => c.CardPrintingId,
                c => new UserCardExpectation(c.QtyOwned, c.QtyProxyOwned, Wishlist: 0));

            foreach (var wish in payload.Wishlist)
            {
                if (!expectedCards.TryGetValue(wish.CardPrintingId, out var state))
                {
                    state = new UserCardExpectation(0, 0, wish.Qty);
                    expectedCards[wish.CardPrintingId] = state;
                }
                else
                {
                    expectedCards[wish.CardPrintingId] = state with { Wishlist = wish.Qty };
                }
            }

            var userCards = await db.UserCards
                .Where(uc => uc.UserId == TestDataSeeder.AliceUserId)
                .OrderBy(uc => uc.CardPrintingId)
                .ToListAsync();

            Assert.Equal(expectedCards.Count, userCards.Count);
            foreach (var row in userCards)
            {
                var expected = expectedCards[row.CardPrintingId];
                Assert.Equal(expected.Owned, row.QuantityOwned);
                Assert.Equal(expected.Proxy, row.QuantityProxyOwned);
                Assert.Equal(expected.Wishlist, row.QuantityWanted);
            }

            var decks = await db.Decks
                .Where(d => d.UserId == TestDataSeeder.AliceUserId)
                .Include(d => d.Cards)
                .ToListAsync();

            Assert.Equal(payload.Decks.Count, decks.Count);
            foreach (var exportedDeck in payload.Decks)
            {
                var deck = Assert.Single(decks.Where(d => d.Game == exportedDeck.Game && d.Name == exportedDeck.Name));
                Assert.Equal(exportedDeck.Description, deck.Description);

                var expectedDeckCards = exportedDeck.Cards.OrderBy(c => c.CardPrintingId).ToList();
                var actualDeckCards = deck.Cards.OrderBy(c => c.CardPrintingId).ToList();
                Assert.Equal(expectedDeckCards.Count, actualDeckCards.Count);

                for (var i = 0; i < expectedDeckCards.Count; i++)
                {
                    var expected = expectedDeckCards[i];
                    var actual = actualDeckCards[i];
                    Assert.Equal(expected.CardPrintingId, actual.CardPrintingId);
                    Assert.Equal(expected.InDeck, actual.QuantityInDeck);
                    Assert.Equal(expected.Idea, actual.QuantityIdea);
                    Assert.Equal(expected.Acquire, actual.QuantityAcquire);
                    Assert.Equal(expected.Proxy, actual.QuantityProxy);
                }
            }
        }
    }

    [Fact]
    public async Task ImportExport_ExportJsonThenMergeImport_AddsDataInsteadOfReplacing()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var exportResponse = await client.GetAsync("/api/export/json");
        exportResponse.EnsureSuccessStatusCode();
        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<ExportPayload>(exportJson, JsonOptions);
        Assert.NotNull(payload);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alphaCard = await db.UserCards.SingleAsync(
                uc => uc.UserId == TestDataSeeder.AliceUserId && uc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            alphaCard.QuantityOwned = 1;
            alphaCard.QuantityProxyOwned = 0;
            alphaCard.QuantityWanted = 0;

            var betaCard = await db.UserCards.SingleAsync(
                uc => uc.UserId == TestDataSeeder.AliceUserId && uc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            betaCard.QuantityOwned = 7;
            betaCard.QuantityProxyOwned = 1;
            betaCard.QuantityWanted = 5;

            var lorcanaDeck = await db.Decks.SingleAsync(d => d.Id == TestDataSeeder.AliceLorcanaDeckId);
            db.Decks.Remove(lorcanaDeck);

            var alphaDeckCard = await db.DeckCards.SingleAsync(dc =>
                dc.DeckId == TestDataSeeder.AliceMagicDeckId && dc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            alphaDeckCard.QuantityInDeck = 1;
            alphaDeckCard.QuantityIdea = 0;
            alphaDeckCard.QuantityAcquire = 0;
            alphaDeckCard.QuantityProxy = 0;

            var betaDeckCard = await db.DeckCards.SingleAsync(dc =>
                dc.DeckId == TestDataSeeder.AliceMagicDeckId && dc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            betaDeckCard.QuantityInDeck = 3;
            betaDeckCard.QuantityIdea = 1;
            betaDeckCard.QuantityAcquire = 5;
            betaDeckCard.QuantityProxy = 2;

            await db.SaveChangesAsync();
        }

        using var mergeDoc = JsonDocument.Parse(exportJson);
        var importResponse = await client.PostAsync(
            "/api/import/json?mode=merge",
            JsonContent.Create(mergeDoc.RootElement.Clone()));
        importResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var exportedCollection = payload!.Collection.ToDictionary(c => c.CardPrintingId);
            var exportedWishlist = payload.Wishlist.ToDictionary(w => w.CardPrintingId, w => w.Qty);
            var exportedAggroDeck = payload.Decks.Single(d => d.Game == "Magic" && d.Name == "Alice Aggro");
            var exportedAlphaDeckCard = exportedAggroDeck.Cards.Single(c => c.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            var exportedBetaDeckCard = exportedAggroDeck.Cards.Single(c => c.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);

            var alphaCard = await db.UserCards.SingleAsync(
                uc => uc.UserId == TestDataSeeder.AliceUserId && uc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            Assert.Equal(1 + exportedCollection[TestDataSeeder.LightningBoltAlphaPrintingId].QtyOwned, alphaCard.QuantityOwned);
            Assert.Equal(0 + exportedCollection[TestDataSeeder.LightningBoltAlphaPrintingId].QtyProxyOwned, alphaCard.QuantityProxyOwned);
            Assert.Equal(exportedWishlist[TestDataSeeder.LightningBoltAlphaPrintingId], alphaCard.QuantityWanted);

            var betaCard = await db.UserCards.SingleAsync(
                uc => uc.UserId == TestDataSeeder.AliceUserId && uc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            Assert.Equal(7 + exportedCollection[TestDataSeeder.LightningBoltBetaPrintingId].QtyOwned, betaCard.QuantityOwned);
            Assert.Equal(1 + exportedCollection[TestDataSeeder.LightningBoltBetaPrintingId].QtyProxyOwned, betaCard.QuantityProxyOwned);
            Assert.Equal(5, betaCard.QuantityWanted);

            var alphaDeckCard = await db.DeckCards.SingleAsync(dc =>
                dc.DeckId == TestDataSeeder.AliceMagicDeckId && dc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            Assert.Equal(1 + exportedAlphaDeckCard.InDeck, alphaDeckCard.QuantityInDeck);
            Assert.Equal(exportedAlphaDeckCard.Idea, alphaDeckCard.QuantityIdea);
            Assert.Equal(exportedAlphaDeckCard.Acquire, alphaDeckCard.QuantityAcquire);
            Assert.Equal(exportedAlphaDeckCard.Proxy, alphaDeckCard.QuantityProxy);

            var betaDeckCard = await db.DeckCards.SingleAsync(dc =>
                dc.DeckId == TestDataSeeder.AliceMagicDeckId && dc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            Assert.Equal(3 + exportedBetaDeckCard.InDeck, betaDeckCard.QuantityInDeck);
            Assert.Equal(1 + exportedBetaDeckCard.Idea, betaDeckCard.QuantityIdea);
            Assert.Equal(5 + exportedBetaDeckCard.Acquire, betaDeckCard.QuantityAcquire);
            Assert.Equal(2 + exportedBetaDeckCard.Proxy, betaDeckCard.QuantityProxy);

            var lorcanaDeck = await db.Decks
                .Include(d => d.Cards)
                .SingleOrDefaultAsync(d => d.UserId == TestDataSeeder.AliceUserId && d.Game == "Lorcana" && d.Name == "Alice Control");
            Assert.NotNull(lorcanaDeck);

            var exportedLorcanaDeck = payload.Decks.Single(d => d.Game == "Lorcana" && d.Name == "Alice Control");
            Assert.Equal(exportedLorcanaDeck.Description, lorcanaDeck!.Description);
            Assert.Equal(exportedLorcanaDeck.Cards.Count, lorcanaDeck.Cards.Count);
        }
    }

    [Fact]
    public async Task ImportJson_WithUnknownCardPrintingId_ReturnsValidationProblem()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var payload = new
        {
            version = 1,
            collection = new[]
            {
                new { cardPrintingId = 999999, qtyOwned = 1, qtyProxyOwned = 0 }
            },
            wishlist = Array.Empty<object>(),
            decks = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/import/json?mode=merge", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);

        var cardPrintingErrors = Assert.Contains("cardPrintingId", problem!.Errors);
        var message = Assert.Single(cardPrintingErrors);
        Assert.Contains("999999", message);
    }

    private record UserCardExpectation(int Owned, int Proxy, int Wishlist);
}
