using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class SwccgdbImporter : ISourceImporter
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public SwccgdbImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(SwccgdbImporter));
        _http.BaseAddress = new Uri("https://swccgdb.com/");
    }

    public string Key => "swccgdb";

    public Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required. Example: Premiere, Hoth, Endor.", nameof(options));
        return ImportSetAsync(options.SetCode!, options, ct);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream _, ImportOptions __, CancellationToken ___ = default)
        => throw new NotSupportedException("Use ImportFromRemoteAsync with set code.");

    private async Task<ImportSummary> ImportSetAsync(string setCode, ImportOptions options, CancellationToken ct)
    {
        var summary = new ImportSummary(Key, options.DryRun, 0, 0, 0, 0, 0);
        var url = $"api/public/cards/{Uri.EscapeDataString(setCode)}.json"; // returns array of cards
        var cards = await _http.GetFromJsonAsync<List<SwccgCard>>(url, Json, ct)
                    ?? throw new InvalidOperationException("Empty response from SWCCGDB.");

        var limit = options.Limit ?? int.MaxValue;

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var c in cards)
            {
                if (processed++ >= limit) break;
                try
                {
                    await UpsertAsync(c, setCode, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error [{c.Id}] {c.Front?.Title}: {ex.Message}");
                }
            }
            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, cards.Count)} records for set={setCode}.");
            return summary;
        });
    }

    private async Task UpsertAsync(SwccgCard src, string setCode, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Star Wars CCG";

        string name = src.Front?.Title?.Trim() ?? "Unknown";
        string cardType = src.Front?.Type ?? "";
        string? desc = src.Front?.Gametext;

        // Card by (Game, Name)
        var card = await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);
        if (card is null)
        {
            card = new Card { Game = game, Name = name, CardType = cardType, Description = desc, DetailsJson = JsonSerializer.Serialize(src, Json) };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            bool changed = false;
            if (card.CardType != cardType) { card.CardType = cardType; changed = true; }
            if (card.Description != desc) { card.Description = desc; changed = true; }
            var newJson = JsonSerializer.Serialize(src, Json);
            if (card.DetailsJson != newJson) { card.DetailsJson = newJson; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        // Printing by (Game, Set, Number)
        string set = setCode; // keep textual set code used in the request
        // Use gempId format like "1_168" → number "168" if present, else fall back to src.Id
        string number = ParseNumber(src.GempId) ?? src.Id.ToString();

        string rarity = src.Rarity ?? "Unknown";
        string style = "Standard";
        string? imageUrl = src.Front?.ImageUrl;

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
                ImageUrl = imageUrl,
                DetailsJson = JsonSerializer.Serialize(new { src.Printings, src.Side, src.Set }, Json)
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
            var pJson = JsonSerializer.Serialize(new { src.Printings, src.Side, src.Set }, Json);
            if (printing.DetailsJson != pJson) { printing.DetailsJson = pJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    private static string? ParseNumber(string? gempId)
    {
        if (string.IsNullOrWhiteSpace(gempId)) return null;
        var i = gempId.IndexOf('_');
        return i >= 0 && i < gempId.Length - 1 ? gempId[(i + 1)..] : gempId;
    }

    // DTOs reflect SWCCGPC JSON fields commonly mirrored by SWCCGDB
    private sealed record SwccgCard(
        int Id,
        string? GempId,
        string? Rarity,
        string? Set,
        string? Side,
        SwccgFace? Front,
        List<SwccgPrinting>? Printings
    );

    private sealed record SwccgFace(
        string? Title,
        string? Type,
        string? SubType,
        string? Gametext,
        string? Lore,
        string? ImageUrl
    );

    private sealed record SwccgPrinting(string? Set);
}
