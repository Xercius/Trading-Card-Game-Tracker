// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Exercises deck CRUD, deck-card management, and availability endpoints end-to-end.

using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api.Features.Decks.Dtos;
using api.Tests.Fixtures;
using Xunit;

namespace api.Tests;

public class DeckControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public DeckControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Deck_List_CurrentUser_FiltersAndHasCards_Works()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var decks = await GetDecksAsync(client, string.Empty);
        Assert.Equal(3, decks.Count);
        Assert.Contains(decks, d => d.Id == TestDataSeeder.AliceMagicDeckId && d.Name == "Alice Aggro");
        Assert.Contains(decks, d => d.Id == TestDataSeeder.AliceEmptyDeckId);

        var magic = await GetDecksAsync(client, "?game=Magic");
        Assert.Equal(2, magic.Count);
        Assert.DoesNotContain(magic, d => d.Game != "Magic");

        var withCards = await GetDecksAsync(client, "?game=Magic&hasCards=true");
        var single = Assert.Single(withCards);
        Assert.Equal(TestDataSeeder.AliceMagicDeckId, single.Id);

        var nameFiltered = await GetDecksAsync(client, "?name=Control");
        var controlDeck = Assert.Single(nameFiltered);
        Assert.Equal(TestDataSeeder.AliceLorcanaDeckId, controlDeck.Id);
    }

    [Fact]
    public async Task Deck_Create_Update_Delete_EnforcesOwnership()
    {
        await _factory.ResetDatabaseAsync();
        using var alice = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        using var bob = _factory.CreateClient().WithUser(TestDataSeeder.BobUserId);

        var createResponse = await alice.PostAsJsonAsync(
            "/api/deck",
            new
            {
                game = "Magic",
                name = "Alice Brew",
                description = "Testing deck"
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdDeck = await createResponse.Content.ReadFromJsonAsync<DeckResponse>(_jsonOptions);
        Assert.NotNull(createdDeck);
        var deckId = createdDeck!.Id;

        var duplicateResponse = await alice.PostAsJsonAsync(
            "/api/deck",
            new
            {
                game = "Magic",
                name = "Alice Brew",
                description = "Duplicate"
            });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var bobView = await bob.GetAsync($"/api/deck/{deckId}");
        Assert.Equal(HttpStatusCode.Forbidden, bobView.StatusCode);

        var patchResponse = await alice.PatchAsync(
            $"/api/deck/{deckId}",
            JsonContent.Create(new { description = "Updated" }));
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var putResponse = await alice.PutAsJsonAsync(
            $"/api/deck/{deckId}",
            new
            {
                game = "Magic",
                name = "Alice Brew Updated",
                description = "Updated"
            });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var deleteResponse = await alice.DeleteAsync($"/api/deck/{deckId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await alice.GetAsync($"/api/deck/{deckId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);

        var bobDelete = await bob.DeleteAsync($"/api/deck/{TestDataSeeder.AliceMagicDeckId}");
        Assert.Equal(HttpStatusCode.Forbidden, bobDelete.StatusCode);
    }

    [Fact]
    public async Task DeckCards_Delta_CreatesMissing_ClampsNonNegative_ValidatesGame()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deltaResponse = await client.PostAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/delta",
            new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    deltaInDeck = -10,
                    deltaIdea = 1,
                    deltaAcquire = 0,
                    deltaProxy = 0
                },
                new
                {
                    cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                    deltaInDeck = 1,
                    deltaIdea = 0,
                    deltaAcquire = 2,
                    deltaProxy = 1
                }
            });
        Assert.Equal(HttpStatusCode.NoContent, deltaResponse.StatusCode);

        var cards = await GetDeckCardsAsync(client, TestDataSeeder.AliceMagicDeckId);
        var alpha = Assert.Single(cards.Where(c => c.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId));
        Assert.Equal(0, alpha.QuantityInDeck);
        Assert.Equal(1, alpha.QuantityIdea);

        var extra = Assert.Single(cards.Where(c => c.CardPrintingId == TestDataSeeder.ExtraMagicPrintingId));
        Assert.Equal(1, extra.QuantityInDeck);
        Assert.Equal(2, extra.QuantityAcquire);
        Assert.Equal(1, extra.QuantityProxy);

        var invalidResponse = await client.PostAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/delta",
            new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.ElsaPrintingId,
                    deltaInDeck = 1,
                    deltaIdea = 0,
                    deltaAcquire = 0,
                    deltaProxy = 0
                }
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task DeckCards_Delta_RemovesRowsWhenAllQuantitiesReachZero()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/delta",
            new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                    deltaInDeck = 1,
                    deltaIdea = 0,
                    deltaAcquire = 0,
                    deltaProxy = 0
                },
                new
                {
                    cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                    deltaInDeck = -1,
                    deltaIdea = 0,
                    deltaAcquire = 0,
                    deltaProxy = 0
                },
                new
                {
                    cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    deltaInDeck = -4,
                    deltaIdea = 0,
                    deltaAcquire = 0,
                    deltaProxy = 0
                }
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cards = await GetDeckCardsAsync(client, TestDataSeeder.AliceMagicDeckId);
        Assert.DoesNotContain(cards, c => c.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.DoesNotContain(cards, c => c.CardPrintingId == TestDataSeeder.ExtraMagicPrintingId);
        Assert.Contains(cards, c => c.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
    }

    [Fact]
    public async Task DeckCards_AllEndpoints_EnforceOwnership_403WhenNotOwner()
    {
        await _factory.ResetDatabaseAsync();
        using var bob = _factory.CreateClient().WithUser(TestDataSeeder.BobUserId);

        var deckResponse = await bob.GetAsync($"/api/deck/{TestDataSeeder.AliceMagicDeckId}");
        Assert.Equal(HttpStatusCode.Forbidden, deckResponse.StatusCode);

        var cardsResponse = await bob.GetAsync($"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards");
        Assert.Equal(HttpStatusCode.Forbidden, cardsResponse.StatusCode);

        var upsertResponse = await bob.PostAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards",
            new
            {
                cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                quantityInDeck = 1,
                quantityIdea = 0,
                quantityAcquire = 0,
                quantityProxy = 0
            });
        Assert.Equal(HttpStatusCode.Forbidden, upsertResponse.StatusCode);

        var deleteResponse = await bob.DeleteAsync($"/api/deck/{TestDataSeeder.AliceMagicDeckId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeckCards_SingleUpsert_Set_Patch_Delete()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var upsertResponse = await client.PostAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards",
            new
            {
                cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                quantityInDeck = 2,
                quantityIdea = 1,
                quantityAcquire = 0,
                quantityProxy = 1
            });
        Assert.Equal(HttpStatusCode.NoContent, upsertResponse.StatusCode);

        var putResponse = await client.PutAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/{TestDataSeeder.ExtraMagicPrintingId}",
            new
            {
                quantityInDeck = 5,
                quantityIdea = -2,
                quantityAcquire = 3,
                quantityProxy = -1
            });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var patchResponse = await client.PatchAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/{TestDataSeeder.ExtraMagicPrintingId}",
            JsonContent.Create(new { quantityIdea = 4 }));
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var cards = await GetDeckCardsAsync(client, TestDataSeeder.AliceMagicDeckId);
        var card = Assert.Single(cards.Where(c => c.CardPrintingId == TestDataSeeder.ExtraMagicPrintingId));
        Assert.Equal(5, card.QuantityInDeck);
        Assert.Equal(4, card.QuantityIdea);
        Assert.Equal(3, card.QuantityAcquire);
        Assert.Equal(0, card.QuantityProxy);

        var deleteResponse = await client.DeleteAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}/cards/{TestDataSeeder.ExtraMagicPrintingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await GetDeckCardsAsync(client, TestDataSeeder.AliceMagicDeckId);
        Assert.DoesNotContain(afterDelete, c => c.CardPrintingId == TestDataSeeder.ExtraMagicPrintingId);
    }

    [Fact]
    public async Task Deck_Patch_DuplicateName_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PatchAsync(
            $"/api/deck/{TestDataSeeder.AliceEmptyDeckId}",
            JsonContent.Create(new { name = "Alice Aggro" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deck_Put_RequiresNameAndGameAndRejectsDuplicates()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var missingName = await client.PutAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}",
            new
            {
                game = "Magic",
                name = " ",
                description = "Invalid update"
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);

        var missingGame = await client.PutAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceMagicDeckId}",
            new
            {
                game = "",
                name = "Valid Name",
                description = "Invalid update"
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingGame.StatusCode);

        var duplicateName = await client.PutAsJsonAsync(
            $"/api/deck/{TestDataSeeder.AliceEmptyDeckId}",
            new
            {
                game = "Magic",
                name = "Alice Aggro",
                description = "Duplicate"
            });
        Assert.Equal(HttpStatusCode.Conflict, duplicateName.StatusCode);
    }

    [Fact]
    public async Task Deck_Availability_WithAndWithoutProxies_ReturnsExpected()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var withoutProxies = await GetDeckAvailabilityAsync(client, includeProxies: false);
        Assert.Equal(2, withoutProxies.Count);

        var alpha = Assert.Single(withoutProxies.Where(a => a.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId));
        Assert.Equal(5, alpha.Owned);
        Assert.Equal(1, alpha.Proxy);
        Assert.Equal(4, alpha.Assigned);
        Assert.Equal(1, alpha.Available);
        Assert.Equal(1, alpha.AvailableWithProxy);

        var beta = Assert.Single(withoutProxies.Where(a => a.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId));
        Assert.Equal(0, beta.Owned);
        Assert.Equal(2, beta.Proxy);
        Assert.Equal(1, beta.Assigned);
        Assert.Equal(0, beta.Available);
        Assert.Equal(0, beta.AvailableWithProxy);

        var withProxies = await GetDeckAvailabilityAsync(client, includeProxies: true);
        var betaWithProxy = Assert.Single(withProxies.Where(a => a.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId));
        Assert.Equal(2, betaWithProxy.Proxy);
        Assert.Equal(1, betaWithProxy.AvailableWithProxy);
    }

    private async Task<List<DeckResponse>> GetDecksAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/deck{query}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<DeckResponse>>(_jsonOptions);
        return payload ?? new List<DeckResponse>();
    }

    private async Task<List<DeckCardItemResponse>> GetDeckCardsAsync(HttpClient client, int deckId)
    {
        var response = await client.GetAsync($"/api/deck/{deckId}/cards");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<DeckCardItemResponse>>(_jsonOptions);
        return payload ?? new List<DeckCardItemResponse>();
    }

    private async Task<List<DeckAvailabilityItemResponse>> GetDeckAvailabilityAsync(HttpClient client, bool includeProxies)
    {
        var response = await client.GetAsync($"/api/deck/{TestDataSeeder.AliceMagicDeckId}/availability?includeProxies={includeProxies.ToString().ToLowerInvariant()}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<DeckAvailabilityItemResponse>>(_jsonOptions);
        return payload ?? new List<DeckAvailabilityItemResponse>();
    }
}
