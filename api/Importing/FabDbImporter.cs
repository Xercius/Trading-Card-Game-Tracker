using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class FabDbImporter : ISourceImporter
{
    public string Key => "fabdb";

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public FabDbImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(FabDbImporter));
        _http.BaseAddress = new Uri("https://api.fabdb.net/");
        _http.Timeout = TimeSpan.FromMinutes(5);

        var key = Environment.GetEnvironmentVariable("FABDB_API_KEY");
        if (!string.IsNullOrWhiteSpace(key))
            _http.DefaultRequestHeaders.Add("X-Api-Key", key);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromStreamAsync(file, options, ct);

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required (e.g., 'WTR', 'DYN', 'MST').", nameof(options));

        // Typical pattern: /cards?set=WTR&page=1
        int page = 1;
        var all = new List<JsonElement>();

        while (true)
        {
            using var resp = await _http.GetAsync($"cards?set={options.SetCode}&page={page}", ct);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var data = doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(e => e.Clone()).ToList()
                : new();
            if (data.Count == 0) break;
            all.AddRange(data);
            if (!doc.RootElement.TryGetProperty("links", out var links) || !links.TryGetProperty("next", out var next) || next.ValueKind == JsonValueKind.Null) break;
            page++;
        }

        await using var ms = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(all, J));
        return await ImportFromStreamAsync(ms, options, ct);
    }

    private async Task<ImportSummary> ImportFromStreamAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(json, cancellationToken: ct);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new();

        var summary = new ImportSummary(Key, options.DryRun, 0, 0, 0, 0, 0);
        var limit = options.Limit ?? int.MaxValue;

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var c in items)
            {
                if (processed++ >= limit) break;
                try { await UpsertAsync(c, summary, ct); }
                catch (Exception ex)
                {
                    var n = Get(c, "name");
                    var set = Get(c, "set");
                    var num = Get(c, "number");
                    summary.Errors++;
                    summary.Messages.Add($"Error [{set}/{num}] {n}: {ex.Message}");
                }
            }
            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, items.Count)} records for set={options.SetCode ?? "(from file)"}.");
            return summary;
        });
    }

    private async Task UpsertAsync(JsonElement src, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Flesh and Blood";

        // Core
        string name = Get(src, "name") ?? "Unknown";
        string type = Get(src, "type") ?? "";                  // Attack / Defense Reaction / Action / Hero / etc.
        string? text = Get(src, "rules") ?? Get(src, "text");  // gametext
        string set = (Get(src, "set") ?? "UNK").ToUpperInvariant();
        string number = Get(src, "number") ?? throw new InvalidOperationException("Missing card number.");
        string rarity = Get(src, "rarity") ?? "Unknown";
        string style = (Get(src, "finish")?.Contains("foil", StringComparison.OrdinalIgnoreCase) == true) ? "Foil" : "Standard";

        // Images
        string? imageUrl =
            GetNested(src, "images", "full") ??
            GetNested(src, "images", "normal") ??
            Get(src, "image");

        // Card JSON payload (class/talent/cost/pitch/power/defense, legality, etc.)
        var cardJson = JsonSerializer.Serialize(new
        {
            @class = Get(src, "class"),
            talent = Get(src, "talent"),
            subtype = Get(src, "subtype"),
            type,
            cost = GetInt(src, "cost"),
            pitch = GetInt(src, "pitch"),
            power = GetInt(src, "power"),
            defense = GetInt(src, "defense"),
            legality = GetObj(src, "legality"),
            variants = GetArr(src, "variants"),
            traits = GetArr(src, "keywords") ?? GetArr(src, "traits"),
            text
        }, J);

        // Upsert Card by (Game, Name)
        var card = _db.ChangeTracker.Entries<Card>()
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity)
            .FirstOrDefault(x => x.Game == game && x.Name == name)
            ?? await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);
        if (card is null)
        {
            card = new Card { Game = game, Name = name, CardType = type, Description = text, DetailsJson = cardJson };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            bool changed = false;
            if (card.CardType != type) { card.CardType = type; changed = true; }
            if (card.Description != text) { card.Description = text; changed = true; }
            if (card.DetailsJson != cardJson) { card.DetailsJson = cardJson; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        // Upsert Printing by (Game, Set, Number)
        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, cc => cc.Id, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        var printingJson = JsonSerializer.Serialize(new
        {
            set,
            number,
            rarity,
            style,
            images = GetObj(src, "images"),
            release = Get(src, "release")
        }, J);

        if (printing is null)
        {
            printing = new CardPrinting
            {
                Card = card,
                Set = set,
                Number = number,
                Rarity = rarity,
                Style = style,
                ImageUrl = imageUrl,
                DetailsJson = printingJson
            };
            _db.CardPrintings.Add(printing);
            summary.PrintingsCreated++;
        }
        else
        {
            bool changed = false;
            if (printing.CardId != card.Id) { printing.CardId = card.Id; changed = true; }
            if (printing.Rarity != rarity) { printing.Rarity = rarity; changed = true; }
            if (printing.Style != style) { printing.Style = style; changed = true; }
            if (imageUrl is not null && printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    // ---- JSON helpers ----
    private static string? Get(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? GetInt(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.Number && v.TryGetInt32(out var i) ? i : (int?)null;
    private static string? GetNested(JsonElement el, params string[] path)
    {
        var cur = el;
        for (int i = 0; i < path.Length; i++)
        {
            if (!cur.TryGetProperty(path[i], out var next)) return null;
            if (i == path.Length - 1) return next.ValueKind == JsonValueKind.String ? next.GetString() : null;
            cur = next;
        }
        return null;
    }
    private static object? GetObj(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? JsonElementToObject(v) : null;
    private static List<object>? GetArr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
           ? v.EnumerateArray().Select(JsonElementToObject!).ToList()
           : null;
    private static object? JsonElementToObject(JsonElement v)
        => v.ValueKind switch
        {
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(v.GetRawText(), J),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object>>(v.GetRawText(), J),
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.TryGetInt64(out var i) ? i : v.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
