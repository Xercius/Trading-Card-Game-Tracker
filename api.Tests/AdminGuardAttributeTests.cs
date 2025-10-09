using System.Collections.Generic;
using System.Net;
using api.Authentication;
using api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace api.Tests;

public class AdminGuardAttributeTests
{
    [Fact]
    public void AdminGuard_AllowsLoopbackRequestsWithoutUser()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Loopback, user: null);

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AdminGuard_AllowsRemoteAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Parse("203.0.113.42"), new CurrentUser(1, "admin", true));

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AdminGuard_RejectsRemoteNonAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Parse("203.0.113.24"), user: new CurrentUser(2, "user", false));

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    [Fact]
    public void AdminGuard_DoesNotTrustHostHeader()
    {
        var attribute = new AdminGuardAttribute();
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), user: null);
        context.HttpContext.Request.Host = new HostString("localhost");

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    private static ActionExecutingContext CreateContext(IPAddress? remoteIp, CurrentUser? user)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;

        if (user is not null)
        {
            var identity = new System.Security.Claims.ClaimsIdentity("test");
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()));
            identity.AddClaim(new System.Security.Claims.Claim("username", user.Username));
            identity.AddClaim(new System.Security.Claims.Claim("is_admin", user.IsAdmin ? "true" : "false"));
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }
}
