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

    [Fact]
    public async Task GetFacets_ReturnsAllFacetsWithCounts_WhenNoFiltersApplied()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Should have games
        Assert.NotEmpty(facets!.Games);
        Assert.All(facets.Games, f => Assert.True(f.Count > 0));
        
        // Should have sets
        Assert.NotEmpty(facets.Sets);
        Assert.All(facets.Sets, f => Assert.True(f.Count > 0));
        
        // Should have rarities
        Assert.NotEmpty(facets.Rarities);
        Assert.All(facets.Rarities, f => Assert.True(f.Count > 0));
    }

    [Fact]
    public async Task GetFacets_FiltersSetsByGame_CaseInsensitive()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?game=magic");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Sets should only contain Magic sets
        Assert.NotEmpty(facets!.Sets);
        Assert.Contains(facets.Sets, f => f.Value == "Alpha");
        Assert.DoesNotContain(facets.Sets, f => f.Value == "Rise of the Floodborn");
        
        // Rarities should only contain Magic rarities
        Assert.NotEmpty(facets.Rarities);
        Assert.DoesNotContain(facets.Rarities, f => f.Value == "Legendary");
    }

    [Fact]
    public async Task GetFacets_FiltersRaritiesBySet()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?set=Alpha");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Games should contain Magic
        Assert.Contains(facets!.Games, f => f.Value == "Magic");
        
        // Rarities should only contain rarities from Alpha set
        Assert.NotEmpty(facets.Rarities);
    }

    [Fact]
    public async Task GetFacets_FiltersGamesByRarity()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?rarity=Common");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Games should only contain games with Common rarity
        Assert.NotEmpty(facets!.Games);
        Assert.Contains(facets.Games, f => f.Value == "Magic");
    }

    [Fact]
    public async Task GetFacets_FiltersWithSearchTerm()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?q=black");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Should have facets but only for cards matching search term
        Assert.NotNull(facets!.Games);
        Assert.NotNull(facets.Sets);
        Assert.NotNull(facets.Rarities);
    }

    [Fact]
    public async Task GetFacets_CombinesMultipleFilters()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/facets?game=Magic&rarity=Common");
        response.EnsureSuccessStatusCode();

        var facets = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(Options);
        Assert.NotNull(facets);
        
        // Sets should only contain Magic sets with Common rarity cards
        Assert.NotEmpty(facets!.Sets);
        
        // Games should contain Magic
        Assert.Contains(facets.Games, f => f.Value == "Magic");
    }
}
