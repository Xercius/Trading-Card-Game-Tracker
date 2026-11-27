using api.Data;
using api.Features.Decks.Dtos;
using api.Models;
using api.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace api.Features.Decks.Services;

/// <summary>
/// Resolves plain-text decklist imports against a game's card catalog, applies confirmed entries to a deck,
/// and formats existing decks into round-trippable plain-text decklists.
/// </summary>
public sealed class DecklistTextService(AppDbContext db, TimeProvider timeProvider, WishlistSourceSyncService sourceSync)
{
    private sealed record CatalogPrintingRow(
        int PrintingId,
        int CardId,
        string CardName,
        string? Subtitle,
        string? SetCode,
        string? SetName,
        string Number,
        string Style,
        string? ImageUrl,
        HashSet<string> CardTypes);

    /// <summary>Parses plain-text decklist lines and resolves candidate printings for <paramref name="deck"/>.</summary>
    public async Task<DecklistImportPreviewResponse> PreviewImportAsync(
        Deck deck,
        string text,
        CancellationToken ct)
    {
        var parsedLines = DecklistLineParser.Parse(text);
        var catalog = await LoadGameCatalogAsync(deck.Game, ct);

        var lineResults = new List<DecklistImportLineResult>(parsedLines.Count);
        var resolvedCount = 0;
        var ambiguousCount = 0;
        var unmatchedCount = 0;

        foreach (var line in parsedLines)
        {
            var result = ResolveLine(line, catalog);
            lineResults.Add(result);
            switch (result.Status)
            {
                case "resolved":
                    resolvedCount++;
                    break;
                case "ambiguous":
                    ambiguousCount++;
                    break;
                default:
                    unmatchedCount++;
                    break;
            }
        }

        return new DecklistImportPreviewResponse(lineResults, resolvedCount, ambiguousCount, unmatchedCount);
    }

    /// <summary>Applies resolved/user-selected decklist items to <paramref name="deck"/> inside a transaction.</summary>
    public async Task<(DecklistImportApplyResponse? Response, string? ErrorKey, string? ErrorMessage)> ApplyImportAsync(
        Deck deck,
        DecklistImportApplyRequest request,
        CancellationToken ct)
    {
        var items = request.Items ?? [];
        var printingIds = items.Select(i => i.CardPrintingId).Distinct().ToList();

        var printings = await db.CardPrintings
            .AsNoTracking()
            .Where(cp => printingIds.Contains(cp.Id))
            .Select(cp => new { cp.Id, GameName = cp.Card.Game.Name })
            .ToDictionaryAsync(cp => cp.Id, ct);

        foreach (var id in printingIds)
        {
            if (!printings.TryGetValue(id, out var found))
            {
                return (null, "cardPrintingId", $"Card printing {id} was not found.");
            }
            if (!string.Equals(found.GameName, deck.Game, StringComparison.OrdinalIgnoreCase))
            {
                return (null, "cardPrintingId", $"Card printing {id} does not belong to {deck.Game}.");
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (request.ReplaceExisting)
        {
            await sourceSync.RemoveDeckSourcesAsync(deck.Id, ct);
            var existing = await db.DeckCards.Where(dc => dc.DeckId == deck.Id).ToListAsync(ct);
            if (existing.Count > 0) db.DeckCards.RemoveRange(existing);
            await db.SaveChangesAsync(ct);
        }

        var deckCardMap = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id)
            .ToDictionaryAsync(dc => dc.CardPrintingId, ct);

        var appliedLines = 0;
        var totalCopies = 0;

        foreach (var item in items)
        {
            if (item.Quantity <= 0) continue;

            if (!deckCardMap.TryGetValue(item.CardPrintingId, out var dc))
            {
                dc = new DeckCard { DeckId = deck.Id, CardPrintingId = item.CardPrintingId };
                deckCardMap[item.CardPrintingId] = dc;
                db.DeckCards.Add(dc);
            }

            ApplyZoneToDeckCard(dc, item.Zone, item.Quantity);
            appliedLines++;
            totalCopies += item.Quantity;
        }

        deck.UpdatedUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (new DecklistImportApplyResponse(appliedLines, totalCopies), null, null);
    }

    /// <summary>Formats a deck and its loaded cards into a clean, round-trippable plain-text decklist.</summary>
    public DecklistExportTextResponse ExportText(Deck deck, IReadOnlyList<DeckCard> cards)
    {
        var sb = new StringBuilder();
        var isSwu = deck.Game.Contains("unlimited", StringComparison.OrdinalIgnoreCase);
        var isSwccg = deck.Game.Contains("ccg", StringComparison.OrdinalIgnoreCase);
        var isMtg = deck.Game.Contains("magic", StringComparison.OrdinalIgnoreCase);

        if (isSwu)
        {
            AppendSection(sb, "Leader", cards.Where(c => c.QuantityInDeck > 0 && HasType(c, "Leader")).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Base", cards.Where(c => c.QuantityInDeck > 0 && HasType(c, "Base")).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Main Deck", cards.Where(c => c.QuantityInDeck > 0 && !HasType(c, "Leader") && !HasType(c, "Base")).Select(c => (c, c.QuantityInDeck)));
        }
        else if (isSwccg)
        {
            AppendSection(sb, "Starting", cards.Where(c => c.QuantityInDeck > 0 && c.IsStarting).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Defensive Shields", cards.Where(c => c.QuantityInDeck > 0 && !c.IsStarting && HasType(c, "Defensive Shield")).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Main Deck", cards.Where(c => c.QuantityInDeck > 0 && !c.IsStarting && !HasType(c, "Defensive Shield")).Select(c => (c, c.QuantityInDeck)));
        }
        else if (isMtg)
        {
            AppendSection(sb, "Commander", cards.Where(c => c.QuantityInDeck > 0 && c.IsCommander).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Main Deck", cards.Where(c => c.QuantityInDeck > 0 && !c.IsCommander).Select(c => (c, c.QuantityInDeck)));
            AppendSection(sb, "Sideboard", cards.Where(c => c.QuantitySideboard > 0).Select(c => (c, c.QuantitySideboard)));
        }
        else
        {
            AppendSection(sb, "Main Deck", cards.Where(c => c.QuantityInDeck > 0).Select(c => (c, c.QuantityInDeck)));
        }

        AppendSection(sb, "Ideas", cards.Where(c => c.QuantityIdea > 0).Select(c => (c, c.QuantityIdea)));

        var sanitizedName = new string(deck.Name
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray()).Trim();
        var fileName = string.IsNullOrWhiteSpace(sanitizedName) ? "deck.txt" : $"{sanitizedName}.txt";

        return new DecklistExportTextResponse(deck.Name, deck.Game, deck.Format, sb.ToString().TrimEnd(), fileName);
    }

    private async Task<List<CatalogPrintingRow>> LoadGameCatalogAsync(string game, CancellationToken ct)
    {
        var raw = await db.CardPrintings
            .AsNoTracking()
            .Where(cp => EF.Functions.Collate(cp.Card.Game.Name, "NOCASE") == game)
            .OrderBy(cp => cp.Id)
            .Select(cp => new
            {
                cp.Id,
                cp.CardId,
                CardName = cp.Card.Name,
                cp.Card.Subtitle,
                SetCode = cp.CatalogSet != null ? cp.CatalogSet.Code : null,
                SetName = cp.CatalogSet != null ? cp.CatalogSet.Name : null,
                cp.Number,
                Style = cp.Treatment != null ? cp.Treatment.Name : "Standard",
                cp.ImageUrl,
                CardTypes = cp.Card.CardTypes.Select(t => t.CardType.Name).ToList()
            })
            .ToListAsync(ct);

        return raw.Select(r => new CatalogPrintingRow(
            r.Id, r.CardId, r.CardName, r.Subtitle, r.SetCode, r.SetName, r.Number, r.Style, r.ImageUrl,
            r.CardTypes.ToHashSet(StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private static DecklistImportLineResult ResolveLine(
        DecklistLineParser.ParsedDecklistLine line,
        List<CatalogPrintingRow> catalog)
    {
        if (!string.IsNullOrWhiteSpace(line.ParseError))
        {
            return new DecklistImportLineResult(
                line.LineNumber, line.RawLine, line.Quantity, line.CardName,
                line.SetCode, line.CollectorNumber, line.Zone, "unmatched", null, [], line.ParseError);
        }

        var matches = FindMatches(line.CardName, line.SetCode, line.CollectorNumber, line.Zone, catalog);
        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(line.UnsplitName))
        {
            matches = FindMatches(line.UnsplitName, null, null, line.Zone, catalog);
        }

        if (matches.Count == 0)
        {
            return new DecklistImportLineResult(
                line.LineNumber, line.RawLine, line.Quantity, line.CardName,
                line.SetCode, line.CollectorNumber, line.Zone, "unmatched", null, [],
                $"Card '{line.CardName}' was not found in the game catalog.");
        }

        var candidates = matches.Select(ToCandidateDto).ToList();
        if (matches.Count == 1)
        {
            return new DecklistImportLineResult(
                line.LineNumber, line.RawLine, line.Quantity, line.CardName,
                line.SetCode, line.CollectorNumber, line.Zone, "resolved", matches[0].PrintingId, candidates, null);
        }

        var preferred = matches.FirstOrDefault(m => string.Equals(m.Style, "Standard", StringComparison.OrdinalIgnoreCase))
            ?? matches[0];
        return new DecklistImportLineResult(
            line.LineNumber, line.RawLine, line.Quantity, line.CardName,
            line.SetCode, line.CollectorNumber, line.Zone, "ambiguous", preferred.PrintingId, candidates,
            $"{matches.Count} printings match '{line.CardName}'; choose a printing or keep the default.");
    }

    private static List<CatalogPrintingRow> FindMatches(
        string cardName,
        string? setCode,
        string? collectorNumber,
        string zone,
        List<CatalogPrintingRow> catalog)
    {
        var nameMatches = catalog.Where(p => MatchesCardName(p, cardName)).ToList();
        if (nameMatches.Count == 0) return [];

        if (zone == "leader")
        {
            var leaders = nameMatches.Where(p => p.CardTypes.Contains("Leader")).ToList();
            if (leaders.Count > 0) nameMatches = leaders;
        }
        else if (zone == "base")
        {
            var bases = nameMatches.Where(p => p.CardTypes.Contains("Base")).ToList();
            if (bases.Count > 0) nameMatches = bases;
        }
        else if (zone == "shields")
        {
            var shields = nameMatches.Where(p => p.CardTypes.Contains("Defensive Shield")).ToList();
            if (shields.Count > 0) nameMatches = shields;
        }

        if (!string.IsNullOrWhiteSpace(setCode))
        {
            var setFiltered = nameMatches.Where(p =>
                string.Equals(p.SetCode, setCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.SetName, setCode, StringComparison.OrdinalIgnoreCase)).ToList();
            if (setFiltered.Count == 0) return [];
            nameMatches = setFiltered;
        }

        if (!string.IsNullOrWhiteSpace(collectorNumber))
        {
            var normalizedTarget = NormalizeNumber(collectorNumber);
            var numFiltered = nameMatches.Where(p =>
                string.Equals(NormalizeNumber(p.Number), normalizedTarget, StringComparison.OrdinalIgnoreCase)).ToList();
            if (numFiltered.Count == 0) return [];
            nameMatches = numFiltered;
        }

        return nameMatches;
    }

    private static bool MatchesCardName(CatalogPrintingRow p, string rawQuery)
    {
        var query = NormalizeCardName(rawQuery);
        var baseName = NormalizeCardName(p.CardName);
        if (string.Equals(baseName, query, StringComparison.OrdinalIgnoreCase)) return true;

        if (!string.IsNullOrWhiteSpace(p.Subtitle))
        {
            var sub = p.Subtitle.Trim();
            if (string.Equals($"{baseName}, {sub}", query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{baseName} - {sub}", query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{baseName} — {sub}", query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var splitIdx = baseName.IndexOf(" // ", StringComparison.Ordinal);
        return splitIdx > 0 && string.Equals(baseName[..splitIdx].Trim(), query, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCardName(string name) =>
        name.Trim().Replace("<>", string.Empty, StringComparison.Ordinal).TrimStart('•', '*', ' ').Trim();

    private static string NormalizeNumber(string number)
    {
        var stripped = number.Trim().TrimStart('#').TrimStart('0');
        return string.IsNullOrEmpty(stripped) ? "0" : stripped;
    }

    private static void ApplyZoneToDeckCard(DeckCard dc, string zone, int quantity)
    {
        switch (zone.ToLowerInvariant())
        {
            case "sideboard":
                dc.QuantitySideboard += quantity;
                break;
            case "ideas":
                dc.QuantityIdea += quantity;
                break;
            case "commander":
                dc.QuantityInDeck += quantity;
                dc.IsCommander = true;
                break;
            case "starting":
                dc.QuantityInDeck += quantity;
                dc.IsStarting = true;
                break;
            default:
                dc.QuantityInDeck += quantity;
                break;
        }
    }

    private static void AppendSection(
        StringBuilder sb,
        string header,
        IEnumerable<(DeckCard Card, int Qty)> items)
    {
        var list = items.OrderBy(i => i.Card.CardPrinting?.Card.Name ?? string.Empty).ToList();
        if (list.Count == 0) return;

        if (sb.Length > 0) sb.AppendLine();
        sb.AppendLine($"{header}:");

        foreach (var (dc, qty) in list)
        {
            var printing = dc.CardPrinting;
            var card = printing?.Card;
            if (printing is null || card is null) continue;

            var displayName = string.IsNullOrWhiteSpace(card.Subtitle)
                ? card.Name
                : $"{card.Name}, {card.Subtitle}";
            var setCode = printing.CatalogSet?.Code;
            var number = printing.Number;

            if (!string.IsNullOrWhiteSpace(setCode) && !string.IsNullOrWhiteSpace(number))
            {
                sb.AppendLine($"{qty} {displayName} ({setCode}) {number}");
            }
            else if (!string.IsNullOrWhiteSpace(setCode))
            {
                sb.AppendLine($"{qty} {displayName} ({setCode})");
            }
            else
            {
                sb.AppendLine($"{qty} {displayName}");
            }
        }
    }

    private static bool HasType(DeckCard dc, string typeName) =>
        dc.CardPrinting?.Card.CardTypes.Any(t =>
            string.Equals(t.CardType.Name, typeName, StringComparison.OrdinalIgnoreCase)) == true;

    private static DecklistPrintingCandidateDto ToCandidateDto(CatalogPrintingRow r) =>
        new(r.PrintingId, r.CardId, r.CardName, r.Subtitle, r.SetCode, r.SetName, r.Number, r.Style, r.ImageUrl);
}
