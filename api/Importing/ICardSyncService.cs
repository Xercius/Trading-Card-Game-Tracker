namespace api.Importing;

/// <summary>
/// Coordinates card synchronization workflows across import sources.
/// </summary>
public interface ICardSyncService
{
    /// <summary>
    /// Syncs new and updated cards for a source using either full or incremental mode.
    /// </summary>
    /// <param name="importerKey">Importer key to sync (for example, <c>swu</c> or <c>scryfall</c>).</param>
    /// <param name="setCode">Optional set/expansion code for sources that partition syncs by set.</param>
    /// <param name="updatedSince">
    /// Optional lower-bound UTC timestamp for incremental syncs.
    /// If null, implementations may resolve the timestamp from <see cref="GetLastSyncTimeAsync(string, string?, CancellationToken)"/>.
    /// </param>
    /// <param name="forceFullSync">When true, ignores incremental timestamps and performs a full sync.</param>
    /// <param name="limit">Optional record limit for controlled sync runs.</param>
    /// <param name="ct">Cancellation token for the sync operation.</param>
    /// <returns>A summary of created, updated, and errored records for the sync run.</returns>
    Task<ImportSummary> SyncNewAndUpdatedCardsAsync(
        string importerKey,
        string? setCode = null,
        DateTimeOffset? updatedSince = null,
        bool forceFullSync = false,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the last successful sync time for an importer and optional set.
    /// </summary>
    /// <param name="importerKey">Importer key to look up.</param>
    /// <param name="setCode">Optional set/expansion code associated with the sync timestamp.</param>
    /// <param name="ct">Cancellation token for the query.</param>
    /// <returns>The last successful sync timestamp in UTC, or null when no prior sync exists.</returns>
    Task<DateTimeOffset?> GetLastSyncTimeAsync(string importerKey, string? setCode = null, CancellationToken ct = default);

    /// <summary>
    /// Updates the last successful sync time for an importer and optional set.
    /// </summary>
    /// <param name="importerKey">Importer key to update.</param>
    /// <param name="setCode">Optional set/expansion code associated with the sync timestamp.</param>
    /// <param name="syncedAt">UTC timestamp to store as the most recent successful sync time.</param>
    /// <param name="ct">Cancellation token for the update operation.</param>
    Task UpdateLastSyncTimeAsync(
        string importerKey,
        string? setCode,
        DateTimeOffset syncedAt,
        CancellationToken ct = default);
}
