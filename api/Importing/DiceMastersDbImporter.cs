using AngleSharp;
using AngleSharp.Dom;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace api.Importing;

public sealed class DiceMastersDbImporter : ISourceImporter
{
    public string Key => "dicemasters";
    public string DisplayName => "DiceMastersDB";
    public IEnumerable<string> SupportedGames => new[] { GameName };

    private const string GameName = "Dice Masters";
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public DiceMastersDbImporter(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http.CreateClient(nameof(DiceMastersDbImporter));
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode (setSlug) required, e.g., 'avx', 'uxm', 'cw'.", nameof(options));

        var slug = options.SetCode!.Trim().ToLowerInvariant();
        var listUrl = $"https://dicemastersdb.com/set/{slug}/cards";

        var html = await _http.GetStringAsync(listUrl, ct);
        var ids = await ParseCardLinksAsync(html, listUrl, ct);

        var limit = options.Limit ?? int.MaxValue;
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

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var url in ids)
            {
                if (processed++ >= limit) break;

                try
                {
                    var cardHtml = await _http.GetStringAsync(url, ct);
                    var card = await ParseCardAsync(cardHtml, url, ct);
                    await UpsertAsync(card, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error importing '{url}': {ex.Message}");
                }

                await Task.Delay(150, ct);
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, ids.Count)} records for set={options.SetCode}.");
            return summary;
        });
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => ImportFromJsonAsync(file, options, ct);

    private static async Task<List<string>> ParseCardLinksAsync(string html, string baseUrl, CancellationToken ct)
    {
        var ctx = BrowsingContext.New(Configuration.Default);
        using var doc = await ctx.OpenAsync(req => req.Content(html).Address(baseUrl), ct);

        var links = doc.QuerySelectorAll("a")
            .Select(a => a.GetAttribute("href"))
            .Where(href => !string.IsNullOrWhiteSpace(href) && href!.Contains("/card/", StringComparison.OrdinalIgnoreCase))
            .Select(href => new Url(new Url(baseUrl), href!).Href)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return links;
    }

    private static async Task<DmCard> ParseCardAsync(string html, string url, CancellationToken ct)
    {
        var ctx = BrowsingContext.New(Configuration.Default);
        using var doc = await ctx.OpenAsync(req => req.Content(html).Address(url), ct);

        string set = (Text(doc, ".card-set", ".meta .set", "a[href*='/set/']") ??
                     TextByLabel(doc, "Set", "Set Name") ??
                     "UNK").Trim().ToUpperInvariant();

        string number = (Text(doc, ".card-number", ".meta .number", ".details .number") ??
                         TextByLabel(doc, "Card Number", "Card #", "Number") ??
                         string.Empty).Trim();

        string name = (Text(doc, "h1.card-title", ".card-header h1", ".title h1", "h1") ?? "Unknown").Trim();
        string? subtitle = Text(doc, ".card-subtitle", ".subtitle") ?? TextByLabel(doc, "Subtitle", "Version");
        string rarity = (Text(doc, ".card-rarity", ".rarity") ?? TextByLabel(doc, "Rarity") ?? "Unknown").Trim();
        string? energy = Text(doc, ".card-energy", ".energy") ?? TextByLabel(doc, "Energy", "Energy Type");
        string? cost = Text(doc, ".card-cost", ".purchase-cost") ?? TextByLabel(doc, "Purchase Cost", "Cost");
        string? cardType = Text(doc, ".type", ".card-type") ?? TextByLabel(doc, "Type");
        string text = Text(doc, ".rules-text", ".card-text", ".abilities", ".game-text") ??
                      TextByLabel(doc, "Card Text", "Abilities", "Ability") ?? string.Empty;

        var imageSrc = doc.QuerySelector("img.card-image, .card img, .main-image img")?.GetAttribute("src") ??
                       doc.QuerySelector("a.card-image[href]")?.GetAttribute("href") ?? string.Empty;
        string imageUrl = MakeAbsolute(url, imageSrc);

        var diceFaces = doc.QuerySelectorAll(".dice-face img, .die img, img[src*='dice'], img[src*='/die']")
            .Select(img => img.GetAttribute("src"))
            .Where(src => !string.IsNullOrWhiteSpace(src))
            .Select(src => MakeAbsolute(url, src!))
            .Distinct()
            .ToList();

        return new DmCard
        {
            Set = set,
            Number = number,
            Name = name,
            Rarity = rarity,
            CardType = cardType,
            Subtitle = subtitle,
            Energy = energy,
            PurchaseCost = cost,
            DiceFaces = diceFaces,
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl
        };
    }

    private async Task<ImportSummary> ImportFromJsonAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(json, cancellationToken: ct);

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

        static IEnumerable<JsonElement> EnumerateCards(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    yield return item;
                yield break;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            if (TryGetProperty(root, "cards", out var cards) && cards.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cards.EnumerateArray())
                    yield return item;
                yield break;
            }

            if (TryGetProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                    yield return item;
                yield break;
            }

            yield return root;
        }

        var cards = EnumerateCards(doc.RootElement).ToList();

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;

            foreach (var element in cards)
            {
                if (processed++ >= limit) break;

                try
                {
                    var card = ParseCard(element);
                    await UpsertAsync(card, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error importing card entry: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, cards.Count)} records from file.");
            return summary;
        });
    }

    private static DmCard ParseCard(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Expected card object.");

        string set = (GetString(element, "set", "setCode", "set_code", "setSlug") ?? "UNK").Trim().ToUpperInvariant();
        string number = (GetString(element, "number", "cardNumber", "collectorNumber", "card_number") ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
            throw new InvalidOperationException("Missing set or number in card entry.");

        string name = (GetString(element, "name", "title", "cardName", "card_name") ?? "Unknown").Trim();
        string rarity = (GetString(element, "rarity") ?? "Unknown").Trim();

        var diceFaces = GetStringArray(element, "diceFaces", "dice_faces", "dice") ?? new List<string>();

        return new DmCard
        {
            Set = set,
            Number = number,
            Name = name,
            Rarity = rarity,
            CardType = GetString(element, "cardType", "type", "category"),
            Subtitle = GetString(element, "subtitle", "subTitle", "version"),
            Energy = GetString(element, "energy", "energyType", "energy_type"),
            PurchaseCost = GetString(element, "purchaseCost", "cost", "purchase_cost", "purchase"),
            DiceFaces = diceFaces,
            Text = GetString(element, "text", "cardText", "rulesText", "gameText", "ability"),
            ImageUrl = GetString(element, "imageUrl", "image_url", "image", "imageUri", "image_uri")
        };
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (TryGetProperty(element, name, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => value.ValueKind == JsonValueKind.Null ? null : value.ToString()
                };
            }
        }

        return null;
    }

    private static List<string>? GetStringArray(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        list.Add(item.GetString()!);
                    else if (item.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                        list.Add(item.ToString());
                }
                return list;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return new List<string>();

                var list = raw
                    .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0)
                    .ToList();
                return list;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string MakeAbsolute(string baseUrl, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var src = value!.Trim();
        if (Uri.TryCreate(src, UriKind.Absolute, out var abs)) return abs.ToString();
        return new Url(new Url(baseUrl), src).Href;
    }

    private static string? Text(IDocument doc, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector)) continue;
            var element = doc.QuerySelector(selector);
            var value = element?.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private static string? TextByLabel(IDocument doc, params string[] labels)
    {
        if (labels.Length == 0) return null;

        var labelSet = labels
            .Select(l => l.Trim().TrimEnd(':'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (labelSet.Count == 0) return null;

        foreach (var element in doc.QuerySelectorAll("dt, th, .label, .field-label, strong"))
        {
            var label = element.TextContent?.Trim().TrimEnd(':');
            if (string.IsNullOrWhiteSpace(label) || !labelSet.Contains(label)) continue;

            var value = element switch
            {
                { NodeName: "DT" } => element.NextElementSibling,
                { NodeName: "TH" } => element.NextElementSibling,
                _ => null
            };

            value ??= element.ParentElement?.QuerySelector(".value, .field-value, span.value, div.value");
            value ??= element.ParentElement?.NextElementSibling;

            var text = value?.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        foreach (var node in doc.QuerySelectorAll("li, p"))
        {
            var raw = node.TextContent;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parts = raw.Split(':', 2);
            if (parts.Length != 2) continue;

            if (labelSet.Contains(parts[0].Trim()))
            {
                var candidate = parts[1].Trim();
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
            }
        }

        return null;
    }

    private async Task UpsertAsync(DmCard src, ImportSummary summary, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(src.Set) || string.IsNullOrWhiteSpace(src.Number))
            throw new InvalidOperationException("Set and number are required.");

        string name = src.Name;
        string cardType = src.CardType ?? string.Empty;
        string? desc = src.Text;
        string set = src.Set;
        string number = src.Number;
        string rarity = src.Rarity;
        string style = "Standard";
        string? imageUrl = src.ImageUrl;

        var card = await _db.Cards.FirstOrDefaultAsync(x => x.Game == GameName && x.Name == name, ct);
        var cardJson = JsonSerializer.Serialize(new
        {
            subtitle = src.Subtitle,
            energy = src.Energy,
            purchaseCost = src.PurchaseCost,
            diceFaces = src.DiceFaces
        }, J);

        if (card is null)
        {
            card = new Card { Game = GameName, Name = name, CardType = cardType, Description = desc, DetailsJson = cardJson };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            bool changed = false;
            if (card.CardType != cardType) { card.CardType = cardType; changed = true; }
            if (card.Description != desc) { card.Description = desc; changed = true; }
            if (card.DetailsJson != cardJson) { card.DetailsJson = cardJson; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == GameName), p => p.CardId, cc => cc.CardId, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        var printingJson = JsonSerializer.Serialize(new { set, number, rarity, style, imageUrl }, J);

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

    private sealed class DmCard
    {
        public string Set { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Rarity { get; set; } = "Unknown";
        public string? CardType { get; set; }
        public string? Subtitle { get; set; }
        public string? Energy { get; set; }
        public string? PurchaseCost { get; set; }
        public List<string> DiceFaces { get; set; } = new();
        public string? Text { get; set; }
        public string? ImageUrl { get; set; }
    }
}
