using api.Filters;
using api.Importing;
using api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;
using System.Linq;
using System.Text.Json;

namespace api.Controllers;

[ApiController]
[Route("api/admin/upload")]
[AdminGuard]
public sealed class AdminUploadController : ControllerBase
{
    private readonly ImporterRegistry _registry;

    public AdminUploadController(ImporterRegistry registry) => _registry = registry;

    /// POST /api/admin/upload/csv?source=guardians&dryRun=true&limit=500
    [HttpPost("csv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportSummary>> UploadCsv(
        IFormFile file,
        [FromQuery] string source,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "File required." });
        if (!_registry.TryGet(source, out var importer)) return NotFound(new { error = $"Importer '{source}' not registered." });

        await using var s = file.OpenReadStream();

        // Schema check: header must include at least name,set,number
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        ms.Position = 0;
        using var parserStream = new MemoryStream(ms.ToArray(), writable: false);
        using var p = new TextFieldParser(parserStream) { TextFieldType = FieldType.Delimited };
        p.SetDelimiters(",");
        if (p.EndOfData) return BadRequest(new { error = "Empty CSV." });
        var header = p.ReadFields() ?? Array.Empty<string>();
        var cols = header.Select(h => h.Trim().ToLowerInvariant()).ToHashSet();
        string[] required = ["name", "set", "number"];
        var missing = required.Where(r => !cols.Contains(r)).ToArray();
        if (missing.Length > 0) return BadRequest(new { error = "CSV missing required columns.", missing });

        // rewind and import
        ms.Position = 0;
        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id);
        return Ok(await importer.ImportFromFileAsync(ms, options, ct));
    }

    /// POST /api/admin/upload/json?source=lorcanajson&dryRun=true&limit=500
    [HttpPost("json")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportSummary>> UploadJson(
        IFormFile file,
        [FromQuery] string source,
        [FromQuery] bool dryRun = true,
        [FromQuery] int? limit = null,
        [FromQuery] string? set = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "File required." });
        if (!_registry.TryGet(source, out var importer)) return NotFound(new { error = $"Importer '{source}' not registered." });

        await using var s = file.OpenReadStream();

        // Schema check: array of objects; each must have set/set_code + number/collector_number + name (sample first 100)
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return BadRequest(new { error = "Top-level JSON must be an array." });
        int idx = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (idx++ >= 100) break;
            if (el.ValueKind != JsonValueKind.Object)
                return BadRequest(new { error = $"Item {idx} is not an object." });
            bool has(string k) => el.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
            if (!(has("set") || has("set_code"))) return BadRequest(new { error = $"Item {idx} missing 'set' or 'set_code'." });
            if (!(has("number") || has("collector_number"))) return BadRequest(new { error = $"Item {idx} missing 'number' or 'collector_number'." });
            if (!has("name")) return BadRequest(new { error = $"Item {idx} missing 'name'." });
        }

        // rewind and import
        await using var s2 = file.OpenReadStream();
        var currentUser = HttpContext.GetCurrentUser();
        var options = new ImportOptions(DryRun: dryRun, Upsert: true, Limit: limit, UserId: currentUser?.Id, SetCode: set);
        return Ok(await importer.ImportFromFileAsync(s2, options, ct));
    }
}
