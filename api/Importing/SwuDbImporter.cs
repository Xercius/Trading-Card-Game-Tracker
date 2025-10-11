using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace api.Importing;

public sealed class SwuDbImporter : ISourceImporter
{
    public string Key => "swu";
    public string DisplayName => "SWU DB";
    public IEnumerable<string> SupportedGames => new[] { "Star Wars Unlimited" };
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SwuDbImporter(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _http = httpFactory.CreateClient(nameof(SwuDbImporter));
        _http.BaseAddress = new Uri("https://www.swu-db.com/");
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromStreamAsync(file, options, ct);

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required (e.g., 'sor', 'sotg').", nameof(options));

        using var response = await _http.GetAsync($"api/cards/{options.SetCode!.ToLowerInvariant()}", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await ImportFromStreamAsync(stream, options, ct);
    }

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

        var cards = await JsonSerializer.DeserializeAsync<List<SwuCard>>(json, JsonOptions, ct)
                    ?? throw new InvalidOperationException("Empty SWUDB response.");

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var card in cards)
            {
                if (processed++ >= limit) break;
                try
                {
                    await UpsertAsync(card, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error [{card.Set}/{card.Number}] {card.Name}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, cards.Count)} records for set={options.SetCode ?? "(from file)"}.");
            return summary;
        });
    }

    private async Task UpsertAsync(SwuCard source, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Star Wars Unlimited";

        string name = source.Name?.Trim() ?? "Unknown";
        string type = source.Type ?? string.Empty;
        string? text = source.Text ?? source.RulesText;
        string set = (source.Set ?? "UNK").ToUpperInvariant();
        string number = source.Number ?? throw new InvalidOperationException("Missing card number.");
        string rarity = source.Rarity ?? "Unknown";
        string style = source.Variant?.Contains("foil", StringComparison.OrdinalIgnoreCase) == true ? "Foil" : "Standard";

        string? imageUrl = source.Image
                         ?? source.ImageUrl
                         ?? source.FrontImage
                         ?? (source.Leader == true ? source.ImageFront : null);

        var card = _db.Cards.Local.FirstOrDefault(x => x.Game == game && x.Name == name)
                   ?? await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);
        var cardJson = JsonSerializer.Serialize(new
        {
            source.Subtitle,
            source.Type,
            source.Traits,
            source.Keywords,
            source.Aspects,
            source.Arena,
            source.Power,
            source.Health,
            source.Cost,
            text = source.Text ?? source.RulesText,
            leader = source.Leader,
            artist = source.Artist
        }, JsonOptions);

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
            if (card.CardType != type)
            {
                card.CardType = type;
                changed = true;
            }
            if (card.Description != text)
            {
                card.Description = text;
                changed = true;
            }
            if (card.DetailsJson != cardJson)
            {
                card.DetailsJson = cardJson;
                changed = true;
            }
            if (changed)
            {
                summary.CardsUpdated++;
            }
        }

        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, c => c.CardId, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        var printingJson = JsonSerializer.Serialize(new
        {
            set,
            number,
            rarity,
            style,
            source.Variant,
            source.Release,
            aspects = source.Aspects,
            cost = source.Cost
        }, JsonOptions);

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
            if (printing.CardId != card.CardId)
            {
                printing.CardId = card.CardId;
                changed = true;
            }
            if (printing.Rarity != rarity)
            {
                printing.Rarity = rarity;
                changed = true;
            }
            if (printing.Style != style)
            {
                printing.Style = style;
                changed = true;
            }
            if (imageUrl is not null && printing.ImageUrl != imageUrl)
            {
                printing.ImageUrl = imageUrl;
                changed = true;
            }
            if (printing.DetailsJson != printingJson)
            {
                printing.DetailsJson = printingJson;
                changed = true;
            }
            if (changed)
            {
                summary.PrintingsUpdated++;
            }
        }
    }

    private sealed record SwuCard(
        string? Name,
        string? Subtitle,
        string? Type,
        string? Set,
        string? Number,
        string? Rarity,
        string[]? Aspects,
        int? Cost,
        int? Power,
        int? Health,
        string[]? Traits,
        string[]? Keywords,
        string? Arena,
        string? Text,
        string? RulesText,
        string? Artist,
        string? Variant,
        string? Release,
        bool? Leader,
        string? Image,
        string? ImageUrl,
        string? FrontImage,
        string? ImageFront
    );
}
