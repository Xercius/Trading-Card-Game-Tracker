using api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Authentication;

public class HeaderAuthenticationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GuardedEndpoint_WithMissingUser_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/value/collection/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GuardedEndpoint_WithValidUser_ReturnsCurrentUserData()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var response = await client.GetAsync("/api/user/me");
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(TestDataSeeder.AliceUserId, user!.Id);
        Assert.Equal("alice", user.Username);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public async Task UserList_RequiresAuthentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/user/list");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserList_ReturnsData_WhenAuthenticated()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId);

        var users = await client.GetFromJsonAsync<UserResponse[]>("/api/user/list");

        Assert.NotNull(users);
        Assert.NotEmpty(users!);
    }

    private sealed record UserResponse(int Id, string Username, string DisplayName, bool IsAdmin);
}
