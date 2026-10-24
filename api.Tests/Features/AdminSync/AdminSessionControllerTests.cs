using System.Net;
using System.Net.Http.Json;
using api.Models;
using api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.AdminSync;

public sealed class AdminSessionControllerTests
{
    private const string SessionPath = "/api/admin/sync/session";

    [Fact]
    public async Task Login_ValidAdministrator_CreatesSecureScopedSessionAndCanLogout()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory);
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);

        var response = await client.PostAsJsonAsync(SessionPath, new { username = "session-admin", password = "test-password" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Secure-AdminSync="));
        Assert.Contains("path=/api/admin/sync", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        var session = await SetCsrfAsync(client);
        Assert.True(session.Authenticated);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(SessionPath)).StatusCode);
        Assert.False((await SetCsrfAsync(client)).Authenticated);
    }

    [Theory]
    [InlineData("wrong", true, true)]
    [InlineData("test-password", false, true)]
    [InlineData("test-password", true, false)]
    public async Task Login_InvalidCredentialsOrNonAdminOrUnprovisioned_ReturnsUnauthorized(
        string password, bool isAdmin, bool hasPassword)
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory, isAdmin, hasPassword);
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);

        var response = await client.PostAsJsonAsync(SessionPath, new { username = "session-admin", password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False((await SetCsrfAsync(client)).Authenticated);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryHeader_ReturnsBadRequest()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        using var client = CreateClient(factory);
        await client.GetAsync(SessionPath);

        var response = await client.PostAsJsonAsync(SessionPath, new { username = "unknown", password = "wrong" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Session_ValidAdministrator_CanReadSyncStatusButCannotMutateWithoutCsrf()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory);
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(SessionPath,
            new { username = "session-admin", password = "test-password" })).StatusCode);

        var status = await client.GetAsync("/api/admin/sync/star-wars-unlimited/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Admin-CSRF");
        var mutation = await client.PostAsync("/api/admin/sync/star-wars-unlimited", null);
        Assert.Equal(HttpStatusCode.BadRequest, mutation.StatusCode);
    }

    [Fact]
    public async Task Session_AfterTwentyMinutes_ExpiresWithoutRenewal()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory);
        var clock = new SessionClock();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<CookieAuthenticationOptions>("AdminSession", options => options.TimeProvider = clock)));
        using var client = configuredFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(SessionPath,
            new { username = "session-admin", password = "test-password" })).StatusCode);
        Assert.True((await SetCsrfAsync(client)).Authenticated);
        clock.Advance(TimeSpan.FromMinutes(21));

        Assert.False((await SetCsrfAsync(client)).Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(SessionPath)).StatusCode);
    }

    [Fact]
    public async Task Login_RepeatedAttempts_ReturnsTooManyRequests()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(SessionPath, new { username = "unknown", password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(SessionPath, new { username = "unknown", password = "wrong" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryHeader_RejectsSessionMutation()
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory);
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(SessionPath,
            new { username = "session-admin", password = "test-password" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Admin-CSRF");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync(SessionPath)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_AfterAdminDemotionOrPasswordChange_IsRevoked(bool changePassword)
    {
        using var factory = new TestingWebAppFactory();
        await factory.ResetStateAsync();
        await SeedUserAsync(factory);
        using var client = CreateClient(factory);
        await SetCsrfAsync(client);
        await client.PostAsJsonAsync(SessionPath, new { username = "session-admin", password = "test-password" });
        await factory.ExecuteDbContextAsync(async db =>
        {
            var user = db.Users.Single(user => user.Username == "session-admin");
            if (changePassword)
            {
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "replacement-password");
            }
            else
            {
                user.IsAdmin = false;
            }
            await db.SaveChangesAsync();
        });

        Assert.False((await SetCsrfAsync(client)).Authenticated);
    }

    private static HttpClient CreateClient(TestingWebAppFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static async Task<SessionState> SetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync(SessionPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionState>())!;
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-Admin-CSRF");
        client.DefaultRequestHeaders.Add("X-Admin-CSRF", session.CsrfToken);
        return session;
    }

    private static Task SeedUserAsync(TestingWebAppFactory factory, bool isAdmin = true, bool hasPassword = true) =>
        factory.ExecuteDbContextAsync(async db =>
        {
            var user = new User { Username = "session-admin", DisplayName = "Admin", IsAdmin = isAdmin };
            user.PasswordHash = hasPassword ? new PasswordHasher<User>().HashPassword(user, "test-password") : null;
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

    private sealed record SessionState(bool Authenticated, string CsrfToken);

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
