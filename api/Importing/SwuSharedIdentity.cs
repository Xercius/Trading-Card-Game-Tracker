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
    private readonly Dictionary<PrintingKey, CardPrinting> _bySetNumberAndTreatment = [];
    private readonly HashSet<PrintingKey> _ambiguousFallbackKeys = [];
    private readonly Dictionary<int, CardPrinting> _bySourceId = [];

    internal SwuSharedIdentity(IEnumerable<CardPrinting> printings)
    {
        foreach (var printing in printings)
            Track(printing);
    }

    internal CardPrinting? FindPrinting(string? cardUid, Set set, string number, Treatment treatment)
    {
        if (!string.IsNullOrWhiteSpace(cardUid) && _byUid.TryGetValue(cardUid, out var printing))
            return printing;
        var fallbackKey = Key(set.Id, set, number, treatment.Id, treatment);
        // Distinct cards may share a (set, number, treatment) tuple; never adopt an ambiguous match.
        var fallback = _ambiguousFallbackKeys.Contains(fallbackKey)
            ? null
            : _bySetNumberAndTreatment.GetValueOrDefault(fallbackKey);
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
            if (oldKeys.SetNumberAndTreatment is { } oldFallbackKey)
                RemoveIfOwned(_bySetNumberAndTreatment, oldFallbackKey, printing);
        }

        var cardUid = SourceUid(printing);
        PrintingKey? fallbackKey = printing.CatalogSet is null || printing.Treatment is null
            ? null
            : Key(printing.SetId, printing.CatalogSet, printing.Number, printing.TreatmentId, printing.Treatment);
        var keys = new PrintingKeys(cardUid, fallbackKey);
        _keysByPrinting[printing] = keys;
        if (cardUid is not null) _byUid[cardUid] = printing;
        if (SourceId(printing) is > 0 and var sourceId) _bySourceId[sourceId] = printing;
        if (fallbackKey is not null)
        {
            if (_bySetNumberAndTreatment.TryGetValue(fallbackKey.Value, out var other) && !ReferenceEquals(other, printing))
                _ambiguousFallbackKeys.Add(fallbackKey.Value);
            _bySetNumberAndTreatment[fallbackKey.Value] = printing;
        }
        if (fallbackKey is not null) _bySetNumberAndTreatment[fallbackKey.Value] = printing;
    }

    internal void ResolveRelationships(out IReadOnlyList<CardPrinting> changedPrintings)
    {
        var updatedPrintings = new List<CardPrinting>();
        foreach (var printing in _printings)
        {
            var (variantOf, keepVariant) = FindTarget(printing, "variantOfCardUid", "variantOfSourceId");
            var (reprintOf, keepReprint) = FindTarget(printing, "reprintOfCardUid", "reprintOfSourceId");
            if (!keepVariant && printing.VariantOfId != variantOf?.Id || !keepReprint && printing.ReprintOfId != reprintOf?.Id)
                updatedPrintings.Add(printing);
            if (!keepVariant) printing.VariantOf = variantOf;
            if (!keepVariant) printing.VariantOfId = variantOf?.Id;
            if (!keepReprint) printing.ReprintOf = reprintOf;
            if (!keepReprint) printing.ReprintOfId = reprintOf?.Id;
        }
        changedPrintings = updatedPrintings;
    }

    internal static int? SourceId(CardPrinting printing)
        => ReadInt(printing.DetailsJson, "sourceId");

    internal static string? SourceUid(CardPrinting printing)
        => printing.CardUid ?? ReadString(printing.DetailsJson, "cardUid");

    internal static bool IsVariantOrReprint(CardPrinting printing)
    {
        var sourceJson = HasRelationshipFields(printing.DetailsJson)
            ? printing.DetailsJson
            : null;
        if (sourceJson is not null)
            return HasRelationship(sourceJson);

        return printing.VariantOfId is not null || printing.ReprintOfId is not null;
    }

    /// <summary>
    /// Resolves a relationship target by card UID, then by source ID for ID-only references.
    /// <c>Preserve</c> is true only for legacy rows without relationship metadata, whose stored link must be left alone;
    /// a declared target that cannot be resolved clears the link until it arrives.
    /// </summary>
    private (CardPrinting? Target, bool Preserve) FindTarget(CardPrinting printing, string uidField, string idField)
    {
        var cardUid = ReadString(printing.DetailsJson, uidField);
        var sourceId = ReadInt(printing.DetailsJson, idField);
        if (string.IsNullOrWhiteSpace(cardUid) && sourceId is not > 0)
        {
            // Explicit nulls mean "no relationship"; absent fields mean legacy metadata, so keep the stored link.
            var legacy = Read(printing.DetailsJson, uidField) is null && Read(printing.DetailsJson, idField) is null;
            return (null, legacy);
        }

        CardPrinting? found = !string.IsNullOrWhiteSpace(cardUid) && _byUid.TryGetValue(cardUid, out var byUid)
            ? byUid
            : sourceId is > 0 && _bySourceId.TryGetValue(sourceId.Value, out var byId) ? byId : null;
        return found is null || ReferenceEquals(found, printing)
            ? (null, false) : (found, false);
    }

    private static void RemoveIfOwned<TKey>(Dictionary<TKey, CardPrinting> index, TKey key, CardPrinting printing)
        where TKey : notnull
    {
        if (index.TryGetValue(key, out var indexed) && ReferenceEquals(indexed, printing))
            index.Remove(key);
    }

    private static PrintingKey Key(int? setId, Set set, string number, int? treatmentId, Treatment treatment)
        => new(setId is > 0 ? setId.Value : set, number,
            treatmentId is > 0 ? treatmentId.Value : treatment);

    private readonly record struct PrintingKey(object SetId, string Number, object TreatmentId);

    private sealed record PrintingKeys(string? CardUid, PrintingKey? SetNumberAndTreatment);

    private static bool HasRelationshipFields(string? json)
        => Read(json, "variantOfCardUid") is not null
           || Read(json, "reprintOfCardUid") is not null
           || Read(json, "variantOfSourceId") is not null
           || Read(json, "reprintOfSourceId") is not null;

    private static bool HasRelationship(string? json)
        => !string.IsNullOrWhiteSpace(ReadString(json, "variantOfCardUid"))
           || !string.IsNullOrWhiteSpace(ReadString(json, "reprintOfCardUid"))
           || ReadInt(json, "variantOfSourceId") is > 0
           || ReadInt(json, "reprintOfSourceId") is > 0;

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
