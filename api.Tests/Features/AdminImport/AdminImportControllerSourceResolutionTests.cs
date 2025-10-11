using api.Features.Admin.Import;
using api.Importing;
using api.Shared.Importing;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace api.Tests.Features.AdminImport;

public sealed class AdminImportControllerSourceResolutionTests
{
    private static readonly (string Source, string ExpectedKey)[] KnownSourceMappings =
    [
        ("lorcanajson", "lorcanajson"),
        ("fabdb", "fabdb"),
        ("scryfall", "scryfall"),
        ("swccgdb", "swccgdb"),
        ("swu", "swu"),
        ("pokemon", "pokemon"),
        ("guardians", "guardians"),
        ("dicemasters", "dicemasters"),
        ("tftcg", "tftcg"),
        ("dummy", "dummy"),
    ];

    public static IEnumerable<object[]> SourceMappings => KnownSourceMappings
        .Select(mapping => new object[] { mapping.Source, mapping.ExpectedKey });

    private static readonly MethodInfo TryResolveImporterMethod = typeof(AdminImportController)
        .GetMethod("TryResolveImporter", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate TryResolveImporter.");

    [Theory]
    [MemberData(nameof(SourceMappings))]
    public void TryResolveImporter_WithKnownSources_ResolvesRegisteredImporter(string source, string expectedKey)
    {
        var controller = CreateController();
        var parameters = new object?[] { source, null };

        var resolved = (bool)TryResolveImporterMethod.Invoke(controller, parameters)!;

        Assert.True(resolved);
        var importer = Assert.IsAssignableFrom<ISourceImporter>(parameters[1]);
        Assert.Equal(expectedKey, importer.Key);
    }

    private static AdminImportController CreateController()
    {
        var importers = KnownSourceMappings
            .Select(mapping => (ISourceImporter)new StubImporter(mapping.ExpectedKey))
            .ToArray();
        var registry = new ImporterRegistry(importers);
        return new AdminImportController(registry, new FileParser(), NullLogger<AdminImportController>.Instance);
    }

    private sealed class StubImporter : ISourceImporter
    {
        private readonly string _key;

        // The backing field avoids CS9124 when compiling on .NET 9.
        public StubImporter(string key)
        {
            _key = key;
        }

        public string Key => _key;

        public string DisplayName => $"{_key} importer";

        public IEnumerable<string> SupportedGames => Array.Empty<string>();

        public Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
            => Task.FromResult(new ImportSummary { Source = _key });

        public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
            => Task.FromResult(new ImportSummary { Source = _key });
    }
}
