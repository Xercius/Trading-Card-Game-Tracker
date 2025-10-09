using System;
using System.Collections.Generic;
using System.Net;
using api.Authentication;
using api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void AdminGuard_RejectsForwardedRemoteNonAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var proxyIp = IPAddress.Parse("10.1.0.5");
        var remoteClient = IPAddress.Parse("203.0.113.50");

        var context = CreateContext(
            proxyIp,
            user: null,
            options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.KnownProxies.Add(proxyIp);
            },
            new Dictionary<string, string>
            {
                ["X-Forwarded-For"] = remoteClient.ToString()
            });

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    [Fact]
    public void AdminGuard_IgnoresSpoofedForwardedHeadersFromUntrustedRemote()
    {
        var attribute = new AdminGuardAttribute();
        var untrustedRemote = IPAddress.Parse("198.51.100.23");

        var context = CreateContext(
            untrustedRemote,
            user: null,
            options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            new Dictionary<string, string>
            {
                ["X-Forwarded-For"] = IPAddress.Loopback.ToString(),
            });

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    private static ActionExecutingContext CreateContext(
        IPAddress? remoteIp,
        CurrentUser? user,
        Action<ForwardedHeadersOptions>? configureForwarding = null,
        IDictionary<string, string>? headers = null)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        if (configureForwarding is not null)
        {
            services.Configure(configureForwarding);
        }

        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        httpContext.Connection.RemoteIpAddress = remoteIp;

        if (user is not null)
        {
            var identity = new System.Security.Claims.ClaimsIdentity("test");
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()));
            identity.AddClaim(new System.Security.Claims.Claim("username", user.Username));
            identity.AddClaim(new System.Security.Claims.Claim("is_admin", user.IsAdmin ? "true" : "false"));
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                httpContext.Request.Headers[header.Key] = header.Value;
            }
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }
}
