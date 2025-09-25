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

    /// POST /api/admin/import/fab?set=WTR&dryRun=true&limit=500
    [HttpPost("fab")]
    public async Task<ActionResult<ImportSummary>> ImportFab(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("fabdb", out var importer))
            return NotFound(new { error = "FabDB importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/swu?set=sor&dryRun=true&limit=500
    [HttpPost("swu")]
    public async Task<ActionResult<ImportSummary>> ImportSwu(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("swu", out var importer))
            return NotFound(new { error = "SWU importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/pokemon?set=sv3&dryRun=true&limit=500
    [HttpPost("pokemon")]
    public async Task<ActionResult<ImportSummary>> ImportPokemon(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("pokemon", out var importer))
            return NotFound(new { error = "Pokémon importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/swccgdb?set=Premiere&dryRun=true&limit=500
    [HttpPost("swccgdb")]
    public async Task<ActionResult<ImportSummary>> ImportSwccgdb(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("swccgdb", out var importer))
            return NotFound(new { error = "SWCCGDB importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/lorcana?source=lorcanajson&set=TFC&dryRun=true&limit=500
    [HttpPost("lorcana")]
    public async Task<ActionResult<ImportSummary>> ImportLorcana(
        [FromQuery] string source = "lorcanajson",
        [FromQuery] string? set = null,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet(source, out var importer))
            return NotFound(new { error = $"Importer '{source}' not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }
}
