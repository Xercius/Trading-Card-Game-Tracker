using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace api.Tests.Helpers;

public static class HttpContextTestHelpers
{
    // Ensure Request.ContentType = application/json for actions with [FromBody]
    public static T EnsureJson<T>(this T c) where T : ControllerBase
    {
        var http = c.ControllerContext?.HttpContext ?? new DefaultHttpContext();
        http.Request.ContentType = "application/json";
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    // Attach an X-User-Id header to an HttpClient for simulating a logged-in user
    public static HttpClient WithUser(this HttpClient client, int userId)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }
}