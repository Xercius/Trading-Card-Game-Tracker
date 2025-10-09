using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api.Tests.Fixtures;

namespace api.Tests;

public static class HttpClientExtensions
{
    private const string DefaultPassword = "Password123!";

    private static readonly IReadOnlyDictionary<int, string> UsernameById = new Dictionary<int, string>
    {
        [TestDataSeeder.AdminUserId] = "admin",
        [TestDataSeeder.AliceUserId] = "alice",
        [TestDataSeeder.BobUserId] = "bob"
    };

    public static HttpClient WithUser(this HttpClient client, int userId)
    {
        if (!UsernameById.TryGetValue(userId, out var username))
        {
            throw new ArgumentOutOfRangeException(nameof(userId), $"Unknown test user id: {userId}");
        }

        client.DefaultRequestHeaders.Authorization = null;

        var response = client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DefaultPassword))
            .GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();

        var payload = response.Content.ReadFromJsonAsync<LoginResponse>().GetAwaiter().GetResult();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Authentication response was invalid.");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return client;
    }

    public static HttpClient AsAdmin(this HttpClient client)
        => client.WithUser(TestDataSeeder.AdminUserId);

    private sealed record LoginRequest(string Username, string Password);

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserDto User);

    private sealed record UserDto(int Id, string Username, string DisplayName, bool IsAdmin);
}
