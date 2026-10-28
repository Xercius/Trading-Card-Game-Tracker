using System.Text.Json;
using api.Models;

namespace api.Importing;

/// <summary>Resolves shared SWU printing identities and relationships without relying on display names.</summary>
internal sealed class SwuSharedIdentity
{
    private readonly List<CardPrinting> _printings = [];
    private readonly HashSet<CardPrinting> _printingSet = [];
    private readonly Dictionary<CardPrinting, PrintingKeys> _keysByPrinting = [];
    private readonly Dictionary<string, CardPrinting> _byUid = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Set, string Number, string Treatment), CardPrinting> _bySetNumberAndTreatment = [];

    internal SwuSharedIdentity(IEnumerable<CardPrinting> printings)
    {
        foreach (var printing in printings)
            Track(printing);
    }

    internal CardPrinting? FindPrinting(string? cardUid, string set, string number, string treatment)
    {
        if (!string.IsNullOrWhiteSpace(cardUid) && _byUid.TryGetValue(cardUid, out var printing))
            return printing;
        var fallback = _bySetNumberAndTreatment.GetValueOrDefault((set, number, treatment));
        if (string.IsNullOrWhiteSpace(cardUid)) return fallback;
        return fallback is not null && SourceUid(fallback) is null ? fallback : null;
    }

    internal void Track(CardPrinting printing)
    {
        if (_printingSet.Add(printing)) _printings.Add(printing);

        if (_keysByPrinting.TryGetValue(printing, out var oldKeys))
        {
            if (oldKeys.CardUid is not null)
                RemoveIfOwned(_byUid, oldKeys.CardUid, printing);
            RemoveIfOwned(_bySetNumberAndTreatment, oldKeys.SetNumberAndTreatment, printing);
        }

        var cardUid = SourceUid(printing);
        var keys = new PrintingKeys(cardUid, (printing.Set, printing.Number, printing.Treatment?.Name ?? "Standard"));
        _keysByPrinting[printing] = keys;
        if (cardUid is not null) _byUid[cardUid] = printing;
        _bySetNumberAndTreatment[(printing.Set, printing.Number, printing.Treatment?.Name ?? "Standard")] = printing;
    }

    internal IReadOnlyList<Card> ResolveRelationships(out IReadOnlyList<CardPrinting> changedPrintings)
    {
        var changed = new List<Card>();
        var updatedPrintings = new List<CardPrinting>();
        var cardsByUid = _printings
            .Where(p => !string.IsNullOrWhiteSpace(SourceUid(p)))
            .GroupBy(p => SourceUid(p)!)
            .ToDictionary(g => g.Key, g => g.First().Card);

        foreach (var printing in _printings)
        {
            var variantOf = FindTarget(printing, "variantOfCardUid");
            var reprintOf = FindTarget(printing, "reprintOfCardUid");
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
            var baseCard = group.Select(p => ResolveBase(p, cardsByUid))
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
        => printing.CardUid ?? ReadString(printing.DetailsJson, "cardUid");

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
        var cardUid = ReadString(printing.DetailsJson, field);
        return !string.IsNullOrWhiteSpace(cardUid) && _byUid.TryGetValue(cardUid, out var target)
            && target != printing ? target : null;
    }

    private static Card? ResolveBase(CardPrinting printing,
        IReadOnlyDictionary<string, Card> byUid)
    {
        var json = printing.DetailsJson;
        Card? baseCard = null;
        var uid = ReadString(json, "variantOfCardUid");
        if (string.IsNullOrWhiteSpace(uid))
            uid = ReadString(json, "reprintOfCardUid");

        if (!string.IsNullOrWhiteSpace(uid))
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
        string? CardUid,
        (string Set, string Number, string Treatment) SetNumberAndTreatment);

    private static bool HasRelationshipFields(string? json)
        => Read(json, "variantOfCardUid") is not null
           || Read(json, "reprintOfCardUid") is not null;

    private static bool HasRelationship(string? json)
        => !string.IsNullOrWhiteSpace(ReadString(json, "variantOfCardUid"))
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
