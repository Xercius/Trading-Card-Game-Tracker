using System;
using System.Collections.Generic;
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

        var clientIp = ResolveEffectiveClientIp(http, forwardedOptions?.Value);

        if (clientIp is not null && IPAddress.IsLoopback(clientIp))
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

    private static IPAddress? ResolveEffectiveClientIp(HttpContext httpContext, ForwardedHeadersOptions? options)
    {
        var remoteIp = NormalizeIp(httpContext.Connection.RemoteIpAddress);

        if (options is null)
        {
            return remoteIp;
        }

        if (remoteIp is null)
        {
            return null;
        }

        if (!IsIpTrusted(remoteIp, options))
        {
            return remoteIp;
        }

        if (!options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor))
        {
            return remoteIp;
        }

        var forwardedValues = GetForwardedChain(httpContext, options);
        if (forwardedValues is null)
        {
            return remoteIp;
        }

        if (options.ForwardLimit > 0 && forwardedValues.Count > options.ForwardLimit)
        {
            return null;
        }

        for (var i = forwardedValues.Count - 1; i >= 0; i--)
        {
            var hop = forwardedValues[i];
            if (IsIpTrusted(hop, options))
            {
                continue;
            }

            return hop;
        }

        return remoteIp;
    }

    private static List<IPAddress>? GetForwardedChain(HttpContext httpContext, ForwardedHeadersOptions options)
    {
        var forwardedFeature = httpContext.Features.Get<IForwardedHeadersFeature>();
        if (forwardedFeature?.ForwardedFor is { Count: > 0 })
        {
            var fromFeature = new List<IPAddress>(forwardedFeature.ForwardedFor.Count);
            foreach (var forwarded in forwardedFeature.ForwardedFor)
            {
                var normalized = NormalizeIp(forwarded);
                if (normalized is null)
                {
                    return null;
                }

                fromFeature.Add(normalized);
            }

            return fromFeature;
        }

        var headerName = options.ForwardedForHeaderName ?? ForwardedHeadersDefaults.XForwardedForHeaderName;
        var headerValues = httpContext.Request.Headers[headerName];
        if (headerValues.Count == 0)
        {
            return null;
        }

        var parsed = new List<IPAddress>();
        if (!TryParseForwardedFor(headerValues, parsed))
        {
            return null;
        }

        return parsed.Count == 0 ? null : parsed;
    }

    private static bool IsIpTrusted(IPAddress? address, ForwardedHeadersOptions? options)
    {
        if (address is null || options is null)
        {
            return false;
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (proxy is null)
            {
                continue;
            }

            if (proxy.Equals(address))
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

    private static bool TryParseForwardedFor(StringValues headerValues, List<IPAddress> results)
    {
        foreach (var value in headerValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();
                if (!TryParseIp(segment, out var parsed))
                {
                    results.Clear();
                    return false;
                }

                results.Add(parsed);
            }
        }

        return true;
    }

    private static bool TryParseIp(string value, out IPAddress? address)
    {
        value = value.Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        if (value.StartsWith("[") && value.Contains(']'))
        {
            var closingIndex = value.IndexOf(']');
            if (closingIndex > 1)
            {
                var inner = value.Substring(1, closingIndex - 1);
                if (IPAddress.TryParse(inner, out var parsedInner))
                {
                    address = NormalizeIp(parsedInner);
                    return true;
                }
            }
        }
        else
        {
            if (IPAddress.TryParse(value, out var parsedDirect))
            {
                address = NormalizeIp(parsedDirect);
                return true;
            }

            var lastColon = value.LastIndexOf(':');
            if (lastColon > 0 && value.IndexOf(':') == lastColon)
            {
                var withoutPort = value[..lastColon];
                if (IPAddress.TryParse(withoutPort, out var parsedWithoutPort))
                {
                    address = NormalizeIp(parsedWithoutPort);
                    return true;
                }
            }
        }

        address = null;
        return false;
    }

    private static IPAddress? NormalizeIp(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
