// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Covers /api/collection endpoints including legacy user-scoped routes.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using api.Features.Collections.Dtos;
using api.Shared;
using api.Tests.Fixtures;
using api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace api.Tests;

public class CollectionControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Collection_Get_CurrentUser_FilteredOnly_ReturnsExpected()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

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
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

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
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

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
    public async Task Collection_Put_UserScoped_SetAll_UpdatesOr404()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/user/{TestDataSeeder.AliceUserId}/collection/{TestDataSeeder.LightningBoltAlphaPrintingId}",
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
            $"/api/user/{TestDataSeeder.AliceUserId}/collection/{TestDataSeeder.ExtraMagicPrintingId}",
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
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var patchReq = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/collection/{TestDataSeeder.LightningBoltBetaPrintingId}")
        {
            Content = JsonContent.Create(new { quantityWanted = 4 })
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
    public async Task Collection_Put_OwnedProxy_SetsValuesAndIsIdempotent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PutAsJsonAsync(
            $"/api/collection/{TestDataSeeder.LightningBoltAlphaPrintingId}",
            new { ownedQty = 3, proxyQty = 2 }
        );
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var first = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var row = Assert.Single(first);
        Assert.Equal(3, row.QuantityOwned);
        Assert.Equal(2, row.QuantityProxyOwned);

        var secondResponse = await client.PutAsJsonAsync(
            $"/api/collection/{TestDataSeeder.LightningBoltAlphaPrintingId}",
            new { ownedQty = 3, proxyQty = 2 }
        );
        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);

        var second = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var secondRow = Assert.Single(second);
        Assert.Equal(3, secondRow.QuantityOwned);
        Assert.Equal(2, secondRow.QuantityProxyOwned);
    }

    [Fact]
    public async Task Collection_BulkPatch_UpdatesMultiplePrintings()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/collection/bulk")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, ownedDelta = 2, proxyDelta = 1 },
                    new { printingId = TestDataSeeder.LightningBoltBetaPrintingId, ownedDelta = -1, proxyDelta = 0 },
                }
            })
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var alpha = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var alphaRow = Assert.Single(alpha);
        Assert.Equal(7, alphaRow.QuantityOwned);
        Assert.Equal(2, alphaRow.QuantityProxyOwned);

        var beta = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        var betaRow = Assert.Single(beta);
        Assert.Equal(0, betaRow.QuantityOwned);
    }

    [Fact]
    public async Task Collection_BulkPatch_InvalidPrintingRollsBack()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/collection/bulk")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, ownedDelta = 2, proxyDelta = 0 },
                    new { printingId = 99999, ownedDelta = 1, proxyDelta = 0 },
                }
            })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.NotFound, problem!.Status);
        Assert.Contains("99999", problem.Detail);

        var alpha = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var alphaRow = Assert.Single(alpha);
        Assert.Equal(5, alphaRow.QuantityOwned);
    }

    [Fact]
    public async Task Collection_BulkPatch_UserScoped_InvalidPrintingRollsBack()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/user/{TestDataSeeder.AliceUserId}/collection/bulk")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, ownedDelta = 2, proxyDelta = 0 },
                    new { printingId = 99999, ownedDelta = 1, proxyDelta = 0 },
                }
            })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.NotFound, problem!.Status);
        Assert.Contains("99999", problem.Detail);

        var alpha = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var alphaRow = Assert.Single(alpha);
        Assert.Equal(5, alphaRow.QuantityOwned);
    }

    [Fact]
    public async Task Collection_List_IncludesAvailability()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var rows = await GetCollectionAsync(client, "");
        Assert.NotEmpty(rows);

        var alpha = rows.First(r => r.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Equal(alpha.QuantityOwned, alpha.Availability);
        Assert.Equal(alpha.QuantityOwned + alpha.QuantityProxyOwned, alpha.AvailabilityWithProxies);
    }

    [Fact]
    public async Task Collection_Patch_AllZero_DoesNotDeleteRow()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var patchReq = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/collection/{TestDataSeeder.LightningBoltBetaPrintingId}")
        {
            Content = JsonContent.Create(new
            {
                quantityOwned = 0,
                quantityWanted = 0,
                quantityProxyOwned = 0
            })
        };

        var response = await client.SendAsync(patchReq);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var rows = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        var row = Assert.Single(rows);
        Assert.Equal(0, row.QuantityOwned);
        Assert.Equal(0, row.QuantityWanted);
        Assert.Equal(0, row.QuantityProxyOwned);
    }

    [Fact]
    public async Task Collection_Delta_CreatesMissing_ValidatesIds()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

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
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var deleteResponse = await client.DeleteAsync($"/api/collection/{TestDataSeeder.LightningBoltBetaPrintingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var rows = await GetCollectionAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Collection_QuickAdd_CreatesAndAccumulates()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/collection/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 2 });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(created);
        Assert.Equal(TestDataSeeder.ExtraMagicPrintingId, created!.PrintingId);
        Assert.Equal(2, created.QuantityOwned);

        var incrementResponse = await client.PostAsJsonAsync(
            "/api/collection/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 3 });
        incrementResponse.EnsureSuccessStatusCode();

        var increment = await incrementResponse.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(increment);
        Assert.Equal(5, increment!.QuantityOwned);
    }

    [Fact]
    public async Task Collection_QuickAdd_ClampsAtIntMax()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/collection/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = int.MaxValue - 1 });
        createResponse.EnsureSuccessStatusCode();

        var incrementResponse = await client.PostAsJsonAsync(
            "/api/collection/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 100 });
        incrementResponse.EnsureSuccessStatusCode();

        var increment = await incrementResponse.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(increment);
        Assert.Equal(int.MaxValue, increment!.QuantityOwned);
    }

    [Fact]
    public async Task Collection_QuickAdd_RejectsInvalidQuantity()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            "/api/collection/items",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Collection_LegacyRoute_UserMismatch_Returns403()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/user/{TestDataSeeder.BobUserId}/collection");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Collection_Endpoints_RequireUserHeader()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/collection");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<List<UserCardItemResponse>> GetCollectionAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/collection{query}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Paged<UserCardItemResponse>>(_jsonOptions);
        return payload?.Items?.ToList() ?? [];
    }
}