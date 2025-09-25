using api.Filters;
using api.Importing;
using api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/admin/import")]
[AdminGuard]
public sealed class AdminImportController : ControllerBase
{
    private readonly ImporterRegistry _registry;

    public AdminImportController(ImporterRegistry registry) => _registry = registry;

    /// POST /api/admin/import/scryfall?set=KHM&dryRun=true&limit=200
    [HttpPost("scryfall")]
    public async Task<ActionResult<ImportSummary>> ImportScryfall(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("scryfall", out var importer))
            return NotFound(new { error = "Scryfall importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }
}
