using System.Net.Http.Json;
using api.Features.Cards.Dtos;
using api.Tests.Fixtures;
using Xunit;

namespace api.Tests.Features.Cards;

public class PrintingsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
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
}
