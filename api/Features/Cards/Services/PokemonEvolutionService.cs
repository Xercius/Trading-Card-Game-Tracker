using System.Text.Json;
using api.Data;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards.Services;

/// <summary>
/// Resolves Pokémon TCG card names for autocomplete and full evolutionary lineages
/// (ancestors, self/variants, and multi-stage descendants).
/// </summary>
public sealed class PokemonEvolutionService
{
    private static readonly string[] NamePrefixes =
    [
        "Mega ",
        "M ",
        "Radiant ",
        "Shining ",
        "Dark ",
        "Light ",
        "Alolan ",
        "Galarian ",
        "Hisuian ",
        "Paldean ",
        "Surfing ",
        "Flying ",
        "Detective ",
    ];

    private static readonly string[] NameSuffixes =
    [
        " V-UNION",
        " VMAX",
        " VSTAR",
        " V",
        " ex",
        "-EX",
        " EX",
        "-GX",
        " GX",
        " BREAK",
        " LEGEND",
        " LV.X",
        " Prime",
        " SP",
    ];

    private readonly AppDbContext _db;

    public PokemonEvolutionService(AppDbContext db) => _db = db;

    /// <summary>
    /// Searches distinct Pokémon card names containing <paramref name="query"/> (case-insensitive),
    /// ordering prefix matches before substring matches.
    /// </summary>
    public async Task<IReadOnlyList<string>> SearchPokemonNamesAsync(
        string query,
        int limit,
        CancellationToken ct)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Array.Empty<string>();
        }

        var clampedLimit = Math.Clamp(limit, 1, 50);
        var pattern = $"%{trimmed}%";

        var names = await PokemonCardsQuery()
            .Where(c => EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern))
            .Select(c => c.Name)
            .Distinct()
            .ToListAsync(ct);

        return names
            .OrderByDescending(n => n.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(clampedLimit)
            .ToList();
    }

    /// <summary>
    /// Resolves all Pokémon <c>Card.Id</c> values in the evolutionary lineage of <paramref name="pokemonName"/>,
    /// including all cards it evolves from, all variants of itself, and all cards it evolves into.
    /// </summary>
    public async Task<IReadOnlyList<int>> ResolveLineageCardIdsAsync(
        string pokemonName,
        CancellationToken ct)
    {
        var trimmed = pokemonName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Array.Empty<int>();
        }

        var rawCards = await PokemonCardsQuery()
            .Select(c => new { c.Id, c.Name, c.ExtraCardDataJson })
            .ToListAsync(ct);

        if (rawCards.Count == 0)
        {
            return Array.Empty<int>();
        }

        var cards = new List<PokemonCardNode>(rawCards.Count);
        var nameToKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var baseNameToKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawCards)
        {
            var (evolveFrom, dexIds) = ParseEvolutionMetadata(raw.ExtraCardDataJson);
            var baseName = NormalizeBaseName(raw.Name);
            var keys = dexIds.Count > 0
                ? dexIds.Select(id => $"dex:{id}").ToList()
                : [$"name:{baseName}"];

            cards.Add(new PokemonCardNode(raw.Id, raw.Name, evolveFrom, keys));

            // Only single-species cards seed/bridge name lookups so Tag Team cards don't merge unrelated lines.
            if (keys.Count == 1)
            {
                AddToSetMap(nameToKeys, raw.Name.Trim(), keys[0]);
                AddToSetMap(baseNameToKeys, baseName, keys[0]);
            }
        }

        var forwardEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var reverseEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var card in cards)
        {
            if (card.SpeciesKeys.Count != 1 || string.IsNullOrWhiteSpace(card.EvolveFrom))
            {
                continue;
            }

            var childKey = card.SpeciesKeys[0];
            var parentKeys = ResolveKeysForName(card.EvolveFrom, nameToKeys, baseNameToKeys);
            foreach (var parentKey in parentKeys)
            {
                if (parentKey == childKey)
                {
                    continue;
                }

                AddToSetMap(forwardEdges, parentKey, childKey);
                AddToSetMap(reverseEdges, childKey, parentKey);
            }
        }

        var seedKeys = ResolveKeysForName(trimmed, nameToKeys, baseNameToKeys);
        if (seedKeys.Count == 0)
        {
            return cards
                .Where(c =>
                    string.Equals(c.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.EvolveFrom?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .ToList();
        }

        var lineageKeys = new HashSet<string>(seedKeys, StringComparer.Ordinal);
        CollectReachable(seedKeys, reverseEdges, lineageKeys);
        CollectReachable(seedKeys, forwardEdges, lineageKeys);

        return cards
            .Where(c => c.SpeciesKeys.Any(lineageKeys.Contains))
            .Select(c => c.Id)
            .ToList();
    }

    private IQueryable<Models.Card> PokemonCardsQuery() =>
        _db.Cards
            .AsNoTracking()
            .Where(c =>
                c.Game.NameNormalized == "pokemon tcg" ||
                c.Game.NameNormalized == "pokemontcg")
            .Where(c => c.CardTypes.Any(ct =>
                EF.Functions.Collate(ct.CardType.Name, "NOCASE") == "Pokemon"));

    private static HashSet<string> ResolveKeysForName(
        string name,
        Dictionary<string, HashSet<string>> nameToKeys,
        Dictionary<string, HashSet<string>> baseNameToKeys)
    {
        var trimmed = name.Trim();
        if (nameToKeys.TryGetValue(trimmed, out var exactKeys) && exactKeys.Count > 0)
        {
            return exactKeys;
        }

        var baseName = NormalizeBaseName(trimmed);
        return baseNameToKeys.TryGetValue(baseName, out var baseKeys)
            ? baseKeys
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static void CollectReachable(
        IEnumerable<string> seeds,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> visited)
    {
        var queue = new Queue<string>(seeds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!edges.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
    }

    private static void AddToSetMap(
        Dictionary<string, HashSet<string>> map,
        string key,
        string value)
    {
        if (!map.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            map[key] = set;
        }

        set.Add(value);
    }

    private static string NormalizeBaseName(string name)
    {
        var current = name.Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in NamePrefixes)
            {
                if (current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    current.Length > prefix.Length)
                {
                    current = current[prefix.Length..].TrimStart();
                    changed = true;
                }
            }

            foreach (var suffix in NameSuffixes)
            {
                if (current.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    current.Length > suffix.Length)
                {
                    current = current[..^suffix.Length].TrimEnd();
                    changed = true;
                }
            }
        }

        return current.ToLowerInvariant();
    }

    private static (string? EvolveFrom, List<int> DexIds) ParseEvolutionMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, []);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, []);
            }

            string? evolveFrom = null;
            if (doc.RootElement.TryGetProperty("evolveFrom", out var evolveProp) &&
                evolveProp.ValueKind == JsonValueKind.String)
            {
                evolveFrom = evolveProp.GetString();
            }

            var dexIds = new List<int>();
            if (doc.RootElement.TryGetProperty("dexId", out var dexProp) &&
                dexProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dexProp.EnumerateArray())
                {
                    if (item.TryGetInt32(out var id))
                    {
                        dexIds.Add(id);
                    }
                }
            }

            return (evolveFrom, dexIds);
        }
        catch (JsonException)
        {
            return (null, []);
        }
    }

    private sealed record PokemonCardNode(
        int Id,
        string Name,
        string? EvolveFrom,
        IReadOnlyList<string> SpeciesKeys);
}
