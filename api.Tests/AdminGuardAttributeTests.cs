using api.Authentication;
using api.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Reflection;
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

    [Fact]
    public void AdminGuard_RejectsSpoofedLoopbackInForwardedFor()
    {
        var attribute = new AdminGuardAttribute();
        var proxyIp = IPAddress.Parse("10.0.0.5");
        var attacker = IPAddress.Parse("198.51.100.25");

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
                ["X-Forwarded-For"] = $"127.0.0.1, {attacker}, {proxyIp}"
            });

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    [Fact]
    public void ResolveEffectiveClientIp_WalksRightToLeftSkippingTrustedProxies()
    {
        var proxyEdge = IPAddress.Parse("10.0.0.5");
        var proxyInner = IPAddress.Parse("10.0.0.6");
        var client = IPAddress.Parse("203.0.113.60");

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
        };
        options.KnownProxies.Add(proxyEdge);
        options.KnownProxies.Add(proxyInner);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = proxyEdge;
        httpContext.Request.Headers["X-Forwarded-For"] = $"{client}, {proxyInner}";

        var resolved = InvokeResolveEffectiveClientIp(httpContext, options);

        Assert.Equal(client, resolved);
    }

    [Fact]
    public void AdminGuard_ForwardLimitExceededDeniesNonAdmin()
    {
        var attribute = new AdminGuardAttribute();
        var proxyIp = IPAddress.Parse("10.0.0.5");

        var context = CreateContext(
            proxyIp,
            user: null,
            options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.ForwardLimit = 1;
                options.KnownProxies.Add(proxyIp);
            },
            new Dictionary<string, string>
            {
                ["X-Forwarded-For"] = "198.51.100.20, 198.51.100.21"
            });

        attribute.OnActionExecuting(context);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(context.Result);
    }

    [Fact]
    public void AdminGuard_ForwardLimitExceededStillAllowsAdmins()
    {
        var attribute = new AdminGuardAttribute();
        var proxyIp = IPAddress.Parse("10.0.0.5");
        var adminUser = new CurrentUser(10, "admin", true);

        var context = CreateContext(
            proxyIp,
            adminUser,
            options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.ForwardLimit = 1;
                options.KnownProxies.Add(proxyIp);
            },
            new Dictionary<string, string>
            {
                ["X-Forwarded-For"] = "198.51.100.20, 198.51.100.21"
            });

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void ResolveEffectiveClientIp_NormalizesIpv6AndIpv4Mapped()
    {
        var proxy = IPAddress.Parse("10.0.0.5");
        var innerProxy = IPAddress.Parse("192.0.2.10");
        var client = IPAddress.Parse("2001:db8::1");

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
        };
        options.KnownProxies.Add(proxy);
        options.KnownProxies.Add(innerProxy);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = proxy;
        httpContext.Request.Headers["X-Forwarded-For"] = $"{client}, ::ffff:{innerProxy}";

        var resolved = InvokeResolveEffectiveClientIp(httpContext, options);

        Assert.Equal(client, resolved);
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

    private static IPAddress? InvokeResolveEffectiveClientIp(HttpContext httpContext, ForwardedHeadersOptions? options)
    {
        var method = typeof(AdminGuardAttribute).GetMethod(
            "ResolveEffectiveClientIp",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (IPAddress?)method!.Invoke(null, new object?[] { httpContext, options });
    }
}
