// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Covers /api/collection endpoints including legacy user-scoped routes.

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using api.Tests.Helpers;
using System.Text;
using System.Text.Json;
using api.Features.Collections.Dtos;
using api.Tests.Fixtures;
using Xunit;

namespace api.Tests;

public class CollectionControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CollectionControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Collection_Get_CurrentUser_FilteredOnly_ReturnsExpected()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var all = await GetCollectionAsync(client, string.Empty);
        Assert.Equal(3, all.Count);
        Assert.DoesNotContain(all, c => c.CardPrintingId == TestDataSeeder.GoblinGuidePrintingId);

        var magicOnly = await GetCollectionAsync(client, "?game=Magic");
        Assert.Equal(2, magicOnly.Count);
        Assert.All(magicOnly, item => Assert.Equal("Magic", item.Game));

        var filtered = await GetCollectionAsync(client, "?set=Beta&name=bolt");
        var single = Assert.Single(filtered);
        Assert.Equal(TestDataSeeder.LightningBoltBetaPrintingId, single.CardPrintingId);

        var specific = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.ElsaPrintingId}");
        var elsa = Assert.Single(specific);
        Assert.Equal("Rise of the Floodborn", elsa.Set);
    }

    [Fact]
    public async Task Collection_Post_Upsert_ClampsNonNegative()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = TestDataSeeder.GoblinGuidePrintingId,
                quantityOwned = 3,
                quantityWanted = 1,
                quantityProxyOwned = 2
            });

        Assert.Equal(HttpStatusCode.NoContent, createResponse.StatusCode);

        var created = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.GoblinGuidePrintingId}");
        var createdRow = Assert.Single(created);
        Assert.Equal(3, createdRow.QuantityOwned);
        Assert.Equal(1, createdRow.QuantityWanted);
        Assert.Equal(2, createdRow.QuantityProxyOwned);

        var updateResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = TestDataSeeder.GoblinGuidePrintingId,
                quantityOwned = -5,
                quantityWanted = -10,
                quantityProxyOwned = -1
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.GoblinGuidePrintingId}");
        var updatedRow = Assert.Single(updated);
        Assert.Equal(0, updatedRow.QuantityOwned);
        Assert.Equal(0, updatedRow.QuantityWanted);
        Assert.Equal(0, updatedRow.QuantityProxyOwned);
    }

    [Fact]
    public async Task Collection_Post_Upsert_SkipsInsertingAllZeroRowsAndUpdatesExisting()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var skipResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                quantityOwned = 0,
                quantityWanted = 0,
                quantityProxyOwned = 0
            });

        Assert.Equal(HttpStatusCode.NoContent, skipResponse.StatusCode);

        var skipped = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.ExtraMagicPrintingId}");
        Assert.Empty(skipped);

        var createResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                quantityOwned = 2,
                quantityWanted = 1,
                quantityProxyOwned = 3
            });

        Assert.Equal(HttpStatusCode.NoContent, createResponse.StatusCode);

        var created = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.ExtraMagicPrintingId}");
        var createdRow = Assert.Single(created);
        Assert.Equal(2, createdRow.QuantityOwned);
        Assert.Equal(1, createdRow.QuantityWanted);
        Assert.Equal(3, createdRow.QuantityProxyOwned);

        var updateResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                quantityOwned = 5,
                quantityWanted = 4,
                quantityProxyOwned = 0
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.ExtraMagicPrintingId}");
        var row = Assert.Single(updated);
        Assert.Equal(5, row.QuantityOwned);
        Assert.Equal(4, row.QuantityWanted);
        Assert.Equal(0, row.QuantityProxyOwned);
    }

    [Fact]
    public async Task Collection_Put_SetAll_UpdatesOr404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/collection/{TestDataSeeder.LightningBoltAlphaPrintingId}",
            new
            {
                quantityOwned = 1,
                quantityWanted = 5,
                quantityProxyOwned = -1
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var row = Assert.Single(updated);
        Assert.Equal(1, row.QuantityOwned);
        Assert.Equal(5, row.QuantityWanted);
        Assert.Equal(0, row.QuantityProxyOwned);

        var missingResponse = await client.PutAsJsonAsync(
            $"/api/collection/{TestDataSeeder.ExtraMagicPrintingId}",
            new
            {
                quantityOwned = 1,
                quantityWanted = 1,
                quantityProxyOwned = 1
            });

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
        public async Task Collection_Patch_Partial_UpdatesOnlySpecified()
        {
            await _factory.ResetDatabaseAsync();
            using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

            var patchReq = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/collection/{TestDataSeeder.LightningBoltBetaPrintingId}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { quantityWanted = 4 }),
                    Encoding.UTF8,
                    "application/json")
            };

            var response = await client.SendAsync(patchReq);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var rows = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
            var row = Assert.Single(rows);
            Assert.Equal(0, row.QuantityOwned);
            Assert.Equal(4, row.QuantityWanted);
            Assert.Equal(2, row.QuantityProxyOwned);
        }


    [Fact]
    public async Task Collection_Delta_CreatesMissing_ValidatesIds()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deltaResponse = await client.PostAsJsonAsync(
            "/api/collection/delta",
            new[]
            {
                new
                {
                    cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId,
                    deltaOwned = -10,
                    deltaWanted = 1,
                    deltaProxyOwned = 0
                },
                new
                {
                    cardPrintingId = TestDataSeeder.ExtraMagicPrintingId,
                    deltaOwned = 2,
                    deltaWanted = 0,
                    deltaProxyOwned = 1
                }
            });

        Assert.Equal(HttpStatusCode.NoContent, deltaResponse.StatusCode);

        var existing = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var existingRow = Assert.Single(existing);
        Assert.Equal(0, existingRow.QuantityOwned);
        Assert.Equal(2, existingRow.QuantityWanted);
        Assert.Equal(1, existingRow.QuantityProxyOwned);

        var created = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.ExtraMagicPrintingId}");
        var createdRow = Assert.Single(created);
        Assert.Equal(2, createdRow.QuantityOwned);
        Assert.Equal(0, createdRow.QuantityWanted);
        Assert.Equal(1, createdRow.QuantityProxyOwned);

        var invalidResponse = await client.PostAsJsonAsync(
            "/api/collection/delta",
            new[]
            {
                new
                {
                    cardPrintingId = 9999,
                    deltaOwned = 1,
                    deltaWanted = 0,
                    deltaProxyOwned = 0
                }
            });

        Assert.Equal(HttpStatusCode.NotFound, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Collection_Delete_RemovesRow()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deleteResponse = await client.DeleteAsync($"/api/collection/{TestDataSeeder.LightningBoltBetaPrintingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var rows = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Collection_LegacyRoute_UserMismatch_Returns403()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/user/{TestDataSeeder.BobUserId}/collection");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Collection_Endpoints_RequireUserHeader()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/collection");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<List<UserCardItemResponse>> GetCollectionAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/collection{query}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<UserCardItemResponse>>(_jsonOptions);
        return payload ?? new List<UserCardItemResponse>();
    }
}
