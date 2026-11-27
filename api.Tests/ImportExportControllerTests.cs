using api.Data;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;


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
    private record ExportPayload(int Version, List<ExportCollectionItem> Collection, List<ExportWishlistItem> Wishlist, List<ExportDeck> Decks);

    [Fact]
    public async Task ImportExport_ExportJsonAndCsvThenReplaceImport_RestoresState()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

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
            db.UserCards.RemoveRange(db.UserCards);
            db.Decks.RemoveRange(db.Decks);
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
                .Include(d => d.Cards)
                .ToListAsync();

            Assert.Equal(payload.Decks.Count, decks.Count);
            foreach (var exportedDeck in payload.Decks)
            {
                var deck = Assert.Single(decks, d => d.Game == exportedDeck.Game && d.Name == exportedDeck.Name);
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
        using var client = factory.CreateClient();

        var exportResponse = await client.GetAsync("/api/export/json");
        exportResponse.EnsureSuccessStatusCode();
        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<ExportPayload>(exportJson, JsonOptions);
        Assert.NotNull(payload);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alphaCard = await db.UserCards.SingleAsync(
                uc => uc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            alphaCard.QuantityOwned = 1;
            alphaCard.QuantityProxyOwned = 0;
            alphaCard.QuantityWanted = 0;

            var betaCard = await db.UserCards.SingleAsync(
                uc => uc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
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
                uc => uc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            Assert.Equal(1 + exportedCollection[TestDataSeeder.LightningBoltAlphaPrintingId].QtyOwned, alphaCard.QuantityOwned);
            Assert.Equal(0 + exportedCollection[TestDataSeeder.LightningBoltAlphaPrintingId].QtyProxyOwned, alphaCard.QuantityProxyOwned);
            Assert.Equal(exportedWishlist[TestDataSeeder.LightningBoltAlphaPrintingId], alphaCard.QuantityWanted);

            var betaCard = await db.UserCards.SingleAsync(
                uc => uc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
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
                .SingleOrDefaultAsync(d => d.Game == "Lorcana" && d.Name == "Alice Control");
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
        using var client = factory.CreateClient();

        const int invalidCardPrintingId = 999999;
        var payload = new
        {
            version = 1,
            collection = new[]
            {
                new { cardPrintingId = invalidCardPrintingId, qtyOwned = 1, qtyProxyOwned = 0 }
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
        Assert.Contains(invalidCardPrintingId.ToString(), message);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasInvalidCard = await db.UserCards.AnyAsync(uc =>
                uc.CardPrintingId == invalidCardPrintingId);
            Assert.False(hasInvalidCard);
        }
    }

    [Fact]
    public async Task ExportJson_ReturnsOk()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/export/json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ImportExport_PortableNaturalKeyFallbackAndV1Fields_RestoresAccurately()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var magicDeck = await db.Decks.SingleAsync(d => d.Id == TestDataSeeder.AliceMagicDeckId);
            magicDeck.Format = "Constructed";

            var betaDeckCard = await db.DeckCards.SingleAsync(dc =>
                dc.DeckId == TestDataSeeder.AliceMagicDeckId &&
                dc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            betaDeckCard.QuantitySideboard = 2;
            betaDeckCard.IsCommander = true;
            betaDeckCard.IsStarting = true;

            db.WishlistSources.AddRange(
                new api.Models.WishlistSource
                {
                    CardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                    DeckId = null,
                    Quantity = 1,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new api.Models.WishlistSource
                {
                    CardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                    DeckId = TestDataSeeder.AliceMagicDeckId,
                    Quantity = 1,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var exportResponse = await client.GetAsync("/api/export/json");
        exportResponse.EnsureSuccessStatusCode();
        var exportedJson = await exportResponse.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(exportedJson);
        var root = doc.RootElement;

        // Verify natural keys and new fields are emitted on export
        var firstCollection = root.GetProperty("collection").EnumerateArray().First();
        Assert.False(string.IsNullOrWhiteSpace(firstCollection.GetProperty("cardName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(firstCollection.GetProperty("game").GetString()));

        // Rewrite all cardPrintingIds to bogus shifted IDs so natural-key resolution is exercised
        var shiftedPayload = new
        {
            version = 1,
            collection = root.GetProperty("collection").EnumerateArray().Select((el, i) => new
            {
                cardPrintingId = 80000 + i,
                qtyOwned = el.GetProperty("qtyOwned").GetInt32(),
                qtyProxyOwned = el.GetProperty("qtyProxyOwned").GetInt32(),
                game = el.GetProperty("game").GetString(),
                cardName = el.GetProperty("cardName").GetString(),
                setCode = el.GetProperty("setCode").GetString(),
                setName = el.GetProperty("setName").GetString(),
                number = el.GetProperty("number").GetString(),
                style = el.GetProperty("style").GetString()
            }).ToList(),
            wishlist = root.GetProperty("wishlist").EnumerateArray().Select((el, i) => new
            {
                cardPrintingId = 81000 + i,
                qty = el.GetProperty("qty").GetInt32(),
                game = el.GetProperty("game").GetString(),
                cardName = el.GetProperty("cardName").GetString(),
                setCode = el.GetProperty("setCode").GetString(),
                setName = el.GetProperty("setName").GetString(),
                number = el.GetProperty("number").GetString(),
                style = el.GetProperty("style").GetString(),
                sources = el.TryGetProperty("sources", out var s) && s.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<List<object>>(s.GetRawText(), JsonOptions)
                    : null
            }).ToList(),
            decks = root.GetProperty("decks").EnumerateArray().Select(d => new
            {
                game = d.GetProperty("game").GetString(),
                name = d.GetProperty("name").GetString(),
                description = d.GetProperty("description").GetString(),
                format = d.GetProperty("format").GetString(),
                side = d.GetProperty("side").GetString(),
                cards = d.GetProperty("cards").EnumerateArray().Select((c, idx) => new
                {
                    cardPrintingId = 82000 + idx,
                    inDeck = c.GetProperty("inDeck").GetInt32(),
                    idea = c.GetProperty("idea").GetInt32(),
                    acquire = c.GetProperty("acquire").GetInt32(),
                    proxy = c.GetProperty("proxy").GetInt32(),
                    sideboard = c.GetProperty("sideboard").GetInt32(),
                    isStarting = c.GetProperty("isStarting").GetBoolean(),
                    isCommander = c.GetProperty("isCommander").GetBoolean(),
                    cardName = c.GetProperty("cardName").GetString(),
                    setCode = c.GetProperty("setCode").GetString(),
                    setName = c.GetProperty("setName").GetString(),
                    number = c.GetProperty("number").GetString(),
                    style = c.GetProperty("style").GetString()
                }).ToList()
            }).ToList()
        };

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WishlistSources.RemoveRange(db.WishlistSources);
            db.UserCards.RemoveRange(db.UserCards);
            db.Decks.RemoveRange(db.Decks);
            await db.SaveChangesAsync();
        }

        var importResponse = await client.PostAsJsonAsync("/api/import/json?mode=replace", shiftedPayload);
        importResponse.EnsureSuccessStatusCode();
        var body = await importResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("imported").GetProperty("resolvedByNaturalKey").GetInt32() > 0);
        Assert.Equal(0, body.GetProperty("imported").GetProperty("unresolvedCount").GetInt32());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deck = await db.Decks.Include(d => d.Cards).SingleAsync(d => d.Game == "Magic" && d.Name == "Alice Aggro");
            Assert.Equal("Constructed", deck.Format);
            var restoredBeta = Assert.Single(deck.Cards, c => c.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            Assert.Equal(2, restoredBeta.QuantitySideboard);
            Assert.True(restoredBeta.IsCommander);
            Assert.True(restoredBeta.IsStarting);

            var sources = await db.WishlistSources
                .Where(ws => ws.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId)
                .ToListAsync();
            Assert.Equal(2, sources.Count);
            Assert.Contains(sources, s => s.DeckId == null && s.Quantity == 1);
            Assert.Contains(sources, s => s.DeckId == deck.Id && s.Quantity == 1);
        }
    }

    [Fact]
    public async Task ImportJson_PortablePayloadWithMissingCard_ImportsResolvableAndReportsUnresolved()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var payload = new
        {
            version = 1,
            collection = new object[]
            {
                new
                {
                    cardPrintingId = 999991,
                    qtyOwned = 3,
                    qtyProxyOwned = 0,
                    game = "Magic",
                    cardName = "Goblin Guide",
                    setCode = "Zendikar",
                    number = "Z3",
                    style = "Standard"
                },
                new
                {
                    cardPrintingId = 999992,
                    qtyOwned = 1,
                    qtyProxyOwned = 0,
                    game = "Magic",
                    cardName = "Nonexistent Card XYZ",
                    setCode = "NONE",
                    number = "999",
                    style = "Standard"
                }
            },
            wishlist = Array.Empty<object>(),
            decks = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/import/json?mode=merge", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("imported").GetProperty("collection").GetInt32());
        Assert.Equal(1, body.GetProperty("imported").GetProperty("unresolvedCount").GetInt32());
        Assert.Equal("Nonexistent Card XYZ", body.GetProperty("unresolved")[0].GetProperty("cardName").GetString());
    }

    [Fact]
    public async Task ImportJson_ReplaceModeWithInvalidLegacyId_RollsBackWithoutClearingExistingData()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var payload = new
        {
            version = 1,
            collection = new[] { new { cardPrintingId = 999999, qtyOwned = 1, qtyProxyOwned = 0 } },
            wishlist = Array.Empty<object>(),
            decks = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/import/json?mode=replace", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.UserCards.AnyAsync());
        Assert.True(await db.Decks.AnyAsync());
    }

    [Fact]
    public async Task ImportJson_DirectIdCollidesWithDifferentStyle_ResolvesByNaturalKeyStyle()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        int foilPrintingId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var basePrinting = await db.CardPrintings
                .AsNoTracking()
                .Include(cp => cp.Card)
                .SingleAsync(cp => cp.Id == TestDataSeeder.LightningBoltAlphaPrintingId);

            var foilTreatment = await db.Treatments
                .FirstOrDefaultAsync(t => t.GameId == basePrinting.Card.GameId && t.Name == "Foil");
            if (foilTreatment is null)
            {
                foilTreatment = new api.Models.Treatment
                {
                    GameId = basePrinting.Card.GameId,
                    Name = "Foil"
                };
                db.Treatments.Add(foilTreatment);
                await db.SaveChangesAsync();
            }

            var foilPrinting = new api.Models.CardPrinting
            {
                CardId = basePrinting.CardId,
                SetId = basePrinting.SetId,
                Number = basePrinting.Number,
                TreatmentId = foilTreatment.Id,
                IsFoil = true
            };
            db.CardPrintings.Add(foilPrinting);
            await db.SaveChangesAsync();
            foilPrintingId = foilPrinting.Id;
        }

        // Payload has the Standard printing's ID, but requests style = "Foil" for the same card/set/number.
        var payload = new
        {
            version = 1,
            collection = new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    qtyOwned = 4,
                    qtyProxyOwned = 0,
                    game = "Magic",
                    cardName = "Lightning Bolt",
                    setCode = "Alpha",
                    number = "A1",
                    style = "Foil"
                }
            },
            wishlist = Array.Empty<object>(),
            decks = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/import/json?mode=replace", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("imported").GetProperty("resolvedByNaturalKey").GetInt32());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var foilCard = await db.UserCards.SingleOrDefaultAsync(uc => uc.CardPrintingId == foilPrintingId);
            var standardCard = await db.UserCards.SingleOrDefaultAsync(uc => uc.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
            Assert.NotNull(foilCard);
            Assert.Equal(4, foilCard!.QuantityOwned);
            Assert.Null(standardCard);
        }
    }

    [Fact]
    public async Task ImportJson_LegacyWishlistWithoutSources_CreatesGeneralSourceSoDeckSyncAddsRatherThanOverwrites()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        // Legacy backup payload without any `sources` array
        var payload = new
        {
            version = 1,
            collection = Array.Empty<object>(),
            wishlist = new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                    qty = 5
                }
            },
            decks = new[]
            {
                new
                {
                    game = "Magic",
                    name = "Burn Deck",
                    description = "Test",
                    cards = new[]
                    {
                        new
                        {
                            cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                            inDeck = 2,
                            idea = 0,
                            acquire = 0,
                            proxy = 0
                        }
                    }
                }
            }
        };

        var importRes = await client.PostAsJsonAsync("/api/import/json?mode=replace", payload);
        importRes.EnsureSuccessStatusCode();

        int deckId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var generalSource = await db.WishlistSources.SingleOrDefaultAsync(
                ws => ws.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId && ws.DeckId == null);
            Assert.NotNull(generalSource);
            Assert.Equal(5, generalSource!.Quantity);

            deckId = (await db.Decks.SingleAsync(d => d.Name == "Burn Deck")).Id;
        }

        // Adding 2 missing copies from the deck should yield 5 (General) + 2 (Deck) = 7, not 2
        var syncRes = await client.PostAsJsonAsync($"/api/decks/{deckId}/wishlist-missing", new { includeProxies = false });
        syncRes.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var uc = await db.UserCards.SingleAsync(u => u.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
            Assert.Equal(7, uc.QuantityWanted);
        }
    }

    private record UserCardExpectation(int Owned, int Proxy, int Wishlist);
}
