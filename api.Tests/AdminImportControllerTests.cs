// api.Tests/AdminImportControllerTests.cs
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;
using api.Tests.Fixtures;


namespace api.Tests;

public class AdminImportControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Sources_WithoutUserHeader_IsForbidden()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/admin/import/sources");
        // AdminGuard should block when no user header present:
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sources_WithAdminHeader_IsOk()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/import/sources");
        req.Headers.Add("X-User-Id", "1"); // seeded admin user in test fixture
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<object>();
        Assert.NotNull(payload);
    }

    [Fact]
    public async Task ImportRemote_LegacyAlias_Fab_MapsToFabDb()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/fab?set=WTR&dryRun=true&limit=1");
        req.Headers.Add("X-User-Id", "1");
        var res = await client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ImportRemote_LegacyAlias_Lorcana_MapsToLorcanaJson()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/import/lorcana?set=TFC&dryRun=true&limit=1");
        req.Headers.Add("X-User-Id", "1");
        var res = await client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode);
    }
}
