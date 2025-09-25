using System.Globalization;
using System.Net.Http;
using api.Tests.Fixtures;

namespace api.Tests;

public static class HttpClientExtensions
{
    private const string HeaderName = "X-User-Id";

    public static HttpClient WithUser(this HttpClient client, int userId)
    {
        if (client.DefaultRequestHeaders.Contains(HeaderName))
        {
            client.DefaultRequestHeaders.Remove(HeaderName);
        }

        client.DefaultRequestHeaders.Add(HeaderName, userId.ToString(CultureInfo.InvariantCulture));
        return client;
    }

    public static HttpClient AsAdmin(this HttpClient client)
        => client.WithUser(TestDataSeeder.AdminUserId);
}
