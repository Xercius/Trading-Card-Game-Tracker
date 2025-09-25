using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

public sealed class PokemonTcgImporter : ISourceImporter
{
    public string Key => "pokemon";
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public PokemonTcgImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(PokemonTcgImporter));
        _http.BaseAddress = new Uri("https://api.pokemontcg.io/v2/");
        _http.Timeout = TimeSpan.FromMinutes(5);
        // If you later add an API key:
        // _http.DefaultRequestHeaders.Add("X-Api-Key", "<key>");
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromStreamAsync(file, options, ct);

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required (e.g., 'base1', 'swsh12', 'sv3').", nameof(options));

        // q syntax: set.id:<code>. Use page pagination.
        var page = 1;
        const int pageSize = 250;
        var all = new List<PkmCard>();

        while (true)
        {
            var url = $"cards?q=set.id:{options.SetCode!.ToLowerInvariant()}&page={page}&pageSize={pageSize}";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var chunk = await resp.Content.ReadFromJsonAsync<Paged<PkmCard>>(J, ct) ?? new Paged<PkmCard>();
            if (chunk.Data is { Count: > 0 }) all.AddRange(chunk.Data);
            var got = chunk.Data?.Count ?? 0;
            if (got < pageSize) break;
            page++;
        }

        await using var ms = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(all, J));
        return await ImportFromStreamAsync(ms, options, ct);
    }

    private async Task<ImportSummary> ImportFromStreamAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        var cards = await JsonSerializer.DeserializeAsync<List<PkmCard>>(json, J, ct)
                    ?? throw new InvalidOperationException("Empty Pokémon TCG response.");

        var summary = new ImportSummary(Key, options.DryRun, 0, 0, 0, 0, 0);
        var limit = options.Limit ?? int.MaxValue;

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var c in cards)
            {
                if (processed++ >= limit) break;
                try { await UpsertAsync(c, summary, ct); }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error [{c.Set?.Id}/{c.Number}] {c.Name}: {ex.Message}");
                }
            }
            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, cards.Count)} records for set={options.SetCode ?? "(from file)"}.");
            return summary;
        });
    }

    private async Task UpsertAsync(PkmCard src, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Pokemon";

        string name = src.Name?.Trim() ?? "Unknown";
        string type = src.Supertype ?? ""; // Pokémon | Trainer | Energy
        string? text = BuildText(src);
        string set = (src.Set?.Id ?? "UNK").ToUpperInvariant();
        string number = src.Number ?? throw new InvalidOperationException("Missing card number.");
        string rarity = src.Rarity ?? "Unknown";
        string style = "Standard"; // keep simple; reverse/holo variants are separate printings in their DB

        string? imageUrl = src.Images?.Large ?? src.Images?.Small;

        // Upsert Card by (Game, Name)
        var card = await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);
        var cardJson = JsonSerializer.Serialize(new
        {
            src.Supertype,
            src.Subtypes,
            src.Types,
            src.Attacks,
            src.Abilities,
            src.Weaknesses,
            src.Resistances,
            src.RetreatCost,
            src.RegulationMark,
            src.Legalities
        }, J);

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
            images = src.Images,
            tcgplayer = src.Tcgplayer,   // pricing urls if present
            cardmarket = src.Cardmarket  // pricing urls if present
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

    private static string? BuildText(PkmCard c)
    {
        var sb = new StringBuilder();
        if (c.Abilities is { Count: > 0 })
            foreach (var a in c.Abilities) sb.AppendLine($"{a.Name}: {a.Text}");
        if (c.Attacks is { Count: > 0 })
            foreach (var atk in c.Attacks) sb.AppendLine($"{atk.Name}: {atk.Text}");
        return sb.Length == 0 ? null : sb.ToString().Trim();
    }

    // DTOs (subset of v2)
    private sealed record Paged<T>(List<T>? Data = null, int? Page = null, int? PageSize = null, int? Count = null, int? TotalCount = null);

    private sealed record PkmCard(
        string? Id,
        string? Name,
        string? Supertype,
        List<string>? Subtypes,
        List<string>? Types,
        List<Attack>? Attacks,
        List<Ability>? Abilities,
        List<WeakResist>? Weaknesses,
        List<WeakResist>? Resistances,
        List<string>? RetreatCost,
        string? RegulationMark,
        Legalities? Legalities,
        string? Number,
        string? Rarity,
        PkmSet? Set,
        ImgUris? Images,
        Tcgplayer? Tcgplayer,
        Cardmarket? Cardmarket
    );

    private sealed record Attack(string? Name, string? Text, List<string>? Cost, int? ConvertedEnergyCost, int? Damage);
    private sealed record Ability(string? Name, string? Text, string? Type);
    private sealed record WeakResist(string? Type, string? Value);
    private sealed record Legalities(string? Unlimited, string? Standard, string? Expanded);
    private sealed record PkmSet(string? Id, string? Name, string? Series);
    private sealed record ImgUris(string? Small, string? Large);
    private sealed record Tcgplayer(object? Prices, string? Url);
    private sealed record Cardmarket(object? Prices, string? Url);
}
