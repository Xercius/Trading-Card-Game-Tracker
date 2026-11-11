using api.Common.Errors;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Features.Admin.Sync.Services;
using api.Importing;
using api.Importing.Pokemon;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    ILogger<AdminSyncController> logger,
    PokemonTcgdexImporter pokemonImporter) : ControllerBase
{
    private const string SwuImporterKey = "swu";
    private const string CatalogHistoryKey = "swu-catalog";
    private const string GlobalCatalogScope = "GlobalCatalog";
    private const string SyncRouteKey = "star-wars-unlimited";

    private const string SwccgImporterKey = "swccg";
    private const string SwccgCatalogHistoryKey = "swccg-catalog";
    private const string SwccgCatalogScope = "SwccgCatalog";
    private const string SwccgSyncRouteKey = "star-wars-ccg";

    private const string PokemonImporterKey = "pokemon";
    private const string PokemonSyncRouteKey = "pokemon";

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
            var syncLog = await CreatePendingSyncLogAsync(startedAt, updatedSince, GlobalCatalogScope, ct);
            try
            {
                var summary = await importer.ImportFromRemoteAsync(
                    new ImportOptions(DryRun: false, Upsert: true, UpdatedSince: updatedSince), ct);

                MergeSummary(combined, summary);
                setCodes = await ResolveSetCodesAsync("star wars unlimited", ct);
                if (summary.Errors == 0)
                {
                    await RecordSuccessfulSyncAsync(syncLog, summary, startedAt, CatalogHistoryKey, ct);
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

            return Ok(BuildResponse(combined.Errors == 0 ? "Succeeded" : "Failed", startedAt, completedAt, setCodes.Length, combined, SwuImporterKey));
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
                BuildResponse("Failed", startedAt, completedAt, setCodes.Length, combined, SwuImporterKey, "Sync failed. See server logs for details."));
        }
        finally
        {
            executionTracker.Complete(SyncRouteKey);
        }
    }

    [HttpGet("star-wars-ccg/logs")]
    public async Task<ActionResult<AdminSyncLogsResponse>> GetStarWarsCcgLogs(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is <= 0 or > 200)
        {
            limit = 50;
        }

        var totalCount = await db.SyncLogs.CountAsync(l => l.CatalogScope == SwccgCatalogScope, ct);

        var allLogs = await db.SyncLogs
            .AsNoTracking()
            .Where(l => l.CatalogScope == SwccgCatalogScope)
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
            Source: SwccgImporterKey,
            TotalCount: totalCount,
            Logs: logs));
    }

    [HttpGet("star-wars-ccg/status")]
    public async Task<ActionResult<AdminSyncStatusDetailsResponse>> GetStarWarsCcgStatus(CancellationToken ct)
    {
        var history = (await db.ImportSyncHistories
            .AsNoTracking()
            .Where(h => h.ImporterKey == SwccgImporterKey)
            .Select(h => new AdminSyncSetHistoryEntry(h.SetCode, h.LastSyncedAt))
            .ToArrayAsync(ct))
            .OrderByDescending(h => h.LastSyncedAt)
            .ThenBy(h => h.SetCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isRunning = executionTracker.TryGetStartedAt(SwccgSyncRouteKey, out var runningSince);
        var checkpoint = await db.ImportSyncHistories.AsNoTracking()
            .Where(h => h.ImporterKey == SwccgCatalogHistoryKey && h.SetCode == "")
            .Select(h => (DateTimeOffset?)h.LastSyncedAt)
            .SingleOrDefaultAsync(ct);
        var lastOutcome = (await db.SyncLogs.AsNoTracking()
            .Where(l => l.CatalogScope == SwccgCatalogScope && l.CompletedAt != null)
            .Select(l => new { l.StartedAt, l.Status })
            .ToListAsync(ct))
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefault()?.Status;
        var status = isRunning ? "Running" : "Idle";
        string[] messages = checkpoint is null
            ? ["No successful Star Wars CCG sync recorded yet. The next sync will import the catalog."]
            : Array.Empty<string>();

        return Ok(new AdminSyncStatusDetailsResponse(
            Source: SwccgImporterKey,
            Status: status,
            RunningSince: isRunning ? runningSince : null,
            LastCompletedAt: checkpoint,
            HistoryCount: history.Length,
            History: history,
            Messages: messages,
            LastOutcome: lastOutcome));
    }

    [HttpPost("star-wars-ccg")]
    public async Task<ActionResult<AdminSyncStatusResponse>> RunStarWarsCcg(
        CancellationToken ct,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AdminSwccgSyncRequest? request = null,
        [FromQuery] bool forceFullSync = false)
    {
        var startedAt = timeProvider.GetUtcNow();
        if (!executionTracker.TryStart(SwccgSyncRouteKey, startedAt))
        {
            executionTracker.TryGetStartedAt(SwccgSyncRouteKey, out var runningSince);
            logger.LogWarning("Rejected concurrent admin sync request for {Source}.", SwccgImporterKey);
            return Conflict(new AdminSyncStatusResponse(
                Source: SwccgImporterKey,
                Status: "Running",
                StartedAt: runningSince == default ? startedAt : runningSince,
                CompletedAt: null,
                SetCount: 0,
                Created: 0,
                Updated: 0,
                Invalid: 0,
                Messages: ["A Star Wars CCG sync is already running."]));
        }

        string[] setCodes = [];
        var combined = new ImportSummary
        {
            Source = SwccgImporterKey,
            DryRun = request?.DryRun ?? false
        };

        try
        {
            if (!registry.TryGet(SwccgImporterKey, out var importer))
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    title: "Importer not found.",
                    detail: "The Star Wars CCG importer is not registered.");
            }

            var checkpoint = await db.ImportSyncHistories.AsNoTracking()
                .Where(h => h.ImporterKey == SwccgCatalogHistoryKey && h.SetCode == "")
                .Select(h => (DateTimeOffset?)h.LastSyncedAt)
                .SingleOrDefaultAsync(ct);
            var updatedSince = forceFullSync ? null : checkpoint?.AddMinutes(-1);
            var syncLog = await CreatePendingSyncLogAsync(startedAt, updatedSince, SwccgCatalogScope, ct);
            try
            {
                var summary = await importer.ImportFromRemoteAsync(
                    new ImportOptions(
                        DryRun: request?.DryRun ?? false,
                        Upsert: true,
                        Limit: request?.Limit,
                        UpdatedSince: updatedSince), ct);

                MergeSummary(combined, summary);
                setCodes = await ResolveSetCodesAsync("star wars ccg", ct);
                if (summary.Errors == 0)
                {
                    await RecordSuccessfulSyncAsync(
                        syncLog,
                        summary,
                        startedAt,
                        summary.DryRun ? null : SwccgCatalogHistoryKey,
                        ct);
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
                logger.LogError(ex, "Admin catalog sync failed for {Source}.", SwccgImporterKey);
                combined.Errors++;
                combined.Messages.Add("Catalog sync failed. Retry the sync; its checkpoint was not advanced.");
                await UpdateSyncLogFailedAsync(syncLog, "Import failed. See server logs for details.");
            }

            var completedAt = timeProvider.GetUtcNow();
            logger.LogInformation(
                "Admin sync completed for {Source}. SetCount={SetCount}, Created={Created}, Updated={Updated}, Errors={Errors}",
                SwccgImporterKey,
                setCodes.Length,
                combined.CardsCreated + combined.PrintingsCreated,
                combined.CardsUpdated + combined.PrintingsUpdated,
                combined.Errors);

            return Ok(BuildResponse(combined.Errors == 0 ? "Succeeded" : "Failed", startedAt, completedAt, setCodes.Length, combined, SwccgImporterKey));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var completedAt = timeProvider.GetUtcNow();
            logger.LogError(ex, "Admin sync failed for {Source}.", SwccgImporterKey);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                BuildResponse("Failed", startedAt, completedAt, setCodes.Length, combined, SwccgImporterKey, "Sync failed. See server logs for details."));
        }
        finally
        {
            executionTracker.Complete(SwccgSyncRouteKey);
        }
    }

    [HttpGet("pokemon/status")]
    public async Task<ActionResult<AdminPokemonSyncStatusResponse>> GetPokemonStatus(CancellationToken ct)
    {
        var isRunning = executionTracker.TryGetStartedAt(PokemonSyncRouteKey, out var runningSince);

        var lastSetsSyncAt = (await db.ImportSyncHistories.AsNoTracking()
            .Where(h => h.ImporterKey == "pokemon-sets" && h.SetCode == "")
            .Select(h => (DateTimeOffset?)h.LastSyncedAt)
            .ToListAsync(ct))
            .OrderByDescending(h => h)
            .FirstOrDefault();

        var lastCardsSyncAt = (await db.ImportSyncHistories.AsNoTracking()
            .Where(h => h.ImporterKey == "pokemon-cards")
            .Select(h => (DateTimeOffset?)h.LastSyncedAt)
            .ToListAsync(ct))
            .OrderByDescending(h => h)
            .FirstOrDefault();

        var lastOutcome = (await db.SyncLogs.AsNoTracking()
            .Where(l => l.CatalogScope != null && l.CatalogScope.StartsWith("Pokemon:") && l.CompletedAt != null)
            .Select(l => new { l.StartedAt, l.Status })
            .ToListAsync(ct))
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefault()?.Status;

        var cardCountsBySetId = await db.CardPrintings.AsNoTracking()
            .Where(cp => cp.SetId != null && cp.CatalogSet != null && (cp.CatalogSet.Game.Name == "Pokemon TCG" || cp.CatalogSet.Game.NameNormalized == "pokemontcg" || cp.CatalogSet.Game.NameNormalized == "pokemon tcg"))
            .GroupBy(cp => cp.SetId!.Value)
            .Select(g => new { SetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SetId, g => g.Count, ct);

        var sets = await db.Sets.AsNoTracking()
            .Where(s => s.Game.Name == "Pokemon TCG" || s.Game.NameNormalized == "pokemontcg" || s.Game.NameNormalized == "pokemon tcg")
            .Select(s => new
            {
                s.Id,
                s.Code,
                Name = s.Name ?? s.Code,
                s.Era,
                s.ReleasedAt
            })
            .ToListAsync(ct);

        var syncedSetHistories = await db.ImportSyncHistories.AsNoTracking()
            .Where(h => h.ImporterKey == "pokemon-cards")
            .ToDictionaryAsync(h => h.SetCode.Trim(), h => h.LastSyncedAt, StringComparer.OrdinalIgnoreCase, ct);

        var setEntries = sets
            .Select(s =>
            {
                var codeTrimmed = s.Code.Trim();
                var hasHistory = syncedSetHistories.TryGetValue(codeTrimmed, out var syncedAt);
                cardCountsBySetId.TryGetValue(s.Id, out var countInDb);
                var isSynced = hasHistory || countInDb > 0;
                return new AdminPokemonSetEntry(
                    s.Id,
                    s.Code,
                    s.Name,
                    s.Era,
                    s.ReleasedAt?.ToString("yyyy-MM-dd"),
                    countInDb,
                    isSynced,
                    hasHistory ? syncedAt : null
                );
            })
            .OrderByDescending(s => s.ReleaseDate ?? "0000")
            .ThenBy(s => s.Name)
            .ToList();

        return Ok(new AdminPokemonSyncStatusResponse(
            IsRunning: isRunning,
            RunningSince: isRunning ? runningSince : null,
            LastSetsSyncAt: lastSetsSyncAt,
            LastCardsSyncAt: lastCardsSyncAt,
            LastOutcome: lastOutcome,
            Sets: setEntries
        ));
    }

    [HttpPost("pokemon/sets")]
    public async Task<IActionResult> SyncPokemonSets(CancellationToken ct)
    {
        var startedAt = timeProvider.GetUtcNow();
        if (!executionTracker.TryStart(PokemonSyncRouteKey, startedAt))
        {
            return Conflict(new { message = "A Pokémon sync is already running." });
        }

        try
        {
            var summary = await pokemonImporter.SyncSetsAsync(ct);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync Pokémon sets.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
        finally
        {
            executionTracker.Complete(PokemonSyncRouteKey);
        }
    }

    [HttpPost("pokemon/cards")]
    public async Task<IActionResult> SyncPokemonCards([FromBody] AdminPokemonCardsSyncRequest request, CancellationToken ct)
    {
        if (request?.SetCodes == null || request.SetCodes.Count == 0)
        {
            return BadRequest(new { message = "At least one set code is required." });
        }

        var startedAt = timeProvider.GetUtcNow();
        if (!executionTracker.TryStart(PokemonSyncRouteKey, startedAt))
        {
            return Conflict(new { message = "A Pokémon sync is already running." });
        }

        try
        {
            var summary = await pokemonImporter.SyncCardsAsync(request.SetCodes, request.ForceFullSync, request.DryRun, ct);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync Pokémon cards.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
        finally
        {
            executionTracker.Complete(PokemonSyncRouteKey);
        }
    }

    [HttpGet("pokemon/logs")]
    public async Task<ActionResult<AdminSyncLogsResponse>> GetPokemonLogs(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is <= 0 or > 200)
        {
            limit = 50;
        }

        var totalCount = await db.SyncLogs.CountAsync(l => l.CatalogScope != null && l.CatalogScope.StartsWith("Pokemon:"), ct);

        var rawLogs = await db.SyncLogs
            .AsNoTracking()
            .Where(l => l.CatalogScope != null && l.CatalogScope.StartsWith("Pokemon:"))
            .Select(l => new AdminSyncLogEntry(
                l.Id,
                l.SetId,
                l.Set != null ? l.Set.Code : l.CatalogScope!.Replace("Pokemon:", ""),
                l.StartedAt,
                l.CompletedAt,
                l.IsIncremental,
                l.UpdatedSince,
                l.CardsReturned,
                l.CardsUpserted,
                l.Status,
                l.ErrorMessage))
            .ToListAsync(ct);

        var allLogs = rawLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToList();

        return Ok(new AdminSyncLogsResponse(
            Source: PokemonImporterKey,
            TotalCount: totalCount,
            Logs: allLogs));
    }

    private async Task<SyncLog> CreatePendingSyncLogAsync(
        DateTimeOffset startedAt, DateTimeOffset? updatedSince, string scope, CancellationToken ct)
    {
        var syncLog = new SyncLog
        {
            StartedAt = startedAt,
            CatalogScope = scope,
            IsIncremental = updatedSince.HasValue,
            UpdatedSince = updatedSince,
            Status = "Pending"
        };
        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(ct);
        return syncLog;
    }

    private async Task RecordSuccessfulSyncAsync(
        SyncLog syncLog, ImportSummary summary, DateTimeOffset checkpoint, string? historyKey, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (historyKey is not null)
        {
            var entry = await db.ImportSyncHistories
                .SingleOrDefaultAsync(h => h.ImporterKey == historyKey && h.SetCode == "", ct);
            if (entry is null)
            {
                db.ImportSyncHistories.Add(new ImportSyncHistory
                {
                    ImporterKey = historyKey,
                    SetCode = "",
                    LastSyncedAt = checkpoint
                });
            }
            else
            {
                entry.LastSyncedAt = checkpoint;
            }
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

    private async Task<string[]> ResolveSetCodesAsync(string gameNormalized, CancellationToken ct)
    {
        var discoveredCodes = await db.Sets
            .Where(s => s.Game.NameNormalized == gameNormalized)
            .AsNoTracking()
            .Select(s => s.Code)
            .Concat(
                db.CardPrintings
                    .AsNoTracking()
                    .Where(p => p.Card.Game != null
                        && p.Card.Game.NameNormalized == gameNormalized)
                    .Select(p => p.CatalogSet == null ? null : p.CatalogSet.Code))
            .ToListAsync(ct);

        return discoveredCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
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
        string source = SwuImporterKey,
        params string[] additionalMessages)
    {
        var messages = summary.Messages.Concat(additionalMessages.Where(message => !string.IsNullOrWhiteSpace(message))).ToArray();
        return new AdminSyncStatusResponse(
            Source: source,
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
