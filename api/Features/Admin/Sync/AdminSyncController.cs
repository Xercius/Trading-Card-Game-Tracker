using api.Common.Errors;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Features.Admin.Sync.Services;
using api.Importing;
using api.Infrastructure.Auth;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin.Sync;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationHandler.SchemeName)]
[Route("api/admin/sync")]
public sealed class AdminSyncController(
    ImporterRegistry registry,
    AppDbContext db,
    AdminSyncExecutionTracker executionTracker,
    ILogger<AdminSyncController> logger) : ControllerBase
{
    private const string SwuImporterKey = "swu";
    private const string SwuGameName = "Star Wars Unlimited";
    private const string SyncRouteKey = "star-wars-unlimited";

    [HttpPost("star-wars-unlimited")]
    public async Task<ActionResult<AdminSyncStatusResponse>> RunStarWarsUnlimited(CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
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

            setCodes = await ResolveSwuSetCodesAsync(ct);
            if (setCodes.Length == 0)
            {
                return this.CreateValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["sets"] = ["At least one Star Wars Unlimited set must be available before a sync can run."]
                    },
                    title: "No Star Wars Unlimited sets available.",
                    detail: ProblemTypes.BadRequest.DefaultDetail);
            }

            foreach (var setCode in setCodes)
            {
                var syncTimestamp = DateTimeOffset.UtcNow;
                var summary = await importer.ImportFromRemoteAsync(
                    new ImportOptions(
                        DryRun: false,
                        Upsert: true,
                        SetCode: setCode),
                    ct);

                MergeSummary(combined, summary);
                await RecordSyncHistoryAsync(setCode, syncTimestamp, ct);
            }

            var completedAt = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "Admin sync completed for {Source}. SetCount={SetCount}, Created={Created}, Updated={Updated}, Errors={Errors}",
                SwuImporterKey,
                setCodes.Length,
                combined.CardsCreated + combined.PrintingsCreated,
                combined.CardsUpdated + combined.PrintingsUpdated,
                combined.Errors);

            return Ok(BuildResponse("Succeeded", startedAt, completedAt, setCodes.Length, combined));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var completedAt = DateTimeOffset.UtcNow;
            logger.LogError(ex, "Admin sync failed for {Source}.", SwuImporterKey);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                BuildResponse("Failed", startedAt, completedAt, setCodes.Length, combined, ex.Message));
        }
        finally
        {
            executionTracker.Complete(SyncRouteKey);
        }
    }

    private async Task<string[]> ResolveSwuSetCodesAsync(CancellationToken ct)
    {
        var discoveredCodes = await db.SwuSets
            .AsNoTracking()
            .Select(s => s.Code)
            .Concat(
                db.CardPrintings
                    .AsNoTracking()
                    .Where(p => p.Card.Game == SwuGameName)
                    .Select(p => p.Set))
            .ToListAsync(ct);

        return discoveredCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task RecordSyncHistoryAsync(string setCode, DateTimeOffset syncedAt, CancellationToken ct)
    {
        var entry = await db.ImportSyncHistories
            .FirstOrDefaultAsync(h => h.ImporterKey == SwuImporterKey && h.SetCode == setCode, ct);

        if (entry is null)
        {
            db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = SwuImporterKey,
                SetCode = setCode,
                LastSyncedAt = syncedAt
            });
        }
        else
        {
            entry.LastSyncedAt = syncedAt;
        }

        await db.SaveChangesAsync(ct);
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
