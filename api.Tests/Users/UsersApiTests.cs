using System.Net;
using System.Net.Http.Json;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using api.Tests.Infrastructure;

namespace api.Tests.Users;

public class UsersApiTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task GetUsers_ReturnsSeededUsers()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<UserResponseContract>>();
        payload.Should().NotBeNull();
        payload!.Select(u => u.Id).Should().Contain(new[] { Seed.AdminUserId, Seed.SecondaryUserId });
    }

    [Fact]
    public async Task GetUserById_ReturnsSingleUserAndMissingReturns404()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync($"/api/user/{Seed.AdminUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponseContract>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(Seed.AdminUserId);
        user.Username.Should().Be("admin");

        var missing = await client.GetAsync("/api/user/9999");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedUserAndPersists()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/admin/users", new { name = "charlie" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<AdminUserResponseContract>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("charlie");
        created.Username.Should().Be("charlie");

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var stored = await db.Users.FindAsync(created.Id);
            stored.Should().NotBeNull();
            stored!.Username.Should().Be("charlie");
        });
    }

    [Fact]
    public async Task CreateUser_WithBlankName_ReturnsProblemDetails()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/admin/users", new { name = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Name required");
        problem.Detail.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record UserResponseContract(int Id, string Username, string DisplayName, bool IsAdmin);

    private sealed record AdminUserResponseContract(int Id, string Name, string Username, string DisplayName, bool IsAdmin, DateTime CreatedUtc);
}
