using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Xunit;

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
        payload!.Should().Contain(u => u.Id == Seed.AdminUserId);
        payload.Should().Contain(u => u.Id == Seed.SecondaryUserId);
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

    [Fact]
    public async Task UpdateUser_WithNullBody_ReturnsValidationProblem()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PutAsync(
            $"/api/user/{Seed.AdminUserId}",
            JsonContent.Create<object?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Invalid payload");
        problem.Errors.Should().ContainKey("request");
        problem.Errors["request"].Should().Contain("A request body is required.");
    }

    [Fact]
    public async Task DeleteLegacyUser_LastAdmin_ReturnsConflict()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.DeleteAsync($"/api/user/{Seed.AdminUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Cannot remove last administrator");
        problem.Detail.Should().Be("At least one administrator must remain.");
    }

    [Fact]
    public async Task SetAdminLegacy_LastAdmin_ReturnsConflict()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/user/{Seed.AdminUserId}/admin?value=false");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Cannot remove last administrator");
        problem.Detail.Should().Be("At least one administrator must remain.");
    }

    [Fact]
    public async Task SetAdminAlias_LastAdmin_ReturnsConflict()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/users/{Seed.AdminUserId}/admin?value=false");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Cannot remove last administrator");
        problem.Detail.Should().Be("At least one administrator must remain.");
    }

    private sealed record UserResponseContract(int Id, string Username, string DisplayName, bool IsAdmin);

    private sealed record AdminUserResponseContract(int Id, string Name, string Username, string DisplayName, bool IsAdmin, DateTime CreatedUtc);
}
