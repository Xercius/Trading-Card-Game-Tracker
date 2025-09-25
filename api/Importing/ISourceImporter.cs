namespace api.Importing;

public interface ISourceImporter
{
    /// Unique key. Example: "scryfall", "swccgdb", "lorcanajson", "swu".
    string Key { get; }

    /// Pulls from a remote API this importer knows how to query.
    Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default);

    /// Parses a file you pass in (CSV/JSON, etc.).
    Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default);
}
