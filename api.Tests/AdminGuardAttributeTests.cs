using System.Collections.Generic;
using System.Net;
using api.Filters;
using api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace api.Tests;

public class AdminGuardAttributeTests
{
    [Fact]
    public void AdminGuard_AllowsLocalRequestsWithoutUser()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Loopback, includeLocalhostHostHeader: false, user: null);

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AdminGuard_AllowsRequestsWhenHostIsLocalhost()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), includeLocalhostHostHeader: true, user: null);

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AdminGuard_AllowsRemoteAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(
            IPAddress.Parse("203.0.113.42"),
            includeLocalhostHostHeader: false,
            user: new CurrentUser(1, "admin", true));

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AdminGuard_RejectsRemoteNonAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Parse("203.0.113.24"), includeLocalhostHostHeader: false, user: null);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    private static ActionExecutingContext CreateContext(IPAddress? remoteIp, bool includeLocalhostHostHeader, CurrentUser? user)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        if (includeLocalhostHostHeader)
        {
            httpContext.Request.Host = new HostString("localhost");
        }

        if (user is not null)
        {
            httpContext.Items["User"] = user;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }
}
