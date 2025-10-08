using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using api.Common.Errors;
using api.Filters;
using api.Importing;
using api.Middleware;
using api.Shared.Importing;
using api.Shared.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace api.Features.Admin.Import;

[ApiController]
[RequireUserHeader]
[AdminGuard]
[Route("api/admin/import")]
public sealed class AdminImportController : ControllerBase
{
    private readonly ImporterRegistry _registry;
    private readonly FileParser _fileParser;
    private readonly ILogger<AdminImportController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> SourceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LorcanaJSON"] = "LorcanaJSON",
        ["FabDb"] = "FabDb",
        ["Scryfall"] = "Scryfall",
        ["Swccgdb"] = "Swccgdb",
        ["SwuDb"] = "SwuDb",
        ["PokemonTcg"] = "PokemonTcg",
        ["GuardiansLocal"] = "GuardiansLocal",
        ["DiceMastersDb"] = "DiceMastersDb",
        ["TransformersFm"] = "TransformersFm",
        ["Dummy"] = "Dummy",

        ["lorcana-json"] = "LorcanaJSON",
        ["lorcanajson"] = "LorcanaJSON",
        ["lorcana"] = "LorcanaJSON",
        ["disneylorcana"] = "LorcanaJSON",

        ["fabdb"] = "FabDb",
        ["fab"] = "FabDb",
        ["fleshandblood"] = "FabDb",

        ["scryfall"] = "Scryfall",
        ["swccgdb"] = "Swccgdb",
        ["swu"] = "SwuDb",
        ["swudb"] = "SwuDb",
        ["pokemontcg"] = "PokemonTcg",
        ["guardians"] = "GuardiansLocal",
        ["dicemasters"] = "DiceMastersDb",
        ["transformers"] = "TransformersFm",
        ["dummy"] = "Dummy",
    };

    private static readonly Dictionary<string, string> CanonicalToImporterKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LorcanaJSON"] = "lorcanajson",
        ["FabDb"] = "fabdb",
        ["Scryfall"] = "scryfall",
        ["Swccgdb"] = "swccgdb",
        ["SwuDb"] = "swudb",
        ["PokemonTcg"] = "pokemontcg",
        ["GuardiansLocal"] = "guardianslocal",
        ["DiceMastersDb"] = "dicemastersdb",
        ["TransformersFm"] = "transformersfm",
        ["Dummy"] = "dummy",
    };

    private static readonly Dictionary<string, ImportSetOption[]> StaticSetOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LorcanaJSON"] =
        [
            new ImportSetOption("TFC", "The First Chapter"),
            new ImportSetOption("ROTF", "Rise of the Floodborn"),
            new ImportSetOption("ITI", "Into the Inklands"),
            new ImportSetOption("UTS", "Ursula's Return"),
        ],
        ["FabDb"] =
        [
            new ImportSetOption("WTR", "Welcome to Rathe"),
            new ImportSetOption("ARC", "Arcane Rising"),
            new ImportSetOption("MON", "Monarch"),
            new ImportSetOption("DYN", "Dynasty"),
            new ImportSetOption("MST", "Monarch: Soulbound"),
        ],
    };

    public AdminImportController(ImporterRegistry registry, FileParser fileParser, ILogger<AdminImportController> logger)
    {
        _registry = registry;
        _fileParser = fileParser;
        _logger = logger;
    }

    [HttpGet("options")]
    public ActionResult<ImportOptionsResponse> GetOptions()
    {
        var stopwatch = RequestTiming.Start();
        var sources = _registry.All
            .Select(importer =>
            {
                var canonical = GetCanonicalSource(importer.Key);
                var importerKey = CanonicalToImporterKey.TryGetValue(canonical, out var mapped)
                    ? mapped
                    : importer.Key;
                var sets = StaticSetOptions.TryGetValue(canonical, out var options)
                    ? options
                    : Array.Empty<ImportSetOption>();
                return new ImportSourceOption(
                    canonical,
                    importerKey,
                    importer.DisplayName,
                    importer.SupportedGames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    sets
                );
            })
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new ImportOptionsResponse(sources);
        var durationMs = RequestTiming.Stop(stopwatch);
        _logger.LogInformation(
            "Admin import options retrieved in {DurationMs}ms. SourceCount={SourceCount}",
            durationMs,
            sources.Length);
        return Ok(response);
    }

    [HttpPost("dry-run")]
    [Consumes("application/json", "multipart/form-data")]
    public async Task<ActionResult<ImportPreviewResponse>> DryRun(CancellationToken ct)
    {
        const string operation = "dry-run";
        var parse = await ParseRequestAsync(ct);
        if (parse.Problem is not null)
        {
            LogProblem(operation, parse.Problem);
            return parse.Problem;
        }
        var request = parse.Request;
        if (request is null)
        {
            _logger.LogWarning("Admin import {Operation} rejected due to invalid request payload.", operation);
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "A valid import request is required." }
                },
                title: "Invalid request.",
                detail: ProblemTypes.BadRequest.DefaultDetail);
        }

        if (ValidateLimit(request.Limit, out var effectiveLimit) is { } limitProblem)
        {
            _logger.LogWarning(
                "Admin import {Operation} rejected due to invalid limit {RequestedLimit}.",
                operation,
                request.Limit);
            return limitProblem;
        }

        var importer = request.Importer;
        var user = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: true,
            Upsert: true,
            Limit: effectiveLimit,
            UserId: user?.Id,
            SetCode: request.Set);

        ImportSummary? summary = null;
        var stopwatch = RequestTiming.Start();
        try
        {
            if (request.File is { } file)
            {
                FileParseResult? parsed = null;
                try
                {
                    parsed = await _fileParser.ParseAsync(file, effectiveLimit, ct);
                    parsed.Stream.Position = 0;
                    summary = await importer.ImportFromFileAsync(parsed.Stream, options, ct);
                }
                finally
                {
                    parsed?.Dispose();
                }
            }
            else
            {
                summary = await importer.ImportFromRemoteAsync(options, ct);
            }
        }
        catch (FileParserException ex)
        {
            var durationMs = RequestTiming.Stop(stopwatch);
            LogImportFailure(operation, request, durationMs, ex, "FileParser");
            if (ex.Errors is not null)
            {
                return this.CreateValidationProblem(ex.Errors, title: ex.Message);
            }

            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["file"] = new[] { ex.Message }
                },
                title: ex.Message);
        }
        catch (Exception ex)
        {
            var durationMs = RequestTiming.Stop(stopwatch);
            LogImportFailure(operation, request, durationMs, ex, "Importer");
            var field = request.File is null ? "request" : "file";
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    [field] = new[] { ex.Message }
                },
                title: "Import failed");
        }

        var duration = RequestTiming.Stop(stopwatch);
        if (summary is null)
        {
            _logger.LogError("Admin import {Operation} failed: ImportSummary is null after import.", operation);
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["import"] = new[] { "Import did not produce a summary." }
                },
                title: "Import failed");
        }
        LogImportSuccess(operation, request, summary, duration);
        var response = BuildPreviewResponse(request, summary);
        return Ok(response);
    }

    [HttpPost("apply")]
    [Consumes("application/json", "multipart/form-data")]
    public async Task<ActionResult<ImportApplyResponse>> Apply(CancellationToken ct)
    {
        const string operation = "apply";
        var parse = await ParseRequestAsync(ct);
        if (parse.Problem is not null)
        {
            LogProblem(operation, parse.Problem);
            return parse.Problem;
        }
        var request = parse.Request;
        if (request is null)
        {
            _logger.LogWarning("Admin import {Operation} rejected due to invalid request payload.", operation);
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "A valid import request is required." }
                },
                title: "Invalid request.",
                detail: ProblemTypes.BadRequest.DefaultDetail);
        }

        if (ValidateLimit(request.Limit, out var effectiveLimit) is { } limitProblem)
        {
            _logger.LogWarning(
                "Admin import {Operation} rejected due to invalid limit {RequestedLimit}.",
                operation,
                request.Limit);
            return limitProblem;
        }

        var importer = request.Importer;
        var user = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: false,
            Upsert: true,
            Limit: effectiveLimit,
            UserId: user?.Id,
            SetCode: request.Set);

        ImportSummary? summary = null;
        var stopwatch = RequestTiming.Start();
        try
        {
            if (request.File is { } file)
            {
                FileParseResult? parsed = null;
                try
                {
                    parsed = await _fileParser.ParseAsync(file, ImportingOptions.MaxPreviewLimit, ct);
                    parsed.Stream.Position = 0;
                    summary = await importer.ImportFromFileAsync(parsed.Stream, options, ct);
                }
                finally
                {
                    parsed?.Dispose();
                }
            }
            else
            {
                summary = await importer.ImportFromRemoteAsync(options, ct);
            }
        }
        catch (FileParserException ex)
        {
            var durationMs = RequestTiming.Stop(stopwatch);
            LogImportFailure(operation, request, durationMs, ex, "FileParser");
            if (ex.Errors is not null)
            {
                return this.CreateValidationProblem(ex.Errors, title: ex.Message);
            }

            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["file"] = new[] { ex.Message }
                },
                title: ex.Message);
        }
        catch (Exception ex)
        {
            var durationMs = RequestTiming.Stop(stopwatch);
            LogImportFailure(operation, request, durationMs, ex, "Importer");
            var field = request.File is null ? "request" : "file";
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    [field] = new[] { ex.Message }
                },
                title: "Import failed");
        }

        var duration = RequestTiming.Stop(stopwatch);
        LogImportSuccess(operation, request, summary!, duration);
        return Ok(new ImportApplyResponse(
            Created: summary.CardsCreated + summary.PrintingsCreated,
            Updated: summary.CardsUpdated + summary.PrintingsUpdated,
            Skipped: 0,
            Invalid: summary.Errors));
    }

    private void LogProblem(string operation, ObjectResult problem)
    {
        if (problem.Value is ProblemDetails details)
        {
            _logger.LogWarning(
                "Admin import {Operation} request returned problem: {Title}",
                operation,
                details.Title);
        }
        else
        {
            _logger.LogWarning(
                "Admin import {Operation} request returned problem result.",
                operation);
        }
    }

    private void LogImportSuccess(string operation, ResolvedImportRequest request, ImportSummary summary, long durationMs)
    {
        var source = string.IsNullOrWhiteSpace(summary.Source) ? request.Importer.Key : summary.Source;
        var mode = request.File is null ? "remote" : "file";
        var created = summary.CardsCreated + summary.PrintingsCreated;
        var updated = summary.CardsUpdated + summary.PrintingsUpdated;
        var total = created + updated;

        _logger.LogInformation(
            "Admin import {Operation} completed for {Source} via {Mode} in {DurationMs}ms. ItemCount={ItemCount}, Created={Created}, Updated={Updated}, Errors={Errors}, Success={Success}",
            operation,
            source,
            mode,
            durationMs,
            total,
            created,
            updated,
            summary.Errors,
            true);
    }

    private void LogImportFailure(string operation, ResolvedImportRequest request, long durationMs, Exception exception, string errorType)
    {
        var source = request.Importer.Key;
        var mode = request.File is null ? "remote" : "file";

        _logger.LogError(
            exception,
            "Admin import {Operation} failed for {Source} via {Mode} after {DurationMs}ms. ErrorType={ErrorType}, ItemCount={ItemCount}, Created={Created}, Updated={Updated}, Errors={Errors}, Success={Success}",
            operation,
            source,
            mode,
            durationMs,
            errorType,
            0,
            0,
            0,
            0,
            false);
    }

    private async Task<ParseRequestResult> ParseRequestAsync(CancellationToken ct)
    {
        var queryLimitRaw = Request.Query.TryGetValue("limit", out var queryLimitValues)
            ? queryLimitValues.ToString()
            : null;
        if (!TryParseLimit(queryLimitRaw, out var queryLimit, out var queryProblem, this))
        {
            return new ParseRequestResult(null, queryProblem);
        }

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var source = form["source"].ToString();
            if (!TryResolveImporter(source, out var importer)) return new ParseRequestResult(null, null);

            var formLimitRaw = form.TryGetValue("limit", out var limitValues)
                ? limitValues.ToString()
                : null;
            if (!TryParseLimit(formLimitRaw, out var limit, out var limitProblem, this))
            {
                return new ParseRequestResult(null, limitProblem);
            }

            if (limit is null)
            {
                limit = queryLimit;
            }

            var set = form["set"].ToString();
            if (string.IsNullOrWhiteSpace(set)) set = null;

            return new ParseRequestResult(new ResolvedImportRequest(importer, set, limit, form.Files.FirstOrDefault()), null);
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return new ParseRequestResult(null, null);
            ImportRequestPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ImportRequestPayload>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return new ParseRequestResult(
                    null,
                    this.CreateValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["request"] = new[] { "The request body could not be parsed as JSON." }
                        },
                        title: "Invalid request.",
                        detail: ProblemTypes.BadRequest.DefaultDetail));
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.Source)) return new ParseRequestResult(null, null);
            if (!TryResolveImporter(payload.Source, out var importer)) return new ParseRequestResult(null, null);

            var limit = payload.Limit ?? queryLimit;
            return new ParseRequestResult(new ResolvedImportRequest(importer, payload.Set, limit, null), null);
        }
    }

    private static bool TryParseLimit(
        string? raw,
        out int? value,
        out ObjectResult? problem,
        AdminImportController controller)
    {
        value = null;
        problem = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (int.TryParse(raw, out var parsed))
        {
            value = parsed;
            return true;
        }

        problem = controller.CreateInvalidLimitProblem();
        return false;
    }

    private bool TryResolveImporter(string? source, out ISourceImporter importer)
    {
        importer = null!;
        if (string.IsNullOrWhiteSpace(source)) return false;
        var canonical = GetCanonicalSource(source);
        var importerKey = CanonicalToImporterKey.TryGetValue(canonical, out var mapped) ? mapped : canonical;
        return _registry.TryGet(importerKey, out importer);
    }

    private static string GetCanonicalSource(string source)
    {
        return SourceMap.TryGetValue(source, out var canonical) ? canonical : source;
    }

    private static ImportPreviewResponse BuildPreviewResponse(ResolvedImportRequest request, ImportSummary summary)
    {
        var defaultGame = request.Importer.SupportedGames.FirstOrDefault() ?? request.Set ?? "Unknown";
        var setLabel = request.Set ?? "(remote)";
        var rows = new List<ImportPreviewRow>();
        int newCount = summary.CardsCreated + summary.PrintingsCreated;
        int updateCount = summary.CardsUpdated + summary.PrintingsUpdated;

        if (newCount > 0)
        {
            rows.Add(new ImportPreviewRow(
                ExternalId: "new",
                Name: "New records",
                Game: defaultGame,
                Set: setLabel,
                Rarity: null,
                PrintingKey: null,
                ImageUrl: null,
                Price: null,
                Status: "New",
                Messages: new[] { $"{newCount} new entities will be created." }
            ));
        }

        if (updateCount > 0)
        {
            rows.Add(new ImportPreviewRow(
                ExternalId: "update",
                Name: "Existing records",
                Game: defaultGame,
                Set: setLabel,
                Rarity: null,
                PrintingKey: null,
                ImageUrl: null,
                Price: null,
                Status: "Update",
                Messages: new[] { $"{updateCount} entities will be updated." }
            ));
        }

        var invalidMessages = summary.Messages
            .Where(m => m.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var (message, index) in invalidMessages.Select((m, i) => (m, i)))
        {
            rows.Add(new ImportPreviewRow(
                ExternalId: $"invalid-{index}",
                Name: "Invalid row",
                Game: defaultGame,
                Set: setLabel,
                Rarity: null,
                PrintingKey: null,
                ImageUrl: null,
                Price: null,
                Status: "Invalid",
                Messages: new[] { message }
            ));
        }

        var infoMessages = summary.Messages
            .Except(invalidMessages, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var (message, index) in infoMessages.Select((m, i) => (m, i)))
        {
            rows.Add(new ImportPreviewRow(
                ExternalId: $"info-{index}",
                Name: "Info",
                Game: defaultGame,
                Set: setLabel,
                Rarity: null,
                PrintingKey: null,
                ImageUrl: null,
                Price: null,
                Status: "Info",
                Messages: new[] { message }
            ));
        }

        var summaryPayload = new ImportPreviewSummary(
            New: newCount,
            Update: updateCount,
            Duplicate: 0,
            Invalid: summary.Errors + invalidMessages.Length);

        return new ImportPreviewResponse(summaryPayload, rows.ToArray());
    }

    private static bool TryParseLimit(string? raw, out int? value, out ObjectResult? problem, AdminImportController controller)
    {
        value = null;
        problem = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (int.TryParse(raw, out var parsed))
        {
            value = parsed;
            return true;
        }

        problem = controller.CreateInvalidLimitProblem();
        return false;
    }

    private ObjectResult CreateInvalidLimitProblem()
    {
        return this.CreateValidationProblem(
            new Dictionary<string, string[]>
            {
                ["limit"] = new[]
                {
                    $"limit must be between {ImportingOptions.MinPreviewLimit} and {ImportingOptions.MaxPreviewLimit}"
                }
            },
            title: "Invalid limit",
            detail: ProblemTypes.BadRequest.DefaultDetail);
    }

    private ObjectResult? ValidateLimit(int? requested, out int effectiveLimit)
    {
        if (requested is null)
        {
            effectiveLimit = ImportingOptions.DefaultPreviewLimit;
            return null;
        }

        if (requested < ImportingOptions.MinPreviewLimit || requested > ImportingOptions.MaxPreviewLimit)
        {
            effectiveLimit = ImportingOptions.DefaultPreviewLimit;
            return CreateInvalidLimitProblem();
        }

        effectiveLimit = requested.Value;
        return null;
    }

    private sealed record ImportRequestPayload
    {
        public string Source { get; init; } = string.Empty;
        public string? Set { get; init; }
        public int? Limit { get; init; }
    }

    private sealed record ParseRequestResult(ResolvedImportRequest? Request, ObjectResult? Problem);

    private sealed record ResolvedImportRequest(
        ISourceImporter Importer,
        string? Set,
        int? Limit,
        IFormFile? File);
}

public sealed record ImportSourceOption(
    string Key,
    string ImporterKey,
    string DisplayName,
    string[] Games,
    ImportSetOption[] Sets);

public sealed record ImportSetOption(string Code, string Name);

public sealed record ImportOptionsResponse(ImportSourceOption[] Sources);

public sealed record ImportPreviewRow(
    string ExternalId,
    string Name,
    string Game,
    string Set,
    string? Rarity,
    string? PrintingKey,
    string? ImageUrl,
    decimal? Price,
    string Status,
    string[] Messages);

public sealed record ImportPreviewSummary(int New, int Update, int Duplicate, int Invalid);

public sealed record ImportPreviewResponse(ImportPreviewSummary Summary, ImportPreviewRow[] Rows);

public sealed record ImportApplyResponse(int Created, int Updated, int Skipped, int Invalid);
