using api.Common.Errors;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Features.Admin.Sync.Services;
using api.Importing;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin.Sync;

[ApiController]
[Authorize(AuthenticationSchemes = "AdminToken,AdminSession", Roles = "Admin")]
[Route("api/admin/sync")]
public sealed class AdminSyncController(
    ImporterRegistry registry,
    AppDbContext db,
    AdminSyncExecutionTracker executionTracker,
    TimeProvider timeProvider,
    ILogger<AdminSyncController> logger) : ControllerBase
{
    private const string SwuImporterKey = "swu";
    private const string CatalogHistoryKey = "swu-catalog";
    private const string GlobalCatalogScope = "GlobalCatalog";
    private const string SyncRouteKey = "star-wars-unlimited";

    [HttpGet("star-wars-unlimited/logs")]
    public async Task<ActionResult<AdminSyncLogsResponse>> GetStarWarsUnlimitedLogs(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is <= 0 or > 200)
        {
            limit = 50;
        }

        var totalCount = await db.SyncLogs.CountAsync(ct);

        var allLogs = await db.SyncLogs
            .AsNoTracking()
            .Select(l => new AdminSyncLogEntry(
                l.Id,
                l.SetId,
                l.Set != null ? l.Set.Code : null,
                l.StartedAt,
                l.CompletedAt,
                l.IsIncremental,
                l.UpdatedSince,
                l.CardsReturned,
                l.CardsUpserted,
                l.Status,
                l.ErrorMessage))
            .ToListAsync(ct);

        var logs = allLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToList();

        return Ok(new AdminSyncLogsResponse(
            Source: SwuImporterKey,
            TotalCount: totalCount,
            Logs: logs));
    }

    [HttpGet("star-wars-unlimited/status")]
    public async Task<ActionResult<AdminSyncStatusDetailsResponse>> GetStarWarsUnlimitedStatus(CancellationToken ct)
    {
        var history = (await db.ImportSyncHistories
            .AsNoTracking()
            .Where(h => h.ImporterKey == SwuImporterKey)
            .Select(h => new AdminSyncSetHistoryEntry(h.SetCode, h.LastSyncedAt))
            .ToArrayAsync(ct))
            .OrderByDescending(h => h.LastSyncedAt)
            .ThenBy(h => h.SetCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isRunning = executionTracker.TryGetStartedAt(SyncRouteKey, out var runningSince);
        var checkpoint = await db.ImportSyncHistories.AsNoTracking()
            .Where(h => h.ImporterKey == CatalogHistoryKey && h.SetCode == "")
            .Select(h => (DateTimeOffset?)h.LastSyncedAt)
            .SingleOrDefaultAsync(ct);
        var lastOutcome = (await db.SyncLogs.AsNoTracking()
            .Where(l => l.CatalogScope == GlobalCatalogScope && l.CompletedAt != null)
            .Select(l => new { l.StartedAt, l.Status })
            .ToListAsync(ct))
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefault()?.Status;
        var status = isRunning ? "Running" : "Idle";
        string[] messages = checkpoint is null
            ? ["No successful global sync recorded yet. The next sync will import the full catalog."]
            : Array.Empty<string>();

        return Ok(new AdminSyncStatusDetailsResponse(
            Source: SwuImporterKey,
            Status: status,
            RunningSince: isRunning ? runningSince : null,
            LastCompletedAt: checkpoint,
            HistoryCount: history.Length,
            History: history,
            Messages: messages,
            LastOutcome: lastOutcome));
    }

    [HttpPost("star-wars-unlimited")]
    public async Task<ActionResult<AdminSyncStatusResponse>> RunStarWarsUnlimited(
        CancellationToken ct,
        [FromQuery] bool forceFullSync = false)
    {
        var startedAt = timeProvider.GetUtcNow();
        if (!executionTracker.TryStart(SyncRouteKey, startedAt))
        {
            executionTracker.TryGetStartedAt(SyncRouteKey, out var runningSince);
            logger.LogWarning("Rejected concurrent admin sync request for {Source}.", SwuImporterKey);
            return Conflict(new AdminSyncStatusResponse(
                Source: SwuImporterKey,
                Status: "Running",
                StartedAt: runningSince == default ? startedAt : runningSince,
                CompletedAt: null,
                SetCount: 0,
                Created: 0,
                Updated: 0,
                Invalid: 0,
                Messages: ["A Star Wars Unlimited sync is already running."]));
        }

        string[] setCodes = [];
        var combined = new ImportSummary
        {
            Source = SwuImporterKey,
            DryRun = false
        };

        try
        {
            if (!registry.TryGet(SwuImporterKey, out var importer))
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    title: "Importer not found.",
                    detail: "The Star Wars Unlimited importer is not registered.");
            }

            var checkpoint = await db.ImportSyncHistories.AsNoTracking()
                .Where(h => h.ImporterKey == CatalogHistoryKey && h.SetCode == "")
                .Select(h => (DateTimeOffset?)h.LastSyncedAt)
                .SingleOrDefaultAsync(ct);
            var updatedSince = forceFullSync ? null : checkpoint?.AddMinutes(-1);
            var syncLog = await CreatePendingSyncLogAsync(startedAt, updatedSince, ct);
            try
            {
                var summary = await importer.ImportFromRemoteAsync(
                    new ImportOptions(DryRun: false, Upsert: true, UpdatedSince: updatedSince), ct);

                MergeSummary(combined, summary);
                setCodes = await ResolveSwuSetCodesAsync(ct);
                if (summary.Errors == 0)
                {
                    await RecordSuccessfulSyncAsync(syncLog, summary, startedAt, ct);
                }
                else
                {
                    await UpdateSyncLogFailedAsync(syncLog, "Import contained invalid records; synchronization checkpoint was not advanced.");
                }
            }
            catch (OperationCanceledException)
            {
                await UpdateSyncLogFailedAsync(syncLog, "Sync was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Admin catalog sync failed for {Source}.", SwuImporterKey);
                combined.Errors++;
                combined.Messages.Add("Catalog sync failed. Retry the sync; its checkpoint was not advanced.");
                await UpdateSyncLogFailedAsync(syncLog, "Import failed. See server logs for details.");
            }

            var completedAt = timeProvider.GetUtcNow();
            logger.LogInformation(
                "Admin sync completed for {Source}. SetCount={SetCount}, Created={Created}, Updated={Updated}, Errors={Errors}",
                SwuImporterKey,
                setCodes.Length,
                combined.CardsCreated + combined.PrintingsCreated,
                combined.CardsUpdated + combined.PrintingsUpdated,
                combined.Errors);

            return Ok(BuildResponse(combined.Errors == 0 ? "Succeeded" : "Failed", startedAt, completedAt, setCodes.Length, combined));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var completedAt = timeProvider.GetUtcNow();
            logger.LogError(ex, "Admin sync failed for {Source}.", SwuImporterKey);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                BuildResponse("Failed", startedAt, completedAt, setCodes.Length, combined, "Sync failed. See server logs for details."));
        }
        finally
        {
            executionTracker.Complete(SyncRouteKey);
        }
    }

    private async Task<SyncLog> CreatePendingSyncLogAsync(
        DateTimeOffset startedAt, DateTimeOffset? updatedSince, CancellationToken ct)
    {
        var syncLog = new SyncLog
        {
            StartedAt = startedAt,
            CatalogScope = GlobalCatalogScope,
            IsIncremental = updatedSince.HasValue,
            UpdatedSince = updatedSince,
            Status = "Pending"
        };
        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(ct);
        return syncLog;
    }

    private async Task RecordSuccessfulSyncAsync(
        SyncLog syncLog, ImportSummary summary, DateTimeOffset checkpoint, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var entry = await db.ImportSyncHistories
            .SingleOrDefaultAsync(h => h.ImporterKey == CatalogHistoryKey && h.SetCode == "", ct);
        if (entry is null)
        {
            db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = CatalogHistoryKey,
                SetCode = "",
                LastSyncedAt = checkpoint
            });
        }
        else
        {
            entry.LastSyncedAt = checkpoint;
        }

        syncLog.Status = "Succeeded";
        syncLog.CompletedAt = timeProvider.GetUtcNow();
        syncLog.CardsUpserted = summary.CardsCreated + summary.PrintingsCreated
                                + summary.CardsUpdated + summary.PrintingsUpdated;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task UpdateSyncLogFailedAsync(SyncLog syncLog, string errorMessage)
    {
        try
        {
            db.ChangeTracker.Clear();
            db.SyncLogs.Attach(syncLog);
            syncLog.Status = "Failed";
            syncLog.CompletedAt = timeProvider.GetUtcNow();
            syncLog.ErrorMessage = errorMessage;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist Failed SyncLog entry for set {SetId}.", syncLog.SetId);
        }
    }

    private async Task<string[]> ResolveSwuSetCodesAsync(CancellationToken ct)
    {
        var discoveredCodes = await db.Sets
            .Where(s => s.Game.NameNormalized == "star wars unlimited")
            .AsNoTracking()
            .Select(s => s.Code)
            .Concat(
                db.CardPrintings
                    .AsNoTracking()
                    .Where(p => p.Card.Game != null
                        && p.Card.Game.NameNormalized == "star wars unlimited")
                    .Select(p => p.Set))
            .ToListAsync(ct);

        return discoveredCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AdminSyncStatusResponse BuildResponse(
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        int setCount,
        ImportSummary summary,
        params string[] additionalMessages)
    {
        var messages = summary.Messages.Concat(additionalMessages.Where(message => !string.IsNullOrWhiteSpace(message))).ToArray();
        return new AdminSyncStatusResponse(
            Source: SwuImporterKey,
            Status: status,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            SetCount: setCount,
            Created: summary.CardsCreated + summary.PrintingsCreated,
            Updated: summary.CardsUpdated + summary.PrintingsUpdated,
            Invalid: summary.Errors,
            Messages: messages);
    }

    private static void MergeSummary(ImportSummary combined, ImportSummary summary)
    {
        combined.CardsCreated += summary.CardsCreated;
        combined.CardsUpdated += summary.CardsUpdated;
        combined.PrintingsCreated += summary.PrintingsCreated;
        combined.PrintingsUpdated += summary.PrintingsUpdated;
        combined.Errors += summary.Errors;
        combined.Messages.AddRange(summary.Messages);
    }
}
