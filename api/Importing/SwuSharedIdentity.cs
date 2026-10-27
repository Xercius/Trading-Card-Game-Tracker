using System.Text.Json;
using api.Models;

namespace api.Importing;

/// <summary>Resolves shared SWU printing identities and relationships without relying on display names.</summary>
internal sealed class SwuSharedIdentity
{
    private readonly List<CardPrinting> _printings = [];
    private readonly HashSet<CardPrinting> _printingSet = [];
    private readonly Dictionary<CardPrinting, PrintingKeys> _keysByPrinting = [];
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
        if (_printingSet.Add(printing)) _printings.Add(printing);

        if (_keysByPrinting.TryGetValue(printing, out var oldKeys))
        {
            if (oldKeys.SourceId is { } oldSourceId)
                RemoveIfOwned(_bySource, oldSourceId, printing);
            if (oldKeys.SerialCode is not null)
                RemoveIfOwned(_bySerial, oldKeys.SerialCode, printing);
            RemoveIfOwned(_bySerial, oldKeys.Number, printing);
            RemoveIfOwned(_bySetNumber, oldKeys.SetNumber, printing);
        }

        var sourceIdValue = printing.StrapiId ?? ReadInt(printing.DetailsJson, "sourceId");
        var sourceId = sourceIdValue is > 0 ? sourceIdValue : null;
        var serialCode = ReadString(printing.DetailsJson, "serialCode");
        var keys = new PrintingKeys(sourceId, serialCode, printing.Number, (printing.Set, printing.Number));
        _keysByPrinting[printing] = keys;
        if (sourceId is { } idValue) _bySource[idValue] = printing;
        if (serialCode is not null) _bySerial[serialCode] = printing;
        _bySerial.TryAdd(printing.Number, printing);
        _bySetNumber[(printing.Set, printing.Number)] = printing;
    }

    internal IReadOnlyList<Card> ResolveRelationships(out IReadOnlyList<CardPrinting> changedPrintings)
    {
        var changed = new List<Card>();
        var updatedPrintings = new List<CardPrinting>();
        var bySource = _printings
            .Where(p => (p.StrapiId ?? ReadInt(p.DetailsJson, "sourceId")) is > 0)
            .GroupBy(p => (p.StrapiId ?? ReadInt(p.DetailsJson, "sourceId"))!.Value)
            .ToDictionary(g => g.Key, g => g.First().Card);
        var byUid = _printings
            .Where(p => !string.IsNullOrWhiteSpace(SourceUid(p)))
            .GroupBy(p => SourceUid(p)!)
            .ToDictionary(g => g.Key, g => g.First().Card);

        foreach (var printing in _printings)
        {
            var variantOf = FindTarget(printing, "variantOfSourceId");
            var reprintOf = FindTarget(printing, "reprintOfSourceId");
            if (printing.VariantOfId != variantOf?.Id || printing.ReprintOfId != reprintOf?.Id)
                updatedPrintings.Add(printing);
            printing.VariantOf = variantOf;
            printing.VariantOfId = variantOf?.Id;
            printing.ReprintOf = reprintOf;
            printing.ReprintOfId = reprintOf?.Id;
        }
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
        changedPrintings = updatedPrintings;
        return changed;
    }

    internal static int? SourceId(CardPrinting printing)
        => printing.StrapiId ?? ReadInt(printing.DetailsJson, "sourceId");

    internal static string? SourceUid(CardPrinting printing)
        => printing.CardUid ?? ReadString(printing.DetailsJson, "cardUid") ?? printing.Card.CardUid;

    internal static bool IsVariantOrReprint(CardPrinting printing)
    {
        var sourceJson = HasRelationshipFields(printing.DetailsJson)
            ? printing.DetailsJson
            : HasRelationshipFields(printing.Card.DetailsJson) ? printing.Card.DetailsJson : null;
        if (sourceJson is not null)
            return HasRelationship(sourceJson);

        return printing.VariantOfId is not null || printing.ReprintOfId is not null;
    }

    private CardPrinting? FindTarget(CardPrinting printing, string field)
    {
        var id = ReadInt(printing.DetailsJson, field);
        return id is > 0 && _bySource.TryGetValue(id.Value, out var target)
            && target != printing ? target : null;
    }

    private static Card? ResolveBase(CardPrinting printing,
        IReadOnlyDictionary<int, Card> bySource, IReadOnlyDictionary<string, Card> byUid)
    {
        var json = HasProperty(printing.DetailsJson, "variantOfSourceId")
            ? printing.DetailsJson : printing.Card.DetailsJson;
        Card? baseCard = null;
        var sourceId = ReadInt(json, "variantOfSourceId");
        var uid = ReadString(json, "variantOfCardUid");
        if (sourceId is not > 0 && string.IsNullOrWhiteSpace(uid))
        {
            sourceId = ReadInt(json, "reprintOfSourceId");
            uid = ReadString(json, "reprintOfCardUid");
        }

        if (sourceId is > 0)
            bySource.TryGetValue(sourceId.Value, out baseCard);
        if (baseCard is null && !string.IsNullOrWhiteSpace(uid))
            byUid.TryGetValue(uid, out baseCard);
        return baseCard;
    }

    private static void RemoveIfOwned<TKey>(Dictionary<TKey, CardPrinting> index, TKey key, CardPrinting printing)
        where TKey : notnull
    {
        if (index.TryGetValue(key, out var indexed) && ReferenceEquals(indexed, printing))
            index.Remove(key);
    }

    private sealed record PrintingKeys(
        int? SourceId,
        string? SerialCode,
        string Number,
        (string Set, string Number) SetNumber);

    private static bool HasProperty(string? json, string name)
        => Read(json, name) is not null;

    private static bool HasRelationshipFields(string? json)
        => HasProperty(json, "variantOfSourceId")
           || HasProperty(json, "variantOfCardUid")
           || HasProperty(json, "reprintOfSourceId")
           || HasProperty(json, "reprintOfCardUid");

    private static bool HasRelationship(string? json)
        => ReadInt(json, "variantOfSourceId") is > 0
           || !string.IsNullOrWhiteSpace(ReadString(json, "variantOfCardUid"))
           || ReadInt(json, "reprintOfSourceId") is > 0
           || !string.IsNullOrWhiteSpace(ReadString(json, "reprintOfCardUid"));

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
    private readonly HashSet<int> _created = [];
    private readonly HashSet<int> _updated = [];

    internal void Created(Card card)
    {
        if (_created.Add(card.Id)) summary.CardsCreated++;
    }

    internal void Updated(Card card)
    {
        if (!_created.Contains(card.Id) && _updated.Add(card.Id)) summary.CardsUpdated++;
    }
}
