using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json;

namespace api.Importing;

public sealed class GuardiansLocalImporter : ISourceImporter
{
    public string Key => "guardians";
    public string DisplayName => "Guardians Local";
    public IEnumerable<string> SupportedGames => new[] { "Guardians CCG" };
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GuardiansLocalImporter(AppDbContext db) => _db = db;

    public Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
        => throw new NotSupportedException("Use file upload or local path for Guardians.");

    public async Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        int first = ms.ReadByte();
        ms.Position = 0;
        return first == (int)'[' || first == (int)'{'
            ? await ImportJsonAsync(ms, options, ct)
            : await ImportCsvAsync(ms, options, ct);
    }

    private async Task<ImportSummary> ImportJsonAsync(Stream json, ImportOptions options, CancellationToken ct)
    {
        var items = await JsonSerializer.DeserializeAsync<List<Raw>>(json, J, ct) ?? new();
        return await UpsertAllAsync(items, options, ct);
    }

    private async Task<ImportSummary> ImportCsvAsync(Stream csv, ImportOptions options, CancellationToken ct)
    {
        var list = new List<Raw>();
        using var parser = new TextFieldParser(csv)
        {
            TextFieldType = FieldType.Delimited
        };
        parser.SetDelimiters(",");
        string[]? header = null;
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row is null)
            {
                continue;
            }

            if (header is null)
            {
                header = row;
                continue;
            }

            var map = header.Zip(row, (h, v) => (h, v)).ToDictionary(x => x.h, x => x.v);
            list.Add(new Raw(
                map.TryGetValue("name", out var name) ? name : null,
                map.TryGetValue("type", out var type) ? type : null,
                map.TryGetValue("text", out var text) ? text : null,
                map.TryGetValue("set", out var set) ? set : null,
                map.TryGetValue("number", out var number) ? number : null,
                map.TryGetValue("rarity", out var rarity) ? rarity : null,
                map.TryGetValue("imageUrl", out var imageUrl) ? imageUrl : null,
                map.Where(kv => kv.Key is not "name" and not "type" and not "text" and not "set" and not "number" and not "rarity" and not "imageUrl")
                   .ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            ));
        }

        return await UpsertAllAsync(list, options, ct);
    }

    private async Task<ImportSummary> UpsertAllAsync(List<Raw> rows, ImportOptions options, CancellationToken ct)
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

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            int processed = 0;
            foreach (var row in rows)
            {
                if (processed++ >= limit)
                {
                    break;
                }

                try
                {
                    await UpsertAsync(row, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    summary.Messages.Add($"Error [{row.Set}/{row.Number}] {row.Name}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, rows.Count)} records.");
            return summary;
        });
    }

    private async Task UpsertAsync(Raw row, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Guardians CCG";
        if (string.IsNullOrWhiteSpace(row.Set) || string.IsNullOrWhiteSpace(row.Number))
        {
            throw new InvalidOperationException("Set and number are required.");
        }

        string name = row.Name?.Trim() ?? "Unknown";
        string type = row.Type ?? string.Empty;
        string? text = row.Text;
        string set = row.Set!.ToUpperInvariant();
        string number = row.Number!;
        string rarity = row.Rarity ?? "Unknown";
        string style = "Standard";
        string? imageUrl = row.ImageUrl;

        var card = _db.ChangeTracker.Entries<Card>()
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity)
            .FirstOrDefault(x => x.Game == game && x.Name == name)
            ?? await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);

        string detailsCard = JsonSerializer.Serialize(new { row.Extras, text, type }, J);

        if (card is null)
        {
            card = new Card
            {
                Game = game,
                Name = name,
                CardType = type,
                Description = text,
                DetailsJson = detailsCard
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

            if (card.DetailsJson != detailsCard)
            {
                card.DetailsJson = detailsCard;
                changed = true;
            }

            if (changed)
            {
                summary.CardsUpdated++;
            }
        }

        var printing = await _db.CardPrintings
            .Where(p => p.Set == set && p.Number == number)
            .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, c => c.Id, (p, _) => p)
            .FirstOrDefaultAsync(ct);

        string detailsPrinting = JsonSerializer.Serialize(new { row.Rarity, row.ImageUrl, row.Extras }, J);

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
                DetailsJson = detailsPrinting
            };
            _db.CardPrintings.Add(printing);
            summary.PrintingsCreated++;
        }
        else
        {
            bool changed = false;
            if (printing.CardId != card.Id)
            {
                printing.CardId = card.Id;
                changed = true;
            }

            if (printing.Rarity != rarity)
            {
                printing.Rarity = rarity;
                changed = true;
            }

            if (imageUrl is not null && printing.ImageUrl != imageUrl)
            {
                printing.ImageUrl = imageUrl;
                changed = true;
            }

            if (printing.DetailsJson != detailsPrinting)
            {
                printing.DetailsJson = detailsPrinting;
                changed = true;
            }

            if (changed)
            {
                summary.PrintingsUpdated++;
            }
        }
    }

    private sealed record Raw(
        string? Name,
        string? Type,
        string? Text,
        string? Set,
        string? Number,
        string? Rarity,
        string? ImageUrl,
        Dictionary<string, object?>? Extras
    );
}
