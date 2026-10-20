using System.Text.Json;
using api.Models;

namespace api.Importing;

/// <summary>Resolves shared SWU printing identities and relationships without relying on display names.</summary>
internal sealed class SwuSharedIdentity
{
    private readonly List<CardPrinting> _printings = [];
    private readonly Dictionary<int, CardPrinting> _bySource = [];
    private readonly Dictionary<string, CardPrinting> _bySerial = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Set, string Number), CardPrinting> _bySetNumber = [];

    internal SwuSharedIdentity(IEnumerable<CardPrinting> printings)
    {
        foreach (var printing in printings)
            Track(printing);
    }

    internal CardPrinting? FindPrinting(int sourceId, string? serialCode, string set, string number)
    {
        if (_bySource.TryGetValue(sourceId, out var printing)) return printing;
        if (serialCode is not null && _bySerial.TryGetValue(serialCode, out printing)) return printing;
        return _bySetNumber.GetValueOrDefault((set, serialCode ?? number));
    }

    internal void Track(CardPrinting printing)
    {
        if (!_printings.Contains(printing)) _printings.Add(printing);
        if (ReadInt(printing.DetailsJson, "sourceId") is int id && id > 0)
            _bySource[id] = printing;
        if (ReadString(printing.DetailsJson, "serialCode") is { } serial)
            _bySerial[serial] = printing;
        _bySerial.TryAdd(printing.Number, printing);
        _bySetNumber[(printing.Set, printing.Number)] = printing;
    }

    internal IReadOnlyList<Card> ResolveRelationships()
    {
        var changed = new List<Card>();
        var bySource = _printings
            .Where(p => ReadInt(p.DetailsJson, "sourceId") is > 0)
            .GroupBy(p => ReadInt(p.DetailsJson, "sourceId")!.Value)
            .ToDictionary(g => g.Key, g => g.First().Card);
        var byUid = _printings
            .Where(p => !string.IsNullOrWhiteSpace(ReadString(p.DetailsJson, "cardUid")))
            .GroupBy(p => ReadString(p.DetailsJson, "cardUid")!)
            .ToDictionary(g => g.Key, g => g.First().Card);

        foreach (var group in _printings.GroupBy(p => p.Card))
        {
            var card = group.Key;
            var baseCard = group.Select(p => ResolveBase(p, bySource, byUid))
                .FirstOrDefault(target => target is not null && !ReferenceEquals(target, card));
            if (card.BaseCardId == baseCard?.Id) continue;
            // A printing without a relationship must not erase another printing's valid link.
            // If no printing resolves, clear stale links after upstream changes/removals.
            card.BaseCard = baseCard;
            card.BaseCardId = baseCard?.Id;
            changed.Add(card);
        }
        return changed;
    }

    private static Card? ResolveBase(CardPrinting printing,
        IReadOnlyDictionary<int, Card> bySource, IReadOnlyDictionary<string, Card> byUid)
    {
        var json = HasProperty(printing.DetailsJson, "variantOfSourceId")
            ? printing.DetailsJson : printing.Card.DetailsJson;
        Card? baseCard = null;
        if (ReadInt(json, "variantOfSourceId") is int sourceId && sourceId > 0)
            bySource.TryGetValue(sourceId, out baseCard);
        var uid = ReadString(json, "variantOfCardUid");
        if (baseCard is null && !string.IsNullOrWhiteSpace(uid))
            byUid.TryGetValue(uid, out baseCard);
        return baseCard;
    }

    private static bool HasProperty(string? json, string name)
        => Read(json, name) is not null;

    private static int? ReadInt(string? json, string name)
        => Read(json, name) is { ValueKind: JsonValueKind.Number } value
            && value.TryGetInt32(out var number) ? number : null;

    private static string? ReadString(string? json, string name)
        => Read(json, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static JsonElement? Read(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var value) ? value.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed class SwuSharedCardChanges(ImportSummary summary)
{
    private readonly HashSet<Card> _created = [];
    private readonly HashSet<Card> _updated = [];

    internal void Created(Card card)
    {
        if (_created.Add(card)) summary.CardsCreated++;
    }

    internal void Updated(Card card)
    {
        if (!_created.Contains(card) && _updated.Add(card)) summary.CardsUpdated++;
    }
}
