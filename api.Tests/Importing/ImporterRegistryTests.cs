using api.Importing;
using api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Xunit;

namespace api.Tests.Importing;

public sealed class ImporterRegistryTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly string[] ExpectedKeys =
    [
        "dicemasters",
        "fabdb",
        "guardians",
        "lorcanajson",
        "pokemon",
        "scryfall",
        "swccgdb",
        "swu",
        "tftcg",
    ];

    [Fact]
    public void Registry_Resolves_Each_Importer_By_Key()
    {
        using var scope = factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ImporterRegistry>();

        var actual = registry.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expected = ExpectedKeys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expected, actual);

        foreach (var key in ExpectedKeys)
        {
            Assert.True(registry.TryGet(key, out var importer));
            Assert.Equal(key, importer.Key);
        }
    }
}
