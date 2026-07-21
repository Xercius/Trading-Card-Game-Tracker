using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace api.Models;

/// <summary>
/// Records each SWU import (sync) operation for audit and incremental-sync tracking.
/// Distinct from <see cref="ImportSyncHistory"/> which stores only the latest sync
/// timestamp per importer; <c>SyncLog</c> appends a row for every sync run.
/// </summary>
[Index(nameof(SwuSetId), nameof(StartedAt))]
public sealed class SyncLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="SwuSet"/> that was synced.
    /// Null when the operation targets all sets or is not set-scoped.
    /// </summary>
    public int? SwuSetId { get; set; }

    /// <summary>
    /// Navigation property to the synced expansion set.
    /// </summary>
    public SwuSet? SwuSet { get; set; }

    /// <summary>
    /// UTC timestamp when this sync operation was initiated.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when this sync operation completed successfully.
    /// Null if the sync is still in progress or failed before completion.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Whether this was an incremental sync driven by the <c>updatedAt</c> filter
    /// (<c>true</c>) or a full re-import of the set (<c>false</c>).
    /// </summary>
    public bool IsIncremental { get; set; }

    /// <summary>
    /// The <c>UpdatedSince</c> lower-bound timestamp used for incremental syncs.
    /// Null for full imports.
    /// </summary>
    public DateTimeOffset? UpdatedSince { get; set; }

    /// <summary>
    /// Total number of card records returned by the API during this sync.
    /// </summary>
    public int CardsReturned { get; set; }

    /// <summary>
    /// Number of card rows inserted or updated as a result of this sync.
    /// </summary>
    public int CardsUpserted { get; set; }

    /// <summary>
    /// Outcome of the sync operation.
    /// Expected values: <c>"Pending"</c>, <c>"Succeeded"</c>, <c>"Failed"</c>.
    /// </summary>
    [Required]
    [MaxLength(16)]
    public required string Status { get; set; }

    /// <summary>
    /// Error message captured when <see cref="Status"/> is <c>"Failed"</c>.
    /// Null for successful syncs.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
