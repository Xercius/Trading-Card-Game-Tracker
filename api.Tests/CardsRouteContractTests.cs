using System.Net;
using System.Threading.Tasks;
using api.Tests.Fixtures;
using api.Tests.Helpers;
using Xunit;

namespace api.Tests;

public class CardsRouteContractTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Singular_path_returns_404()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var res = await client.GetAsync("/api/card?skip=0&take=1");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Plural_path_returns_200()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);
        var res = await client.GetAsync("/api/cards?skip=0&take=1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
