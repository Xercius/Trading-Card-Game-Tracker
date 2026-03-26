namespace api.Tests;

public static class HttpClientExtensions
{
    /// <summary>
    /// Returns the client unchanged. Authentication is no longer required;
    /// this method exists for test-call compatibility.
    /// </summary>
    public static HttpClient WithUser(this HttpClient client, int _ = 0) => client;

    public static HttpClient AsAdmin(this HttpClient client) => client;
}
