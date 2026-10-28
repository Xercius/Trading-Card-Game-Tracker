using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public class PrintingsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Get_with_card_type_search_matches_facet_results()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/cards/printings?q=cReAtUrE&uniqueCards=true");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var printings = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        var facets = await client.GetFromJsonAsync<CardsFacetResponse>("/api/cards/facets?q=cReAtUrE&printingSearch=true");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.GoblinGuidePrintingId, printing.PrintingId);
        Assert.NotNull(facets);
        Assert.Equal(printing.Game, Assert.Single(facets.Games).Value);
        Assert.Equal(printing.SetName, Assert.Single(facets.Sets).Value);
        Assert.Equal(printing.Rarity, Assert.Single(facets.Rarities).Value);
    }

    [Fact]
    public async Task Get_with_number_filters_to_exact_match()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=A1");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, printing.PrintingId);
        Assert.Equal("A1", printing.Number);
    }

    [Fact]
    public async Task Upsert_with_mismatched_card_id_uses_existing_printing_game_for_treatment()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var request = new UpsertPrintingRequest(
            TestDataSeeder.GoblinGuidePrintingId,
            TestDataSeeder.ElsaCardId,
            null,
            null,
            null,
            "Showcase",
            null);

        var response = await client.PostAsJsonAsync("/api/cards/printing", request);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printing = await db.CardPrintings
            .Include(p => p.Treatment)
            .SingleAsync(p => p.Id == TestDataSeeder.GoblinGuidePrintingId);
        var magicGameId = await db.Games
            .Where(game => game.NameNormalized == "magic")
            .Select(game => game.Id)
            .SingleAsync();

        Assert.Equal("Showcase", printing.Treatment!.Name);
        Assert.Equal(magicGameId, printing.Treatment.GameId);
    }

    [Fact]
    public async Task Get_with_style_filter_matches_treatment_name()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var printing = await db.CardPrintings.SingleAsync(
                p => p.Id == TestDataSeeder.GoblinGuidePrintingId);
            printing.Treatment = TreatmentLookup.Upsert(db, "Magic", "Hyperspace");
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var hyperspace = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Hyperspace");
        var showcase = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Showcase");
        var both = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Hyperspace%2C%20Showcase");
        var partial = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=space");

        Assert.Contains(hyperspace!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.DoesNotContain(showcase!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.Contains(both!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.DoesNotContain(partial!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
    }

    [Fact]
    public async Task Get_with_unknown_number_returns_empty_list()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=UNKNOWN");

        Assert.NotNull(printings);
        Assert.Empty(printings);
    }

    [Fact]
    public async Task Get_with_single_game_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.Equal("Magic", p.Game));
    }

    [Fact]
    public async Task Get_with_multiple_games_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Lorcana");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.Contains(printings, p => p.Game == "Magic");
        Assert.Contains(printings, p => p.Game == "Lorcana");
        Assert.All(printings, p => Assert.True(p.Game == "Magic" || p.Game == "Lorcana"));
    }

    [Fact]
    public async Task Get_with_csv_games_normalizes_whitespace()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var normalized = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Lorcana");
        var withWhitespace = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=%20Magic%20,%20Lorcana%20");

        Assert.NotNull(normalized);
        Assert.NotNull(withWhitespace);
        Assert.Equal(normalized.Select(p => p.PrintingId), withWhitespace.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_csv_games_removes_duplicates()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var single = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");
        var duplicates = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Magic,Magic");

        Assert.NotNull(single);
        Assert.NotNull(duplicates);
        Assert.Equal(single.Select(p => p.PrintingId), duplicates.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_single_set_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Alpha", printing.SetName);
    }

    [Fact]
    public async Task Get_with_unique_cards_returns_oldest_printing_for_each_card()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true");

        Assert.NotNull(printings);
        Assert.Equal(4, printings.Count);
        var lightningBolt = Assert.Single(printings, p => p.CardId == TestDataSeeder.LightningBoltCardId);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, lightningBolt.PrintingId);
        Assert.Equal("Alpha", lightningBolt.SetName);
    }

    [Fact]
    public async Task Get_with_unique_cards_set_filter_matches_later_printing_but_returns_oldest()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true&set=Beta");
        var noMatch = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true&set=Unknown");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, printing.PrintingId);
        Assert.Equal("Alpha", printing.SetName);
        Assert.NotNull(noMatch);
        Assert.Empty(noMatch);
    }

    [Fact]
    public async Task Get_with_multiple_sets_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha,Beta");

        Assert.NotNull(printings);
        Assert.Equal(2, printings.Count);
        Assert.Contains(printings, p => p.SetName == "Alpha");
        Assert.Contains(printings, p => p.SetName == "Beta");
    }

    [Fact]
    public async Task Get_with_single_rarity_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.Equal("Common", p.Rarity));
    }

    [Fact]
    public async Task Get_with_multiple_rarities_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common,Uncommon");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.True(p.Rarity == "Common" || p.Rarity == "Uncommon"));
    }

    [Fact]
    public async Task Get_with_combined_csv_filters()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic&rarity=Common,Uncommon");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p =>
        {
            Assert.Equal("Magic", p.Game);
            Assert.True(p.Rarity == "Common" || p.Rarity == "Uncommon");
        });
    }

    [Fact]
    public async Task Get_with_empty_csv_returns_all()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var allPrintings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings");
        var emptyFilter = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=");

        Assert.NotNull(allPrintings);
        Assert.NotNull(emptyFilter);
        Assert.Equal(allPrintings.Select(p => p.PrintingId), emptyFilter.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_game_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=magic");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=MAGIC");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_set_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=alpha");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=ALPHA");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_rarity_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=common");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=COMMON");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_pages_return_all_printings_in_stable_order_without_duplicates()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var allPrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?page=1&pageSize=500");
        Assert.NotNull(allPrintings);
        Assert.True(allPrintings!.Count > 2);

        var pagedPrintings = new List<PrintingDto>();
        for (var page = 1; page <= allPrintings.Count; page++)
        {
            var pagePrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
                $"/api/cards/printings?page={page}&pageSize=2");
            Assert.NotNull(pagePrintings);
            pagedPrintings.AddRange(pagePrintings!);
            if (pagePrintings.Count < 2)
            {
                break;
            }
        }

        Assert.Equal(allPrintings.Select(p => p.PrintingId), pagedPrintings.Select(p => p.PrintingId));
        Assert.Equal(pagedPrintings.Count, pagedPrintings.Select(p => p.PrintingId).Distinct().Count());
    }
}
