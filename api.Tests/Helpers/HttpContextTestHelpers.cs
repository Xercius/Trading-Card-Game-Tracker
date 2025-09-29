using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
}
