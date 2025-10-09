using System;
using System.Net;
using api.Infrastructure.Startup;
using api.Tests.Features.AdminImport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Infrastructure.Startup;

public static class ServiceCollectionExtensionsTests
{
    [Fact]
    public static void ValidateCorsCredentialsWithOrigins_Throws_WhenCredentialsEnabledWithoutOrigins()
    {
        var options = new CorsPolicyOptions
        {
            AllowCredentials = true,
            Origins = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceCollectionExtensions.ValidateCorsCredentialsWithOrigins(options, "Cors:Origins"));

        Assert.Contains("Cors:Origins", exception.Message);
    }

    [Fact]
    public static void ValidateCorsCredentialsWithOrigins_DoesNotThrow_WhenOriginsProvided()
    {
        var options = new CorsPolicyOptions
        {
            AllowCredentials = true,
            Origins = new[] { "https://example.com" }
        };

        ServiceCollectionExtensions.ValidateCorsCredentialsWithOrigins(options, "Cors:Origins");
    }

    [Fact]
    public static void ParseKnownProxy_ThrowsAndLogs_WhenInvalid()
    {
        var loggerProvider = new TestLoggerProvider();
        var logger = loggerProvider.CreateLogger(typeof(ServiceCollectionExtensions).FullName!);

        var exception = Assert.Throws<FormatException>(() =>
            ServiceCollectionExtensions.ParseKnownProxy("not-an-ip", "ForwardedHeaders:KnownProxies:0", logger));

        Assert.Contains("not-an-ip", exception.Message);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("not-an-ip", StringComparison.Ordinal));
    }

    [Fact]
    public static void ParseKnownProxy_ReturnsAddress_WhenValid()
    {
        var result = ServiceCollectionExtensions.ParseKnownProxy(
            "203.0.113.5",
            "ForwardedHeaders:KnownProxies:0",
            NullLogger<ServiceCollectionExtensions>.Instance);

        Assert.Equal(IPAddress.Parse("203.0.113.5"), result);
    }

    [Fact]
    public static void ParseKnownNetwork_ThrowsAndLogs_WhenPrefixInvalid()
    {
        var loggerProvider = new TestLoggerProvider();
        var logger = loggerProvider.CreateLogger(typeof(ServiceCollectionExtensions).FullName!);
        var entry = new ForwardedHeadersSettings.NetworkEntry
        {
            Prefix = "invalid",
            PrefixLength = 24
        };

        var exception = Assert.Throws<FormatException>(() => ServiceCollectionExtensions.ParseKnownNetwork(
            entry,
            "ForwardedHeaders:KnownNetworks:0:Prefix",
            "ForwardedHeaders:KnownNetworks:0:PrefixLength",
            logger));

        Assert.Contains("invalid", exception.Message);
        Assert.Contains(loggerProvider.Entries, log =>
            log.Level == LogLevel.Warning && log.Message.Contains("invalid", StringComparison.Ordinal));
    }

    [Fact]
    public static void ParseKnownNetwork_ThrowsAndLogs_WhenPrefixLengthInvalid()
    {
        var loggerProvider = new TestLoggerProvider();
        var logger = loggerProvider.CreateLogger(typeof(ServiceCollectionExtensions).FullName!);
        var entry = new ForwardedHeadersSettings.NetworkEntry
        {
            Prefix = "192.0.2.0",
            PrefixLength = 33
        };

        var exception = Assert.Throws<FormatException>(() => ServiceCollectionExtensions.ParseKnownNetwork(
            entry,
            "ForwardedHeaders:KnownNetworks:0:Prefix",
            "ForwardedHeaders:KnownNetworks:0:PrefixLength",
            logger));

        Assert.Contains("PrefixLength", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(loggerProvider.Entries, log =>
            log.Level == LogLevel.Warning && log.Message.Contains("33", StringComparison.Ordinal));
    }

    [Fact]
    public static void ParseKnownNetwork_ReturnsNetwork_WhenValid()
    {
        var entry = new ForwardedHeadersSettings.NetworkEntry
        {
            Prefix = "2001:db8::",
            PrefixLength = 64
        };

        var result = ServiceCollectionExtensions.ParseKnownNetwork(
            entry,
            "ForwardedHeaders:KnownNetworks:0:Prefix",
            "ForwardedHeaders:KnownNetworks:0:PrefixLength",
            NullLogger<ServiceCollectionExtensions>.Instance);

        Assert.Equal(IPAddress.Parse("2001:db8::"), result.Prefix);
        Assert.Equal(64, result.PrefixLength);
    }
}
