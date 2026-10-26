using api.Data;
using api.Features.Admin.Import;
using api.Importing;
using api.Shared.Importing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace api.Tests.Features.AdminImport;

public sealed class AdminImportControllerSourceResolutionTests
{
    private static readonly (string Source, string ExpectedKey)[] SupportedSourceMappings =
    [
        ("swu", "swu"),
        ("swudb", "swu"),
    ];

    private static readonly string[] UnsupportedSources =
    [
        "scryfall",
        "swccgdb",
        "lorcanajson",
        "pokemon",
        "fabdb",
        "guardians",
        "dicemasters",
        "transformers",
        "dummy",
    ];

    public static IEnumerable<object[]> SourceMappings => SupportedSourceMappings
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

    [Theory]
    [MemberData(nameof(UnsupportedSourceNames))]
    public void TryResolveImporter_WithUnsupportedSources_RejectsEvenWhenRegistered(string source)
    {
        var controller = CreateController();
        var parameters = new object?[] { source, null };

        var resolved = (bool)TryResolveImporterMethod.Invoke(controller, parameters)!;

        Assert.False(resolved);
    }

    [Fact]
    public void GetOptions_OnlyReturnsSwu_WhenOtherImportersAreRegistered()
    {
        var response = CreateController().GetOptions();
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var options = Assert.IsType<ImportOptionsResponse>(result.Value);

        var source = Assert.Single(options.Sources);
        Assert.Equal("swu", source.ImporterKey);
    }

    public static IEnumerable<object[]> UnsupportedSourceNames => UnsupportedSources
        .Select(source => new object[] { source });

    private static AdminImportController CreateController()
    {
        var importers = SupportedSourceMappings.Select(mapping => mapping.ExpectedKey)
            .Concat(UnsupportedSources)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => (ISourceImporter)new StubImporter(key))
            .ToArray();
        var registry = new ImporterRegistry(importers);
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(dbOptions);
        db.Database.EnsureCreated();
        return new AdminImportController(registry, new FileParser(), db, NullLogger<AdminImportController>.Instance);
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
