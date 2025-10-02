using System;
using System.Collections.Generic;
using api.Data;

namespace api.Importing;

public sealed class DummyImporter : ISourceImporter
{
    private readonly AppDbContext _db;
    public DummyImporter(AppDbContext db) => _db = db;
    public string Key => "dummy";
    public string DisplayName => "Dummy";
    public IEnumerable<string> SupportedGames => Array.Empty<string>();

    public Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
        => ImportFromFileAsync(Stream.Null, options, ct);

    public async Task<ImportSummary> ImportFromFileAsync(Stream _, ImportOptions options, CancellationToken ct = default)
    {
        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            // no-op example; replace with real creates/updates, then _db.SaveChanges();
            await Task.CompletedTask;
            return new ImportSummary
            {
                Source = Key,
                DryRun = options.DryRun,
                CardsCreated = 0,
                CardsUpdated = 0,
                PrintingsCreated = 0,
                PrintingsUpdated = 0,
                Errors = 0,
                Messages = { "Dummy importer ran" }
            };
        });
    }
}