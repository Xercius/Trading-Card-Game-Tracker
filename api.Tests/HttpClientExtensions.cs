using api.Tests.Fixtures;
using System.Net.Http.Headers;

namespace api.Tests;

public static class HttpClientExtensions
{
    public static HttpClient AsAdmin(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.AdminApiToken);
        return client;
    }
}
