using System;
using Microsoft.AspNetCore.HttpOverrides;

namespace api.Infrastructure.Startup;

internal sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public ForwardedHeaders ForwardedHeaders { get; set; } = ForwardedHeaders.None;

    public int? ForwardLimit { get; set; }
        = null;

    public string[] KnownProxies { get; set; } = Array.Empty<string>();

    public NetworkEntry[] KnownNetworks { get; set; } = Array.Empty<NetworkEntry>();

    public sealed class NetworkEntry
    {
        public string? Prefix { get; set; }
            = null;

        public int PrefixLength { get; set; }
            = 0;
    }
}
