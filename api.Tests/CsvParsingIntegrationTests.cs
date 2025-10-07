using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Features.Cards.Dtos;
using api.Tests.Fixtures;
using api.Tests.Helpers;
using Xunit;

namespace api.Tests;

public class CsvParsingIntegrationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task CardsController_ListCardsVirtualized_NormalizesCsvFilters()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var baseline = await client.GetFromJsonAsync<CardListPageResponse>("/api/cards?take=200");
        Assert.NotNull(baseline);

        var empty = await client.GetFromJsonAsync<CardListPageResponse>("/api/cards?game=&take=200");
        Assert.NotNull(empty);
        Assert.Equal(baseline!.Items.Select(i => i.CardId), empty!.Items.Select(i => i.CardId));

        var normalized = await client.GetFromJsonAsync<CardListPageResponse>("/api/cards?game=Magic,Lorcana&take=200");
        Assert.NotNull(normalized);

        var duplicatesAndWhitespace = await client.GetFromJsonAsync<CardListPageResponse>(
            "/api/cards?game=%20Magic%20,%20Magic%20,%20Lorcana%20&take=200");
        Assert.NotNull(duplicatesAndWhitespace);

        Assert.Equal(
            normalized!.Items.Select(i => i.CardId),
            duplicatesAndWhitespace!.Items.Select(i => i.CardId));
    }

    [Fact]
    public async Task CardFacetsController_NormalizesCsvFilters()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var allSets = await client.GetFromJsonAsync<CardFacetSetsResponse>("/api/cards/facets/sets");
        Assert.NotNull(allSets);

        var emptySets = await client.GetFromJsonAsync<CardFacetSetsResponse>("/api/cards/facets/sets?game=");
        Assert.NotNull(emptySets);
        Assert.Null(emptySets!.Game);
        Assert.Equal(allSets!.Sets, emptySets.Sets);

        var normalizedSets = await client.GetFromJsonAsync<CardFacetSetsResponse>("/api/cards/facets/sets?game=Magic,Lorcana");
        Assert.NotNull(normalizedSets);

        var duplicatesAndWhitespaceSets = await client.GetFromJsonAsync<CardFacetSetsResponse>(
            "/api/cards/facets/sets?game=%20Magic%20,%20Magic%20,%20Lorcana%20");
        Assert.NotNull(duplicatesAndWhitespaceSets);
        Assert.Null(duplicatesAndWhitespaceSets!.Game);
        Assert.Equal(normalizedSets!.Sets, duplicatesAndWhitespaceSets.Sets);

        var singleGameSets = await client.GetFromJsonAsync<CardFacetSetsResponse>("/api/cards/facets/sets?game=Magic");
        Assert.NotNull(singleGameSets);

        var duplicateSingleSets = await client.GetFromJsonAsync<CardFacetSetsResponse>(
            "/api/cards/facets/sets?game=Magic,%20Magic%20");
        Assert.NotNull(duplicateSingleSets);
        Assert.Equal("Magic", duplicateSingleSets!.Game);
        Assert.Equal(singleGameSets!.Sets, duplicateSingleSets.Sets);

        var allRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>("/api/cards/facets/rarities");
        Assert.NotNull(allRarities);

        var emptyRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>("/api/cards/facets/rarities?game=");
        Assert.NotNull(emptyRarities);
        Assert.Null(emptyRarities!.Game);
        Assert.Equal(allRarities!.Rarities, emptyRarities.Rarities);

        var normalizedRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>("/api/cards/facets/rarities?game=Magic,Lorcana");
        Assert.NotNull(normalizedRarities);

        var duplicatesAndWhitespaceRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>(
            "/api/cards/facets/rarities?game=%20Magic%20,%20Magic%20,%20Lorcana%20");
        Assert.NotNull(duplicatesAndWhitespaceRarities);
        Assert.Null(duplicatesAndWhitespaceRarities!.Game);
        Assert.Equal(normalizedRarities!.Rarities, duplicatesAndWhitespaceRarities.Rarities);

        var singleGameRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>("/api/cards/facets/rarities?game=Magic");
        Assert.NotNull(singleGameRarities);

        var duplicateSingleRarities = await client.GetFromJsonAsync<CardFacetRaritiesResponse>(
            "/api/cards/facets/rarities?game=Magic,%20Magic%20");
        Assert.NotNull(duplicateSingleRarities);
        Assert.Equal("Magic", duplicateSingleRarities!.Game);
        Assert.Equal(singleGameRarities!.Rarities, duplicateSingleRarities.Rarities);
    }
}
