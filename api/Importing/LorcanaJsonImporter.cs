using System.Collections.Generic;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class LorcanaJsonImporter : ISourceImporter
{
    public string Key => "lorcanajson";
    public string DisplayName => "Lorcana JSON";
    public IEnumerable<string> SupportedGames => new[] { "Disney Lorcana" };

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public LorcanaJsonImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(LorcanaJsonImporter));
        _http.Timeout = TimeSpan.FromMinutes(5);
        // Raw cards dump (single file). We filter by set code locally.
        _http.BaseAddress = new Uri("https://raw.githubusercontent.com/LorcanaJSON/LorcanaJSON/main/");
    }

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required (e.g., 'TFC', 'ROTF', 'ITI').", nameof(options));

        using var resp = await _http.GetAsync("cards.json", ct);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        return await ImportFromStreamAsync(s, options, ct);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromStreamAsync(file, options, ct);

    private async Task<ImportSummary> ImportFromStreamAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun,
            CardsCreated = 0,
            CardsUpdated = 0,
            PrintingsCreated = 0,
            PrintingsUpdated = 0,
            Errors = 0
        };
        var limit = options.Limit ?? int.MaxValue;
        var setCode = options.SetCode?.Trim();

        using var doc = await JsonDocument.ParseAsync(json, cancellationToken: ct);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Expected an array of cards.");

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;

            foreach (var cardEl in root.EnumerateArray())
            {
                if (processed >= limit) break;

                // Filter by set
                var cardSet = GetString(cardEl, "set_code") ?? GetString(cardEl, "set") ?? "";
                if (!string.IsNullOrEmpty(setCode) && !string.Equals(cardSet, setCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    await UpsertAsync(cardEl, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    var name = GetString(cardEl, "name") ?? "Unknown";
                    summary.Messages.Add($"Error [{name}] {ex.Message}");
                }

                processed++;
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {processed} records for set={setCode ?? "(all)"}.");
            return summary;
        });
    }

    private async Task UpsertAsync(JsonElement c, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Disney Lorcana";

        // Core fields (tolerant to schema variants)
        string name = GetString(c, "name") ?? "Unknown";
        string type = GetString(c, "type") ?? GetString(c, "card_type") ?? "";
        string? text = GetString(c, "rules_text") ?? GetString(c, "text");
        string set = (GetString(c, "set_code") ?? GetString(c, "set") ?? "UNK").ToUpperInvariant();
        string number = GetString(c, "number") ?? GetString(c, "collector_number") ?? throw new InvalidOperationException("Missing number");
        string rarity = GetString(c, "rarity") ?? "Unknown";

        // Style and image
        string style =
            GetBool(c, "foil") ? "Foil" :
            (GetString(c, "finish")?.Contains("foil", StringComparison.OrdinalIgnoreCase) == true ? "Foil" : "Standard");

        string? imageUrl =
            GetString(c, "image") ??
            GetNestedString(c, "images", "en", "full") ??
            GetNestedString(c, "image_urls", "en") ??
            GetNestedString(c, "image_uris", "normal");

        // Upsert Card by (Game, Name)
        var card = await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);
        var detailsCardJson = JsonSerializer.Serialize(c, J);

        if (card is null)
        {
            card = new Card { Game = game, Name = name, CardType = type, Description = text, DetailsJson = detailsCardJson };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            bool changed = false;
            if (card.CardType != type) { card.CardType = type; changed = true; }
            if (card.Description != text) { card.Description = text; changed = true; }
            if (card.DetailsJson != detailsCardJson) { card.DetailsJson = detailsCardJson; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        // Upsert Printing by (Game, Set, Number)
        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, cc => cc.CardId, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        var printingJson = JsonSerializer.Serialize(new
        {
            set,
            number,
            rarity,
            style,
            images = new { url = imageUrl }
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
            if (printing.CardId != card.CardId) { printing.CardId = card.CardId; changed = true; }
            if (printing.Rarity != rarity) { printing.Rarity = rarity; changed = true; }
            if (printing.Style != style) { printing.Style = style; changed = true; }
            if (imageUrl is not null && printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    // --- helpers ---
    private static string? GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True
           || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b);

    private static string? GetNestedString(JsonElement el, params string[] props)
    {
        var cur = el;
        for (int i = 0; i < props.Length; i++)
        {
            if (!cur.TryGetProperty(props[i], out var next)) return null;
            if (i == props.Length - 1) return next.ValueKind == JsonValueKind.String ? next.GetString() : null;
            cur = next;
        }
        return null;
    }
}
