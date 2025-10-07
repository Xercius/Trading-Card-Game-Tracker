// Run these tests with `dotnet test` or from Visual Studio Test Explorer.
// Validates /api/wishlist endpoints and legacy routes via full HTTP calls.

using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api.Data;
using api.Features.Collections.Dtos;
using api.Features.Wishlists.Dtos;
using api.Tests.Fixtures;
using api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using api.Tests.Helpers;


namespace api.Tests;

public class WishlistControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Wishlist_Get_CurrentUser_FiltersWantedOnly()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var items = await GetWishlistAsync(client, string.Empty);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.True(item.QuantityWanted > 0));

        var magicOnly = await GetWishlistAsync(client, "?game=Magic");
        Assert.Equal(2, magicOnly.Count);

        var filter = await GetWishlistAsync(client, "?name=bolt&set=Beta");
        var single = Assert.Single(filter);
        Assert.Equal(TestDataSeeder.LightningBoltBetaPrintingId, single.CardPrintingId);

        var specific = await GetWishlistAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltAlphaPrintingId}");
        var alpha = Assert.Single(specific);
        Assert.Equal("Lightning Bolt", alpha.CardName);
    }

    [Fact]
    public async Task Wishlist_Post_Upsert_ClampsToZero()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/wishlist",
            new
            {
                cardPrintingId = TestDataSeeder.ElsaPrintingId,
                quantityWanted = 3
            });
        Assert.Equal(HttpStatusCode.NoContent, createResponse.StatusCode);

        var created = await GetWishlistAsync(client, $"?cardPrintingId={TestDataSeeder.ElsaPrintingId}");
        var createdItem = Assert.Single(created);
        Assert.Equal(3, createdItem.QuantityWanted);

        var updateResponse = await client.PostAsJsonAsync(
            "/api/wishlist",
            new
            {
                cardPrintingId = TestDataSeeder.ElsaPrintingId,
                quantityWanted = -2
            });
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var afterUpdate = await GetWishlistAsync(client, $"?cardPrintingId={TestDataSeeder.ElsaPrintingId}");
        Assert.Empty(afterUpdate);
    }

    [Fact]
    public async Task Wishlist_BulkSet_CreatesMissingAndValidates()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.BobUserId);

        var payload = new[]
        {
            new { cardPrintingId = TestDataSeeder.MickeyPrintingId, quantityWanted = 5 },
            new { cardPrintingId = TestDataSeeder.LightningBoltAlphaPrintingId, quantityWanted = 2 }
        };

        var response = await client.PutAsJsonAsync("/api/wishlist", payload);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var items = await GetWishlistAsync(client, string.Empty);
        Assert.Equal(3, items.Count);
        var mickey = Assert.Single(items.Where(i => i.CardPrintingId == TestDataSeeder.MickeyPrintingId));
        Assert.Equal(5, mickey.QuantityWanted);
        var lightning = Assert.Single(items.Where(i => i.CardPrintingId == TestDataSeeder.LightningBoltAlphaPrintingId));
        Assert.Equal(2, lightning.QuantityWanted);

        var invalidResponse = await client.PutAsJsonAsync(
            "/api/wishlist",
            new[]
            {
                new { cardPrintingId = 9999, quantityWanted = 1 }
            });
        Assert.Equal(HttpStatusCode.NotFound, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Wishlist_MoveToCollection_DecrementsWanted_IncrementsOwned()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            "/api/wishlist/move-to-collection",
            new
            {
                cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                quantity = 1,
                useProxy = false
            });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var wishlist = await GetWishlistAsync(client, $"?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        var item = Assert.Single(wishlist);
        Assert.Equal(1, item.QuantityWanted);

        var collectionResponse = await client.GetAsync($"/api/collection?cardPrintingId={TestDataSeeder.LightningBoltBetaPrintingId}");
        collectionResponse.EnsureSuccessStatusCode();
        var collectionPayload = await collectionResponse.Content
            .ReadFromJsonAsync<Paged<UserCardItemResponse>>(JsonOptions);
        var collectionItem = Assert.Single(collectionPayload!.Items);
        Assert.Equal(1, collectionItem.QuantityOwned);
    }

    [Fact]
    public async Task Wishlist_MoveToCollection_UseProxy_FloorsWishlistAndIncrementsProxy()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            "/api/wishlist/move-to-collection",
            new
            {
                cardPrintingId = TestDataSeeder.LightningBoltBetaPrintingId,
                quantity = 3,
                useProxy = true
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.UserCards
            .SingleAsync(uc => uc.UserId == TestDataSeeder.AliceUserId
                               && uc.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);

        Assert.Equal(0, row.QuantityOwned);
        Assert.Equal(5, row.QuantityProxyOwned); // 2 existing proxies + 3 moved
        Assert.Equal(0, row.QuantityWanted); // floored at zero even when quantity exceeds wanted
    }

    [Fact]
    public async Task Wishlist_Delete_SetsWantedZeroAndRemovesWhenEmpty()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.BobUserId);

        var deleteResponse = await client.DeleteAsync($"/api/wishlist/{TestDataSeeder.MickeyPrintingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var wishlist = await GetWishlistAsync(client, string.Empty);
        Assert.DoesNotContain(wishlist, item => item.CardPrintingId == TestDataSeeder.MickeyPrintingId);
    }

    [Fact]
    public async Task Wishlist_LegacyRoute_UserMismatch_Returns403()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/user/{TestDataSeeder.BobUserId}/wishlist");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Wishlist_Endpoints_RequireHeader()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/wishlist");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wishlist_QuickAdd_CreatesAndAccumulates()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var create = await client.PostAsJsonAsync(
            "/api/wishlist/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 1 });
        create.EnsureSuccessStatusCode();

        var first = await create.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(first);
        Assert.Equal(TestDataSeeder.ExtraMagicPrintingId, first!.PrintingId);
        Assert.Equal(1, first.QuantityWanted);

        var increment = await client.PostAsJsonAsync(
            "/api/wishlist/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 2 });
        increment.EnsureSuccessStatusCode();

        var second = await increment.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(second);
        Assert.Equal(3, second!.QuantityWanted);
    }

    [Fact]
    public async Task Wishlist_QuickAdd_ClampsAtIntMax()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var create = await client.PostAsJsonAsync(
            "/api/wishlist/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = int.MaxValue - 1 });
        create.EnsureSuccessStatusCode();

        var increment = await client.PostAsJsonAsync(
            "/api/wishlist/items",
            new { printingId = TestDataSeeder.ExtraMagicPrintingId, quantity = 100 });
        increment.EnsureSuccessStatusCode();

        var result = await increment.Content.ReadFromJsonAsync<QuickAddResponse>();
        Assert.NotNull(result);
        Assert.Equal(int.MaxValue, result!.QuantityWanted);
    }

    [Fact]
    public async Task Wishlist_QuickAdd_RejectsInvalidQuantity()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.PostAsJsonAsync(
            "/api/wishlist/items",
            new { printingId = TestDataSeeder.LightningBoltAlphaPrintingId, quantity = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<List<WishlistItemResponse>> GetWishlistAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/wishlist{query}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<WishlistItemResponse>>(JsonOptions);
        return payload ?? [];
    }
}
