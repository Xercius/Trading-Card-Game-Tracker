using api.Data;
using Microsoft.EntityFrameworkCore;

namespace api.Features.ImportExport.Services;

/// <summary>
/// Resolves exported card printings by primary key when valid, falling back to stable natural identifiers
/// (game, set, collector number, card name, style) when database IDs differ across environments.
/// </summary>
internal sealed class PrintingNaturalKeyResolver
{
    internal sealed record PrintingSnapshot(
        int Id,
        string Game,
        string CardName,
        string SetCode,
        string SetName,
        string Number,
        string Style);

    internal sealed record ResolutionResult(
        int? ResolvedPrintingId,
        bool UsedNaturalKey,
        bool HasNaturalKey,
        string? Reason);

    private readonly Dictionary<int, PrintingSnapshot> _byId;
    private readonly List<PrintingSnapshot> _all;

    private PrintingNaturalKeyResolver(List<PrintingSnapshot> snapshots)
    {
        _all = snapshots;
        _byId = snapshots.ToDictionary(s => s.Id);
    }

    public static async Task<PrintingNaturalKeyResolver> LoadAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.CardPrintings
            .AsNoTracking()
            .OrderBy(cp => cp.Id)
            .Select(cp => new
            {
                cp.Id,
                Game = cp.Card.Game.Name,
                CardName = cp.Card.Name,
                SetCode = cp.CatalogSet == null ? string.Empty : cp.CatalogSet.Code,
                SetName = cp.CatalogSet == null ? string.Empty : (cp.CatalogSet.Name ?? string.Empty),
                cp.Number,
                Style = cp.Treatment == null ? "Standard" : cp.Treatment.Name
            })
            .ToListAsync(ct);

        var snapshots = rows
            .Select(r => new PrintingSnapshot(r.Id, r.Game, r.CardName, r.SetCode, r.SetName, r.Number, r.Style))
            .ToList();

        return new PrintingNaturalKeyResolver(snapshots);
    }

    public ResolutionResult Resolve(
        int cardPrintingId,
        string? game,
        string? cardName,
        string? setCode,
        string? setName,
        string? number,
        string? style)
    {
        var hasNaturalKey = !string.IsNullOrWhiteSpace(cardName)
            || (!string.IsNullOrWhiteSpace(number)
                && (!string.IsNullOrWhiteSpace(setCode) || !string.IsNullOrWhiteSpace(setName)));

        if (_byId.TryGetValue(cardPrintingId, out var direct) && MatchesDirectKey(direct, game, cardName, setCode, number, style))
        {
            return new ResolutionResult(direct.Id, UsedNaturalKey: false, hasNaturalKey, null);
        }

        if (!hasNaturalKey)
        {
            return new ResolutionResult(null, UsedNaturalKey: false, HasNaturalKey: false, "Unknown CardPrintingId.");
        }

        var matched = FindByNaturalKey(game, cardName, setCode, setName, number, style);
        if (matched is not null)
        {
            return new ResolutionResult(matched.Id, UsedNaturalKey: true, HasNaturalKey: true, null);
        }

        var reason = string.IsNullOrWhiteSpace(cardName)
            ? $"No printing found matching set '{setCode ?? setName}' and number '{number}'."
            : $"No printing found matching '{cardName}' ({setCode ?? setName ?? "any set"} {number ?? ""}).".Trim();
        return new ResolutionResult(null, UsedNaturalKey: false, HasNaturalKey: true, reason);
    }

    private static bool MatchesDirectKey(
        PrintingSnapshot direct,
        string? game,
        string? cardName,
        string? setCode,
        string? number,
        string? style)
    {
        if (!string.IsNullOrWhiteSpace(game) && !Eq(direct.Game, game))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(cardName) && !Eq(direct.CardName, cardName))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(setCode) && !string.IsNullOrWhiteSpace(direct.SetCode) && !Eq(direct.SetCode, setCode))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(number) && !Eq(direct.Number, number))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(style) && !Eq(direct.Style, style))
        {
            return false;
        }
        return true;
    }

    private PrintingSnapshot? FindByNaturalKey(
        string? game,
        string? cardName,
        string? setCode,
        string? setName,
        string? number,
        string? style)
    {
        IEnumerable<PrintingSnapshot> scope = _all;
        if (!string.IsNullOrWhiteSpace(game))
        {
            scope = scope.Where(p => Eq(p.Game, game));
        }

        var candidates = scope.ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var hasSet = !string.IsNullOrWhiteSpace(setCode) || !string.IsNullOrWhiteSpace(setName);
        var hasNumber = !string.IsNullOrWhiteSpace(number);
        var hasName = !string.IsNullOrWhiteSpace(cardName);
        var hasStyle = !string.IsNullOrWhiteSpace(style);

        if (hasSet && hasNumber)
        {
            var bySetAndNumber = candidates
                .Where(p => MatchesSet(p, setCode, setName) && Eq(p.Number, number) && (!hasName || Eq(p.CardName, cardName)))
                .ToList();
            var picked = PickWithStyle(bySetAndNumber, style, hasStyle);
            if (picked is not null) return picked;
        }

        if (hasName && hasSet)
        {
            var byNameAndSet = candidates
                .Where(p => Eq(p.CardName, cardName) && MatchesSet(p, setCode, setName))
                .ToList();
            var picked = PickWithStyle(byNameAndSet, style, hasStyle);
            if (picked is not null) return picked;
        }

        if (hasName && hasNumber)
        {
            var byNameAndNumber = candidates
                .Where(p => Eq(p.CardName, cardName) && Eq(p.Number, number))
                .ToList();
            var picked = PickWithStyle(byNameAndNumber, style, hasStyle);
            if (picked is not null) return picked;
        }

        if (hasName)
        {
            var byName = candidates.Where(p => Eq(p.CardName, cardName)).ToList();
            return PickWithStyle(byName, style, hasStyle);
        }

        return null;
    }

    private static PrintingSnapshot? PickWithStyle(
        List<PrintingSnapshot> matches,
        string? style,
        bool hasStyle)
    {
        if (matches.Count == 0) return null;
        if (hasStyle)
        {
            var exactStyle = matches.FirstOrDefault(p => Eq(p.Style, style));
            if (exactStyle is not null) return exactStyle;
        }
        return matches[0];
    }

    private static bool MatchesSet(PrintingSnapshot p, string? setCode, string? setName)
    {
        var codeMatch = !string.IsNullOrWhiteSpace(setCode) && (Eq(p.SetCode, setCode) || Eq(p.SetName, setCode));
        var nameMatch = !string.IsNullOrWhiteSpace(setName) && (Eq(p.SetName, setName) || Eq(p.SetCode, setName));
        return codeMatch || nameMatch;
    }

    private static bool Eq(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
