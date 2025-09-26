using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class ScryfallImporter : ISourceImporter
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;

    public ScryfallImporter(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _http = httpFactory.CreateClient(nameof(ScryfallImporter));
        _http.BaseAddress = new Uri("https://api.scryfall.com/");
    }

    public string Key => "scryfall";

    public Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required for Scryfall imports.", nameof(options));

        return ImportSetAsync(options.SetCode!, options, ct);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => throw new NotSupportedException("Scryfall file import not implemented. Use ImportFromRemoteAsync with set code.");

    private async Task<ImportSummary> ImportSetAsync(string setCode, ImportOptions options, CancellationToken ct)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun,
            CardsCreated = 0,
            CardsUpdated = 0,
            PrintingsCreated = 0,
            PrintingsUpdated = 0,
            Errors = 0,
            Messages = { "Dummy importer ran" }
        };
        var limit = options.Limit ?? int.MaxValue;

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            var url = $"cards/search?q=set:{Uri.EscapeDataString(setCode)}+unique:prints&order=set";
            int processed = 0;

            while (!string.IsNullOrEmpty(url) && processed < limit)
            {
                var page = await GetPageAsync(url, ct);
                foreach (var c in page.Data)
                {
                    if (processed >= limit) break;
                    try
                    {
                        await UpsertCardAndPrintingAsync(c, setCode, summary, ct);
                    }
                    catch (Exception ex)
                    {
                        summary.Errors++;
                        summary.Messages.Add($"Error [{c.Id}] {c.Name}: {ex.Message}");
                    }
                    processed++;
                }
                url = page.HasMore ? page.NextPage : null;
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {processed} records for set={setCode}.");
            return summary;
        });
    }

    private async Task UpsertCardAndPrintingAsync(ScryCard c, string setCode, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Magic";

        // Name, type, rules text
        string name = c.Name ?? "Unknown";
        string cardType = c.TypeLine ?? "";
        string? desc = BuildRulesText(c);

        // Find or create Card by (Game, Name)
        var card = _db.Cards.Local.FirstOrDefault(x => x.Game == game && x.Name == name)
            ?? await _db.Cards.Where(x => x.Game == game && x.Name == name).FirstOrDefaultAsync(ct);
        if (card is null)
        {
            card = new Card { Game = game, Name = name, CardType = cardType, Description = desc };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            // Update fields if changed
            bool changed = false;
            if (card.CardType != cardType) { card.CardType = cardType; changed = true; }
            if (card.Description != desc) { card.Description = desc; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        // Idempotent by (Game, Set, Number)
        string set = c.Set?.ToUpperInvariant() ?? setCode.ToUpperInvariant();
        string number = c.CollectorNumber ?? "";
        if (string.IsNullOrEmpty(number))
            throw new InvalidOperationException("Missing collector_number.");

        // Prefer nonfoil image, else any face image
        string? imageUrl = c.ImageUris?.Normal
                           ?? c.CardFaces?.FirstOrDefault()?.ImageUris?.Normal;

        string rarity = c.Rarity ?? "common";
        string style = (c.Finishes?.Contains("foil") == true) ? "Foil" : "Standard";

        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, cc => cc.Id, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        if (printing is null)
        {
            printing = new CardPrinting
            {
                Card = card,
                Set = set,
                Number = number,
                Rarity = rarity,
                Style = style,
                ImageUrl = imageUrl
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
            if (printing.ImageUrl != imageUrl && imageUrl is not null) { printing.ImageUrl = imageUrl; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    private static string? BuildRulesText(ScryCard c)
    {
        if (c.OracleText is not null) return c.OracleText;
        if (c.CardFaces is { Count: > 0 })
        {
            var parts = c.CardFaces
                .Select(f => new[] { f.Name, f.OracleText }.Where(s => !string.IsNullOrWhiteSpace(s)))
                .Select(lines => string.Join("\n", lines));
            return string.Join("\n//\n", parts);
        }
        return null;
    }

    // --- Scryfall DTOs + fetch ---
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private async Task<ScryPage> GetPageAsync(string relativeOrAbsolute, CancellationToken ct)
    {
        var uri = relativeOrAbsolute.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(relativeOrAbsolute)
            : new Uri(_http.BaseAddress!, relativeOrAbsolute);

        using var resp = await _http.GetAsync(uri, ct);
        resp.EnsureSuccessStatusCode();
        var page = await resp.Content.ReadFromJsonAsync<ScryPage>(_json, ct);
        return page ?? throw new InvalidOperationException("Empty response from Scryfall.");
    }

    private sealed record ScryPage(
        List<ScryCard> Data,
        bool HasMore,
        string? NextPage
    );

    private sealed record ScryCard(
        string Id,
        string? Name,
        string? TypeLine,
        string? OracleText,
        string? Rarity,
        string? CollectorNumber,
        string? Set,
        List<string>? Finishes,
        ScryImages? ImageUris,
        List<ScryFace>? CardFaces
    );

    private sealed record ScryFace(
        string? Name,
        string? OracleText,
        ScryImages? ImageUris
    );

    private sealed record ScryImages(string? Small, string? Normal, string? Large);
}
