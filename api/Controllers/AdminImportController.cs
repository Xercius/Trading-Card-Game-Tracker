// api/Controllers/AdminImportController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using api.Filters;
using api.Importing;
using api.Middleware;

namespace api.Controllers;

[ApiController]
[RequireUserHeader]
[AdminGuard] // all endpoints require admin
[Route("api/admin/import")]
public sealed class AdminImportController : ControllerBase
{
    private readonly Dictionary<string, ISourceImporter> _importersByKey;

    private static readonly Dictionary<string, string> SourceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // canonical keys
        ["LorcanaJSON"] = "LorcanaJSON",
        ["FabDb"] = "FabDb",

        // friendly aliases
        ["lorcana-json"] = "LorcanaJSON",
        ["lorcanajson"] = "LorcanaJSON",
        ["fabdb"] = "FabDb",

        // legacy aliases
        ["lorcana"] = "LorcanaJSON",
        ["disneylorcana"] = "LorcanaJSON",
        ["fab"] = "FabDb",
        ["fleshandblood"] = "FabDb",
    };

    private static readonly Dictionary<string, string> CanonicalToImporterKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LorcanaJSON"] = "lorcanajson",
        ["FabDb"] = "fabdb",
    };

    public AdminImportController(IEnumerable<ISourceImporter> importers)
    {
        _importersByKey = importers.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetCanonicalSource(string source)
    {
        if (SourceMap.TryGetValue(source, out var canonical)) return canonical;
        return source;
    }

    private bool TryResolveImporter(string source, out string canonical, out ISourceImporter importer)
    {
        importer = null!;
        canonical = string.Empty;

        if (string.IsNullOrWhiteSpace(source)) return false;

        canonical = GetCanonicalSource(source.Trim());

        var importerKey = CanonicalToImporterKey.TryGetValue(canonical, out var mapped)
            ? mapped
            : canonical;

        return _importersByKey.TryGetValue(importerKey, out importer);
    }

    // GET /api/admin/import/sources
    [HttpGet("sources")]
    public ActionResult<object> GetSources()
    {
        var payload = _importersByKey.Values
            .Select(importer =>
            {
                var canonical = GetCanonicalSource(importer.Key);
                var aliases = SourceMap
                    .Where(kvp => string.Equals(kvp.Value, canonical, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .Where(alias => !string.Equals(alias, canonical, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new
                {
                    key = canonical,
                    importerKey = importer.Key,
                    importer.DisplayName,
                    importer.SupportedGames,
                    aliases
                };
            })
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);

        return Ok(payload);
    }

    // POST /api/admin/import/{key}?set=...&dryRun=true&limit=500
    [HttpPost("{key}")]
    [HttpPost("remote/{key}")]
    public async Task<ActionResult<ImportSummary>> ImportRemote(
        string key,
        [FromQuery] string? set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!TryResolveImporter(key, out var canonical, out var importer))
            return NotFound(new { error = $"Importer '{key}' not registered." });

        var me = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: dryRun,
            Upsert: true,
            Limit: limit,
            UserId: me?.Id,
            SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        result.Source = canonical;
        return Ok(result);
    }

    // POST /api/admin/import/{key}/file?dryRun=true&limit=500
    [HttpPost("{key}/file")]
    public async Task<ActionResult<ImportSummary>> ImportFromFile(
        string key,
        IFormFile file,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!TryResolveImporter(key, out var canonical, out var importer))
            return NotFound(new { error = $"Importer '{key}' not registered." });

        await using var stream = file.OpenReadStream();

        var me = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: dryRun,
            Upsert: true,
            Limit: limit,
            UserId: me?.Id);

        var result = await importer.ImportFromFileAsync(stream, options, ct);
        result.Source = canonical;
        return Ok(result);
    }
}
