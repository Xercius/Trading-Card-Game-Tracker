using System;
using System.Net;
using api.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace api.Filters;

/// <summary>Allows only loopback requests or authenticated administrators.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminGuardAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;
        var forwardedOptions = http.RequestServices.GetService(typeof(IOptions<ForwardedHeadersOptions>))
            as IOptions<ForwardedHeadersOptions>;

        var clientIp = ResolveClientIp(http, forwardedOptions?.Value);

        if (IsTrusted(clientIp, forwardedOptions?.Value))
        {
            base.OnActionExecuting(context);
            return;
        }

        var currentUser = http.GetCurrentUser();
        if (currentUser?.IsAdmin == true)
        {
            base.OnActionExecuting(context);
            return;
        }

        context.Result = new ForbidResult();
    }

    private static IPAddress? ResolveClientIp(HttpContext httpContext, ForwardedHeadersOptions? options)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;

        if (options is null)
        {
            return remoteIp;
        }

        if (!IsTrustedProxy(remoteIp, options))
        {
            return remoteIp;
        }

        var forwardedFeature = httpContext.Features.Get<IForwardedHeadersFeature>();
        if (forwardedFeature?.ForwardedFor is { Count: > 0 })
        {
            foreach (var forwarded in forwardedFeature.ForwardedFor)
            {
                if (forwarded is not null)
                {
                    return forwarded;
                }
            }
        }

        if (options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor))
        {
            var headerName = options.ForwardedForHeaderName ?? ForwardedHeadersDefaults.XForwardedForHeaderName;
            if (TryParseAddressFromHeader(httpContext.Request.Headers[headerName], out var forwardedIp))
            {
                return forwardedIp;
            }
        }

        if (TryParseAddressFromHeader(httpContext.Request.Headers["X-Real-IP"], out var realIp))
        {
            return realIp;
        }

        return remoteIp;
    }

    private static bool IsTrustedProxy(IPAddress? address, ForwardedHeadersOptions options)
    {
        if (address is null || IPAddress.IsLoopback(address))
        {
            return true;
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (address.Equals(proxy))
            {
                return true;
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (network?.Contains(address) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrusted(IPAddress? address, ForwardedHeadersOptions? options)
    {
        if (address is null || IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (options is null)
        {
            return false;
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (address.Equals(proxy))
            {
                return true;
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (network?.Contains(address) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAddressFromHeader(StringValues headerValues, out IPAddress? address)
    {
        foreach (var value in headerValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (TryParseIp(segment, out address))
                {
                    return true;
                }
            }
        }

        address = null;
        return false;
    }

    private static bool TryParseIp(string value, out IPAddress? address)
    {
        value = value.Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        if (IPAddress.TryParse(value, out var parsed))
        {
            address = parsed;
            return true;
        }

        if (value.StartsWith("[") && value.Contains(']'))
        {
            var closingIndex = value.IndexOf(']');
            if (closingIndex > 1)
            {
                var inner = value.Substring(1, closingIndex - 1);
                if (IPAddress.TryParse(inner, out parsed))
                {
                    address = parsed;
                    return true;
                }
            }
        }
        else
        {
            var lastColon = value.LastIndexOf(':');
            if (lastColon > 0)
            {
                var withoutPort = value[..lastColon];
                if (IPAddress.TryParse(withoutPort, out parsed))
                {
                    address = parsed;
                    return true;
                }
            }
        }

        address = null;
        return false;
    }
}
