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
[AdminGuard] // all endpoints require admin
[Route("api/admin/import")]
public class AdminImportController : ControllerBase
{
    private readonly IEnumerable<ISourceImporter> _importers;

    public AdminImportController(IEnumerable<ISourceImporter> importers)
    {
        _importers = importers;
    }

    private bool TryGetImporter(string key, out ISourceImporter importer)
    {
        importer = _importers.FirstOrDefault(i =>
            string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        return importer is not null;
    }

    // GET /api/admin/import/sources
    [HttpGet("sources")]
    public ActionResult<object> GetSources()
    {
        var data = _importers
            .Select(i => new { key = i.Key, name = i.DisplayName, games = i.SupportedGames })
            .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase);
        return Ok(data);
    }

    // POST /api/admin/import/{key}?set=...&dryRun=true&limit=500
    [HttpPost("{key}")]
    public async Task<ActionResult<ImportSummary>> ImportRemote(
        string key,
        [FromQuery] string? set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!TryGetImporter(key, out var importer))
            return NotFound(new { error = $"Importer '{key}' not registered." });

        var me = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: dryRun,
            Upsert: true,
            Limit: limit,
            UserId: me?.Id,
            SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
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
        if (!TryGetImporter(key, out var importer))
            return NotFound(new { error = $"Importer '{key}' not registered." });

        await using var stream = file.OpenReadStream();

        var me = HttpContext.GetCurrentUser();
        var options = new ImportOptions(
            DryRun: dryRun,
            Upsert: true,
            Limit: limit,
            UserId: me?.Id);

        var result = await importer.ImportFromFileAsync(stream, options, ct);
        return Ok(result);
    }
}
