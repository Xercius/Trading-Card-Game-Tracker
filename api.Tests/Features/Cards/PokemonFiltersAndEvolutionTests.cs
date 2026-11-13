using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class PokemonFiltersAndEvolutionTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<string> SeedPokemonCatalogAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingGame = db.Games.FirstOrDefault(g => g.Name == "Pokemon TCG");
        if (existingGame is not null)
        {
            return existingGame.Name;
        }

        var game = new Game { Name = "Pokemon TCG" };
        db.Games.Add(game);

        var pokemonType = new CardType { Game = game, Name = "Pokemon" };
        var trainerType = new CardType { Game = game, Name = "Trainer" };
        db.CardTypes.AddRange(pokemonType, trainerType);

        var set = new Set { Game = game, Name = "Test Pokemon Set", Code = "PKT" };
        db.Sets.Add(set);

        var commonRarity = new Rarity { Game = game, Name = "Common" };
        var aceSpecRarity = new Rarity { Game = game, Name = "Ace Spec Rare" };
        db.Rarities.AddRange(commonRarity, aceSpecRarity);

        await db.SaveChangesAsync();

        void AddCard(
            string name,
            CardType cardType,
            string number,
            string? stage = "Basic",
            string? evolveFrom = null,
            int[]? dexIds = null,
            string? suffix = null,
            string? text = null,
            Rarity? rarity = null)
        {
            var extra = JsonSerializer.Serialize(new
            {
                stage,
                suffix,
                evolveFrom,
                dexId = dexIds,
            });
            var card = new Card
            {
                GameId = game.Id,
                Name = name,
                Stage = stage,
                Text = text,
                ExtraCardDataJson = extra,
            };
            card.CardTypes.Add(new CardCardType { Card = card, CardTypeId = cardType.Id, SourceRole = "Type" });
            db.Cards.Add(card);

            db.CardPrintings.Add(new CardPrinting
            {
                Card = card,
                SetId = set.Id,
                Number = number,
                RarityId = (rarity ?? commonRarity).Id,
            });
        }

        // Pikachu lineage
        AddCard("Pichu", pokemonType, "001", stage: "Basic", dexIds: [172]);
        AddCard("Pikachu", pokemonType, "002", stage: "Basic", evolveFrom: "Pichu", dexIds: [25]);
        AddCard("Pikachu V", pokemonType, "003", stage: "Basic", dexIds: [25], suffix: "V");
        AddCard("Pikachu VMAX", pokemonType, "004", stage: "VMAX", evolveFrom: "Pikachu V", dexIds: [25], suffix: "VMAX");
        AddCard("Pikachu ex", pokemonType, "005", stage: "Basic", dexIds: [25], suffix: "ex");
        AddCard("Raichu", pokemonType, "006", stage: "Stage 1", evolveFrom: "Pikachu", dexIds: [26]);
        AddCard("Alolan Raichu", pokemonType, "007", stage: "Stage 1", evolveFrom: "Pikachu", dexIds: [26]);
        AddCard("Pikachu & Zekrom-GX", pokemonType, "008", stage: "Basic", dexIds: [25, 644], suffix: "GX");

        // Eevee branching lineage
        AddCard("Eevee", pokemonType, "009", stage: "Basic", dexIds: [133]);
        AddCard("Vaporeon", pokemonType, "010", stage: "Stage 1", evolveFrom: "Eevee", dexIds: [134]);
        AddCard("Vaporeon V", pokemonType, "011", stage: "Basic", dexIds: [134], suffix: "V");
        AddCard("Vaporeon VMAX", pokemonType, "012", stage: "VMAX", evolveFrom: "Vaporeon V", dexIds: [134], suffix: "VMAX");
        AddCard("Jolteon", pokemonType, "013", stage: "Stage 1", evolveFrom: "Eevee", dexIds: [135]);

        // Special types
        AddCard("Charizard-EX", pokemonType, "014", stage: "Basic", dexIds: [6], suffix: "EX");
        AddCard("M Charizard-EX", pokemonType, "015", stage: "MEGA", evolveFrom: "Charizard-EX", dexIds: [6], suffix: "EX");
        AddCard("Radiant Charizard", pokemonType, "016", stage: "Basic", dexIds: [6]);
        AddCard("Roaring Moon ex", pokemonType, "017", stage: "Basic", dexIds: [1005], suffix: "ex", text: "Ancient Paradox");
        AddCard("Iron Valiant ex", pokemonType, "018", stage: "Basic", dexIds: [1006], suffix: "ex", text: "Future Paradox");
        AddCard("Prime Catcher", trainerType, "019", stage: null, text: "ACE SPEC Item", rarity: aceSpecRarity);

        await db.SaveChangesAsync();
        return game.Name;
    }

    [Fact]
    public async Task GetPokemonNames_MatchingQuery_ReturnsDistinctSortedPokemonNames()
    {
        await SeedPokemonCatalogAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/pokemon-names?q=Pika");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var names = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(names);
        Assert.Contains("Pikachu", names);
        Assert.Contains("Pikachu V", names);
        Assert.Contains("Pikachu ex", names);
        Assert.DoesNotContain("Prime Catcher", names);
    }

    [Fact]
    public async Task GetPrintings_WithSpecialTypeFilter_ReturnsCombinedExAndOtherSpecialTypes()
    {
        var game = await SeedPokemonCatalogAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Combined Pokémon EX / ex matches both "Pikachu ex" and "Charizard-EX"
        var exPrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
            $"/api/cards/printings?game={Uri.EscapeDataString(game)}&specialType={Uri.EscapeDataString("Pokémon EX / ex")}");
        Assert.NotNull(exPrintings);
        Assert.Contains(exPrintings, p => p.CardName == "Pikachu ex");
        Assert.Contains(exPrintings, p => p.CardName == "Charizard-EX");
        Assert.DoesNotContain(exPrintings, p => p.CardName == "Pikachu V");

        // Multi-select OR semantics: Mega Evolution + Future
        var multiPrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
            $"/api/cards/printings?game={Uri.EscapeDataString(game)}&specialType={Uri.EscapeDataString("Mega Evolution,Future")}");
        Assert.NotNull(multiPrintings);
        Assert.Contains(multiPrintings, p => p.CardName == "M Charizard-EX");
        Assert.Contains(multiPrintings, p => p.CardName == "Iron Valiant ex");
        Assert.DoesNotContain(multiPrintings, p => p.CardName == "Roaring Moon ex");

        // Facets include SpecialTypes counts
        var facets = await client.GetFromJsonAsync<CardsFacetResponse>(
            $"/api/cards/facets?game={Uri.EscapeDataString(game)}");
        Assert.NotNull(facets);
        Assert.Contains(facets.SpecialTypes, f => f.Value == "Pokémon V" && f.Count >= 4);
        Assert.Contains(facets.SpecialTypes, f => f.Value == "Pokémon EX / ex" && f.Count >= 5);
        Assert.Contains(facets.SpecialTypes, f => f.Value == "ACE SPEC" && f.Count >= 1);
    }

    [Fact]
    public async Task GetPrintings_WithEvolvesFrom_ReturnsFullLineageWithoutUnrelatedBranches()
    {
        var game = await SeedPokemonCatalogAsync();
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        // Selecting Pikachu returns Pichu (ancestor), Pikachu + V/VMAX/ex/TagTeam (self), and Raichu/Alolan Raichu (descendants)
        var pikachuLine = await client.GetFromJsonAsync<List<PrintingDto>>(
            $"/api/cards/printings?game={Uri.EscapeDataString(game)}&evolvesFrom=Pikachu");
        Assert.NotNull(pikachuLine);
        var pikachuNames = pikachuLine.Select(p => p.CardName).ToHashSet();
        Assert.Contains("Pichu", pikachuNames);
        Assert.Contains("Pikachu", pikachuNames);
        Assert.Contains("Pikachu V", pikachuNames);
        Assert.Contains("Pikachu VMAX", pikachuNames);
        Assert.Contains("Pikachu ex", pikachuNames);
        Assert.Contains("Raichu", pikachuNames);
        Assert.Contains("Alolan Raichu", pikachuNames);
        Assert.Contains("Pikachu & Zekrom-GX", pikachuNames);
        Assert.DoesNotContain("Eevee", pikachuNames);

        // Selecting Vaporeon returns Eevee + Vaporeon + Vaporeon V + Vaporeon VMAX, without Jolteon
        var vaporeonLine = await client.GetFromJsonAsync<List<PrintingDto>>(
            $"/api/cards/printings?game={Uri.EscapeDataString(game)}&evolvesFrom=Vaporeon");
        Assert.NotNull(vaporeonLine);
        var vaporeonNames = vaporeonLine.Select(p => p.CardName).ToHashSet();
        Assert.Contains("Eevee", vaporeonNames);
        Assert.Contains("Vaporeon", vaporeonNames);
        Assert.Contains("Vaporeon V", vaporeonNames);
        Assert.Contains("Vaporeon VMAX", vaporeonNames);
        Assert.DoesNotContain("Jolteon", vaporeonNames);
    }
}
