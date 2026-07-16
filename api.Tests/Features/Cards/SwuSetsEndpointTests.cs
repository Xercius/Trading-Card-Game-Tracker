using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace api.Tests.Features.Cards;

/// <summary>
/// Integration tests verifying that Star Wars Unlimited sets/expansions are correctly
/// surfaced through the card facets endpoints after cards are imported via the SWU importer.
///
/// Set endpoint: GET /api/cards/facets/sets?game=Star+Wars+Unlimited
/// Response schema:
///   { "game": "Star Wars Unlimited", "sets": ["SHD", "SOR", ...] }
///
/// The "sets" list contains the expansion codes (e.g. "SOR", "SHD") that were stored
/// during import. Each value matches the expansion code field from the SWU Strapi API
/// (attributes.expansion.data.attributes.code, normalised to uppercase).
/// </summary>
public sealed class SwuSetsEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwuGame = "Star Wars Unlimited";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // ─── helpers ────────────────────────────────────────────────────────────

    /// <summary>Seeds one SWU card printing for the given expansion code.</summary>
    private static async Task SeedSwuPrintingAsync(
        AppDbContext db,
        string cardName,
        string expansionCode,
        string serialCode,
        string rarity = "Common")
    {
        var card = new Card
        {
            Game = SwuGame,
            Name = cardName,
            CardType = "Unit",
            Description = null
        };
        db.Cards.Add(card);

        db.CardPrintings.Add(new CardPrinting
        {
            Card = card,
            Set = expansionCode,
            Number = serialCode,
            Rarity = rarity,
            Style = "Standard",
            ImageUrl = null
        });

        await db.SaveChangesAsync();
    }

    private HttpClient AuthenticatedClient()
        => factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

    // ─── tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSets_FilteredBySwuGame_ReturnsImportedExpansionCodes()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSwuPrintingAsync(db, "Luke Skywalker", "SOR", "06010001", "Legendary");
        await SeedSwuPrintingAsync(db, "Han Solo", "SHD", "07010001", "Legendary");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/cards/facets/sets?game=Star+Wars+Unlimited");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardFacetSetsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(SwuGame, result!.Game);
        Assert.Contains("SOR", result.Sets);
        Assert.Contains("SHD", result.Sets);
    }

    [Fact]
    public async Task GetSets_FilteredBySwuGame_ExcludesOtherGameSets()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSwuPrintingAsync(db, "Darth Vader", "SOR", "06020001");

        // The test seed already includes Magic ("Alpha", "Beta", "Zendikar") and
        // Lorcana ("Rise of the Floodborn", "Spark of Imagination") sets.
        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/cards/facets/sets?game=Star+Wars+Unlimited");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardFacetSetsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(SwuGame, result!.Game);

        // SWU expansion code is present.
        Assert.Contains("SOR", result.Sets);

        // Other-game sets must not appear when filtering by SWU game.
        Assert.DoesNotContain("Alpha", result.Sets);
        Assert.DoesNotContain("Rise of the Floodborn", result.Sets);
    }

    [Fact]
    public async Task GetSets_SwuGame_SetsAreSortedAlphabetically()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSwuPrintingAsync(db, "Yoda", "TWI", "08010001");
        await SeedSwuPrintingAsync(db, "Obi-Wan Kenobi", "SOR", "06030001");
        await SeedSwuPrintingAsync(db, "Ahsoka Tano", "SHD", "07030001");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/cards/facets/sets?game=Star+Wars+Unlimited");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardFacetSetsResponse>(JsonOptions);
        Assert.NotNull(result);

        var swuSets = result!.Sets.ToList();
        var swuSetsOrdered = swuSets.OrderBy(s => s).ToList();
        Assert.Equal(swuSetsOrdered, swuSets);
    }

    [Fact]
    public async Task GetFacets_FilteredBySwuGame_IncludesSwuSets()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSwuPrintingAsync(db, "Emperor Palpatine", "SOR", "06040001", "Legendary");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/cards/facets?game=Star+Wars+Unlimited");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(JsonOptions);
        Assert.NotNull(result);

        // The unified facets response must include SWU sets.
        Assert.Contains(result!.Sets, f => f.Value == "SOR");

        // The game facet should include Star Wars Unlimited.
        Assert.Contains(result.Games, f => f.Value == SwuGame);
    }

    [Fact]
    public async Task GetFacets_FilteredBySwuSet_ReturnsSingleSetAndGame()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSwuPrintingAsync(db, "IG-88", "SHD", "07050001", "Uncommon");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/cards/facets?set=SHD");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardsFacetResponse>(JsonOptions);
        Assert.NotNull(result);

        // Only SHD cards are in the filtered view.
        Assert.Contains(result!.Sets, f => f.Value == "SHD");
        Assert.Contains(result.Games, f => f.Value == SwuGame);
    }
}
