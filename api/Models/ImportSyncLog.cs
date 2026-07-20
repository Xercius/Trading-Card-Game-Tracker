using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
/// Tracks the timestamp of the most recent successful import run for a given
/// importer source and optional set/expansion code.  Used to drive incremental
/// imports: the next run can pass <c>LastSyncedAt</c> as <c>ImportOptions.UpdatedSince</c>
/// so only cards updated since the last sync are fetched from the remote API.
/// </summary>
[Index(nameof(Source), nameof(SetCode), IsUnique = true)]
public sealed class ImportSyncLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Importer key (e.g. <c>"swu"</c>, <c>"pokemon"</c>).  Matches the <c>Key</c> property of <c>ISourceImporter</c>.
    /// </summary>
    [MaxLength(64)]
    public required string Source { get; set; }

    /// <summary>
    /// Optional expansion/set code (e.g. <c>"SOR"</c>, <c>"TWI"</c>).
    /// <see langword="null"/> when the importer operates across all sets at once.
    /// </summary>
    [MaxLength(32)]
    public string? SetCode { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful import completion for this source + set.
    /// Pass this value as <c>ImportOptions.UpdatedSince</c> to perform an incremental sync.
    /// </summary>
    public DateTimeOffset LastSyncedAt { get; set; }
}
