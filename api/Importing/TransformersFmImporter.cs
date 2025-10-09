using System.Collections.Generic;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class TransformersFmImporter : ISourceImporter
{
    public string Key => "tftcg";
    public string DisplayName => "FortressMaximus";
    public IEnumerable<string> SupportedGames => new[] { "Transformers TCG" };
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TransformersFmImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(TransformersFmImporter));
        _http.BaseAddress = new Uri("https://fortressmaximus.io/");
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode required (e.g., 'wave-1', 'wave-5', 'titan-masters').", nameof(options));

        using var resp = await _http.GetAsync($"api/cards?set={options.SetCode}", ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await ImportFromStreamAsync(stream, options, ct);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromStreamAsync(file, options, ct);

    private async Task<ImportSummary> ImportFromStreamAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        var items = await JsonSerializer.DeserializeAsync<List<FmCard>>(json, J, ct)
                    ?? throw new InvalidOperationException("Empty FortressMaximus payload.");

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

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var c in items)
            {
                if (processed++ >= limit) break;

                try
                {
                    await UpsertAsync(c, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error [{c.Set}/{c.Number}] {c.Name}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);
            var setName = options.SetCode ?? "(from file)";
            summary.Messages.Add($"Processed {Math.Min(processed, items.Count)} records for set={setName}.");
            return summary;
        });
    }

    private async Task UpsertAsync(FmCard src, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Transformers TCG";

        string set = (src.Set ?? "UNK").ToUpperInvariant();
        string number = src.Number ?? throw new InvalidOperationException("Missing number.");
        string name = src.Name?.Trim() ?? "Unknown";
        string type = src.Type ?? string.Empty;
        string? battleText = src.Text ?? src.RulesText;
        string? text = battleText ?? src.BotText ?? src.AltText;

        string rarity = src.Rarity ?? "Unknown";
        string style = "Standard";

        string? imageUrl = src.Images?.Bot
                        ?? src.Images?.Card
                        ?? src.Images?.Alt;

        var card = await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);

        var cardJson = JsonSerializer.Serialize(new
        {
            src.Subtitle,
            src.Faction,
            src.Type,
            subtypes = src.Subtypes,
            stars = src.Stars,
            attack = src.Attack,
            defense = src.Defense,
            health = src.Health,
            modes = new
            {
                bot = new { txt = src.BotText, img = src.Images?.Bot },
                alt = new { txt = src.AltText, img = src.Images?.Alt }
            },
            battle = new { txt = battleText, img = src.Images?.Card }
        }, J);

        if (card is null)
        {
            card = new Card
            {
                Game = game,
                Name = name,
                CardType = type,
                Description = text,
                DetailsJson = cardJson
            };
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
            images = src.Images,
            wave = src.Wave,
            collector = src.CollectorNumber
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
            if (imageUrl is not null && printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    private sealed record FmCard(
        string? Name,
        string? Subtitle,
        string? Type,
        string? Faction,
        string? Set,
        string? Number,
        string? CollectorNumber,
        string? Rarity,
        int? Stars,
        int? Attack,
        int? Defense,
        int? Health,
        string? Text,
        string? RulesText,
        string? BotText,
        string? AltText,
        string? Wave,
        List<string>? Subtypes,
        FmImages? Images
    );

    private sealed record FmImages(string? Bot, string? Alt, string? Card);
}
