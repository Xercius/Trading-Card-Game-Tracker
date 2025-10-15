using api.Features.Cards.Dtos;
using api.Shared;
using api.Tests.Fixtures;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests;

/// <summary>
/// Tests to verify case-insensitive filtering using normalized columns
/// </summary>
public class CaseInsensitiveFilterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("magic")]
    [InlineData("Magic")]
    [InlineData("MAGIC")]
    [InlineData("MaGiC")]
    public async Task Card_List_GameFilter_CaseInsensitive_ReturnsResults(string gameFilter)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/cards/search?game={gameFilter}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<Paged<CardListItemResponse>>(_jsonOptions);

        Assert.NotNull(page);
        Assert.Equal(2, page!.Total); // Should find both Magic cards
        Assert.All(page.Items, item => Assert.Equal("Magic", item.Game));
    }

    [Theory]
    [InlineData("alpha")]
    [InlineData("Alpha")]
    [InlineData("ALPHA")]
    public async Task Card_List_SetFilter_CaseInsensitive_ReturnsResults(string setFilter)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/cards/search?set={setFilter}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<Paged<CardListItemResponse>>(_jsonOptions);

        Assert.NotNull(page);
        Assert.True(page!.Total > 0); // Should find cards from Alpha set
    }

    [Theory]
    [InlineData("common")]
    [InlineData("Common")]
    [InlineData("COMMON")]
    public async Task Card_List_RarityFilter_CaseInsensitive_ReturnsResults(string rarityFilter)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/cards/search?rarity={rarityFilter}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<Paged<CardListItemResponse>>(_jsonOptions);

        Assert.NotNull(page);
        Assert.True(page!.Total > 0); // Should find common cards
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("Magic")]
    [InlineData("MAGIC")]
    public async Task CardFacets_Sets_GameFilter_CaseInsensitive_ReturnsResults(string gameFilter)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync($"/api/cards/facets/sets?game={gameFilter}");
        response.EnsureSuccessStatusCode();
        var facets = await response.Content.ReadFromJsonAsync<CardFacetSetsResponse>(_jsonOptions);

        Assert.NotNull(facets);
        Assert.Contains("Alpha", facets!.Sets);
        Assert.DoesNotContain("Rise of the Floodborn", facets.Sets);
    }
}
