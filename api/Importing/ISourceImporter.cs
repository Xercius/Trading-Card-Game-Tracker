using System.Collections.Generic;

namespace api.Importing;

public interface ISourceImporter
{
    /// Unique key. Example: "scryfall", "swccgdb", "lorcanajson", "swu".
    string Key { get; }

    /// Human-readable source name (e.g., "Scryfall").
    string DisplayName { get; }

    /// Games this importer can populate (e.g., ["Magic"], ["Star Wars CCG"], ...).
    IEnumerable<string> SupportedGames { get; }

    /// Pulls from a remote API this importer knows how to query.
    Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default);

    /// Parses a file you pass in (CSV/JSON, etc.).
    Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default);
}
