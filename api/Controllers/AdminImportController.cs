using api.Filters;
using api.Importing;
using api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace api.Controllers;

[ApiController]
[Route("api/admin/import")]
[AdminGuard]
public sealed record ImportSourceResponse(string Key, string Name, IReadOnlyList<string> Games);

public sealed class AdminImportController : ControllerBase
{
    private readonly ImporterRegistry _registry;

    public AdminImportController(ImporterRegistry registry) => _registry = registry;

    [HttpGet("sources")]
    public ActionResult<IReadOnlyList<ImportSourceResponse>> GetSources()
    {
        var sources = _registry.All
            .Select(importer => new ImportSourceResponse(
                importer.Key,
                importer.DisplayName,
                importer.SupportedGames.ToArray()))
            .OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(sources);
    }

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

    /// POST /api/admin/import/dicemasters?set=avx&dryRun=true&limit=200
    [HttpPost("dicemasters")]
    public async Task<ActionResult<ImportSummary>> ImportDiceMasters(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("dicemasters", out var importer))
            return NotFound(new { error = "Dice Masters importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/dicemasters/file?dryRun=true
    [HttpPost("dicemasters/file")]
    public async Task<ActionResult<ImportSummary>> ImportDiceMastersFromFile(
        IFormFile file,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("dicemasters", out var importer))
            return NotFound(new { error = "Dice Masters importer not registered." });

        await using var stream = file.OpenReadStream();
        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id);

        var result = await importer.ImportFromFileAsync(stream, options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/tftcg?set=wave-1&dryRun=true&limit=500
    [HttpPost("tftcg")]
    public async Task<ActionResult<ImportSummary>> ImportTransformers(
        [FromQuery] string set,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("tftcg", out var importer))
            return NotFound(new { error = "Transformers importer not registered." });

        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);

        var result = await importer.ImportFromRemoteAsync(options, ct);
        return Ok(result);
    }

    /// POST /api/admin/import/tftcg/file?dryRun=true
    [HttpPost("tftcg/file")]
    public async Task<ActionResult<ImportSummary>> ImportTransformersFromFile(
        IFormFile file,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("tftcg", out var importer))
            return NotFound(new { error = "Transformers importer not registered." });

        await using var s = file.OpenReadStream();
        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id);

        var result = await importer.ImportFromFileAsync(s, options, ct);
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

    /// POST /api/admin/import/guardians/file?dryRun=true&limit=500
    [HttpPost("guardians/file")]
    public async Task<ActionResult<ImportSummary>> ImportGuardiansFromFile(
        IFormFile file,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!_registry.TryGet("guardians", out var importer))
            return NotFound(new { error = "Guardians importer not registered." });

        await using var stream = file.OpenReadStream();
        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id);

        var result = await importer.ImportFromFileAsync(stream, options, ct);
        return Ok(result);
    }
}
