using System.Collections.Generic;
using System.Net.Http.Json;
using api.Features.Cards.Dtos;
using api.Tests;
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

        var result = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=A1");

        var printings = Assert.NotNull(result);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, printing.PrintingId);
        Assert.Equal("A1", printing.Number);
    }

    [Fact]
    public async Task Get_with_unknown_number_returns_empty_list()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var result = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=UNKNOWN");

        var printings = Assert.NotNull(result);
        Assert.Empty(printings);
    }
}
