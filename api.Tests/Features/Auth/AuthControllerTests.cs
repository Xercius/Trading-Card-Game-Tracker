using api.Data;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Features.Auth;

public sealed class AuthControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Login_WithNullPasswordHash_ReturnsUnauthorizedAsync()
    {
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Username == "alice");
            user.PasswordHash = null!;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Username = "alice", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorizedAsync()
    {
        await factory.ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Username = "alice", Password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingDisplayName_ReturnsProblemAsync()
    {
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Username == "alice");
            user.DisplayName = "   ";
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Username = "alice", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("User record invalid", problem!.Title);
        Assert.Equal("User record invalid: missing Username or DisplayName", problem.Detail);
    }

    [Fact]
    public async Task Impersonate_WithMissingUsername_ReturnsProblemAsync()
    {
        await factory.ResetDatabaseAsync();

        int targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Username == "bob");
            targetId = user.Id;
            user.Username = "   ";
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/impersonate", new { UserId = targetId });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("User record invalid", problem!.Title);
        Assert.Equal("User record invalid: missing Username or DisplayName", problem.Detail);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUserAsync()
    {
        await factory.ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Username = "alice", Password = "Password123!" });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal(TestDataSeeder.AliceUserId, payload.User.Id);
        Assert.Equal("alice", payload.User.Username);
        Assert.Equal("Alice", payload.User.DisplayName);
    }

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserDto User);

    private sealed record UserDto(int Id, string Username, string DisplayName, bool IsAdmin);
}
