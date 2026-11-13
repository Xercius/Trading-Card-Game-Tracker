using api.Common.Errors;
using api.Features.ImportExport.Dtos;
using api.Features.ImportExport.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Features.ImportExport;

/// <summary>Provides user collection, wishlist, and deck import/export endpoints.</summary>
[ApiController]
[Route("api")]
public sealed class ImportExportController(ImportExportService service) : ControllerBase
{
    /// <summary>Exports collection, wishlist, and decks as JSON.</summary>
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson(CancellationToken ct)
    {
        var (bytes, fileName) = await service.ExportJsonAsync(ct);
        return File(bytes, "application/json", fileName);
    }

    /// <summary>Exports collection as CSV.</summary>
    [HttpGet("export/collection.csv")]
    public async Task<IActionResult> ExportCollectionCsv(CancellationToken ct)
        => File(await service.ExportCollectionCsvAsync(ct), "text/csv", "collection.csv");

    /// <summary>Exports wishlist as CSV.</summary>
    [HttpGet("export/wishlist.csv")]
    public async Task<IActionResult> ExportWishlistCsv(CancellationToken ct)
        => File(await service.ExportWishlistCsvAsync(ct), "text/csv", "wishlist.csv");

    /// <summary>Exports decks as CSV.</summary>
    [HttpGet("export/decks.csv")]
    public async Task<IActionResult> ExportDecksCsv(CancellationToken ct)
        => File(await service.ExportDecksCsvAsync(ct), "text/csv", "decks.csv");

    /// <summary>Imports collection, wishlist, and decks from JSON in merge or replace mode.</summary>
    [HttpPost("import/json")]
    public async Task<ActionResult<ImportJsonResponse>> ImportJson(
        [FromQuery] string? mode,
        [FromBody] ImportPayload? payload,
        CancellationToken ct)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "merge" : mode.ToLowerInvariant();
        if (normalizedMode is not ("merge" or "replace"))
            return this.CreateValidationProblem("mode", "Mode must be merge or replace.");

        if (payload is null)
            return this.CreateValidationProblem("request", "A request body is required for import.");

        if (payload.Version != 1)
            return this.CreateValidationProblem("version", "Unsupported version.");

        var missing = await service.FindMissingPrintingIdsAsync(payload, ct);
        if (missing.Count > 0)
            return this.CreateValidationProblem("cardPrintingId", $"Unknown CardPrintingId(s): {string.Join(", ", missing)}");

        return Ok(await service.ImportJsonAsync(payload, normalizedMode == "replace", ct));
    }
}
