using api.Features.Cards.Dtos;
using api.Tests.Fixtures;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests;

public class CardFacetsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Games_ReturnSeededFacets()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets/games");
        response.EnsureSuccessStatusCode();

        var games = await response.Content.ReadFromJsonAsync<List<string>>(Options);
        Assert.NotNull(games);
        Assert.NotEmpty(games!);
        Assert.Contains("Magic", games!);
        Assert.Contains("Lorcana", games!);
    }

    [Fact]
    public async Task SetsAndRarities_FilterByGame()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var setsResponse = await client.GetAsync("/api/cards/facets/sets");
        setsResponse.EnsureSuccessStatusCode();
        var allSets = await setsResponse.Content.ReadFromJsonAsync<CardFacetSetsResponse>(Options);
        Assert.NotNull(allSets);
        Assert.Contains("Rise of the Floodborn", allSets!.Sets);
        Assert.Contains("Alpha", allSets.Sets);

        var raritiesResponse = await client.GetAsync("/api/cards/facets/rarities");
        raritiesResponse.EnsureSuccessStatusCode();
        var allRarities = await raritiesResponse.Content.ReadFromJsonAsync<CardFacetRaritiesResponse>(Options);
        Assert.NotNull(allRarities);
        Assert.Contains("Legendary", allRarities!.Rarities);
        Assert.Contains("Common", allRarities.Rarities);

        var filteredSetsResponse = await client.GetAsync("/api/cards/facets/sets?game=Magic");
        filteredSetsResponse.EnsureSuccessStatusCode();
        var magicSets = await filteredSetsResponse.Content.ReadFromJsonAsync<CardFacetSetsResponse>(Options);
        Assert.NotNull(magicSets);
        Assert.Equal("Magic", magicSets!.Game);
        Assert.Contains("Alpha", magicSets.Sets);
        Assert.DoesNotContain("Rise of the Floodborn", magicSets.Sets);

        var filteredRaritiesResponse = await client.GetAsync("/api/cards/facets/rarities?game=Magic");
        filteredRaritiesResponse.EnsureSuccessStatusCode();
        var magicRarities = await filteredRaritiesResponse.Content.ReadFromJsonAsync<CardFacetRaritiesResponse>(Options);
        Assert.NotNull(magicRarities);
        Assert.Equal("Magic", magicRarities!.Game);
        Assert.Contains("Common", magicRarities.Rarities);
        Assert.DoesNotContain("Legendary", magicRarities.Rarities);
    }
}
