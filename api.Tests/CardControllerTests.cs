// Run these tests with `dotnet test` at the solution root or from Visual Studio Test Explorer.
// This suite exercises the CardController integration endpoints end-to-end via WebApplicationFactory.

using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api.Common.Dtos;
using api.Features.Cards.Dtos;
using api.Tests.Fixtures;
using Xunit;

namespace api.Tests;

public class CardControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CardControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Card_List_FiltersAndPaging_ReturnsExpected()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var pageOneResponse = await client.GetAsync("/api/card?game=Magic&page=1&pageSize=1");
        pageOneResponse.EnsureSuccessStatusCode();
        var pageOne = await ReadPagedAsync<CardListItemResponse>(pageOneResponse);

        Assert.Equal(2, pageOne.Total);
        Assert.Equal(1, pageOne.Items.Count);
        Assert.Equal("Goblin Guide", pageOne.Items[0].Name);

        var pageTwoResponse = await client.GetAsync("/api/card?game=Magic&page=2&pageSize=1");
        pageTwoResponse.EnsureSuccessStatusCode();
        var pageTwo = await ReadPagedAsync<CardListItemResponse>(pageTwoResponse);

        Assert.Equal(2, pageTwo.Total);
        Assert.Single(pageTwo.Items);
        Assert.Equal("Lightning Bolt", pageTwo.Items[0].Name);

        var filteredResponse = await client.GetAsync("/api/card?includePrintings=true&name=bolt");
        filteredResponse.EnsureSuccessStatusCode();
        var filtered = await ReadPagedAsync<CardDetailResponse>(filteredResponse);

        var detail = Assert.Single(filtered.Items);
        Assert.Equal(TestDataSeeder.LightningBoltCardId, detail.CardId);
        Assert.Equal(3, detail.Printings.Count);
        Assert.Contains(detail.Printings, p => p.Id == TestDataSeeder.LightningBoltAlphaPrintingId);
        Assert.Contains(detail.Printings, p => p.Id == TestDataSeeder.LightningBoltBetaPrintingId);
        Assert.Contains(detail.Printings, p => p.Id == TestDataSeeder.ExtraMagicPrintingId);
    }

    [Fact]
    public async Task Card_Get_ById_ReturnsDetailOr404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().WithUser(TestDataSeeder.BobUserId);

        var response = await client.GetAsync($"/api/card/{TestDataSeeder.LightningBoltCardId}");
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<CardDetailResponse>(_jsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(TestDataSeeder.LightningBoltCardId, detail!.CardId);
        Assert.Equal("Lightning Bolt", detail.Name);
        Assert.Equal(3, detail.Printings.Count);
        Assert.Contains(detail.Printings, p => p.Id == TestDataSeeder.ExtraMagicPrintingId);

        var missing = await client.GetAsync("/api/card/9999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Card_Admin_UpsertPrinting_CreateAndUpdate_Works()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().AsAdmin();

        var createResponse = await client.PostAsJsonAsync(
            "/api/card/printing",
            new
            {
                cardId = TestDataSeeder.LightningBoltCardId,
                set = "Champions",
                number = "C5",
                rarity = "Rare",
                style = "Extended",
                imageUrl = "https://img.example.com/bolt-champions.png"
            });

        Assert.Equal(HttpStatusCode.NoContent, createResponse.StatusCode);

        var createdDetail = await client.GetFromJsonAsync<CardDetailResponse>(
            $"/api/card/{TestDataSeeder.LightningBoltCardId}",
            _jsonOptions);

        Assert.NotNull(createdDetail);
        Assert.Contains(createdDetail!.Printings, p =>
            p.Set == "Champions" &&
            p.Number == "C5" &&
            p.Style == "Extended" &&
            p.ImageUrl == "https://img.example.com/bolt-champions.png");

        var updateResponse = await client.PostAsJsonAsync(
            "/api/card/printing",
            new
            {
                id = TestDataSeeder.LightningBoltBetaPrintingId,
                cardId = TestDataSeeder.LightningBoltCardId,
                rarity = "Mythic",
                imageUrlSet = true,
                imageUrl = (string?)null
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updatedDetail = await client.GetFromJsonAsync<CardDetailResponse>(
            $"/api/card/{TestDataSeeder.LightningBoltCardId}",
            _jsonOptions);

        var updatedPrinting = Assert.Single(updatedDetail!.Printings.Where(p => p.Id == TestDataSeeder.LightningBoltBetaPrintingId));
        Assert.Equal("Mythic", updatedPrinting.Rarity);
        Assert.Null(updatedPrinting.ImageUrl);
    }

    [Fact]
    public async Task Card_Admin_BulkImport_MergesAndUpdates()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient().AsAdmin();

        var payload = new[]
        {
            new
            {
                cardId = TestDataSeeder.GoblinGuideCardId,
                set = "Zendikar",
                number = "Z3",
                style = "Standard",
                rarity = "Mythic",
                imageUrlSet = true,
                imageUrl = (string?)null
            },
            new
            {
                cardId = TestDataSeeder.GoblinGuideCardId,
                set = "Modern Masters",
                number = "MM1",
                style = "Standard",
                rarity = "Rare",
                imageUrl = "https://img.example.com/goblin-mm1.png"
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/card/{TestDataSeeder.GoblinGuideCardId}/printings/import",
            payload);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await client.GetFromJsonAsync<CardDetailResponse>(
            $"/api/card/{TestDataSeeder.GoblinGuideCardId}",
            _jsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Printings.Count);
        var existing = Assert.Single(detail.Printings.Where(p => p.Id == TestDataSeeder.GoblinGuidePrintingId));
        Assert.Equal("Mythic", existing.Rarity);
        Assert.Null(existing.ImageUrl);
        Assert.Contains(detail.Printings, p =>
            p.Set == "Modern Masters" &&
            p.Number == "MM1" &&
            p.ImageUrl == "https://img.example.com/goblin-mm1.png");
    }

    [Fact]
    public async Task Card_Admin_Endpoints_RequireAdmin()
    {
        await _factory.ResetDatabaseAsync();

        using var userClient = _factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var userResponse = await userClient.PostAsJsonAsync(
            "/api/card/printing",
            new
            {
                cardId = TestDataSeeder.LightningBoltCardId,
                set = "Test",
                number = "T1"
            });
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);

        using var anonymousClient = _factory.CreateClient();
        var anonResponse = await anonymousClient.PostAsJsonAsync(
            "/api/card/printing",
            new
            {
                cardId = TestDataSeeder.LightningBoltCardId,
                set = "Test",
                number = "T1"
            });
        Assert.Equal(HttpStatusCode.BadRequest, anonResponse.StatusCode);
    }

    private async Task<PagedResult<T>> ReadPagedAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<T>>(_jsonOptions);
        return payload ?? new PagedResult<T>(Array.Empty<T>(), 0, 1, 0);
    }
}
