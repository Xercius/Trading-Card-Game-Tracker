using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Swccg;

/// <summary>
/// Maintains game-scoped lookups, sets, associations, and rich rule formatting for Star Wars CCG.
/// </summary>
internal sealed class SwccgCatalog(AppDbContext db, TimeProvider clock)
{
    private const string GameName = "Star Wars CCG";
    private Game _game = null!;
    private readonly Dictionary<string, SwccgSet> _setMetaById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SwccgSet> _setMetaByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SwccgSet> _setMetaByAbbr = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SwccgSet> _setMetaByGempName = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Set> _setsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Treatment> _treatmentsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rarity> _raritiesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CardType> _cardTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SubType> _subTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Trait> _traitsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Aspect> _aspectsByName = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _setNames = new(KnownSwccgSetNames, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] KnownSwccgSetNames = BuildKnownSetNames();

    private static string[] BuildKnownSetNames()
    {
        var list = new List<string>
        {
            "Premiere",
            "A New Hope",
            "Hoth",
            "Dagobah",
            "Cloud City",
            "Jabba's Palace",
            "Special Edition",
            "Endor",
            "Death Star II",
            "Tatooine",
            "Coruscant",
            "Theed Palace",
            "Reflections",
            "Reflections II",
            "Reflections III",
            "Enhanced Premiere",
            "Enhanced Cloud City",
            "Enhanced Jabba's Palace",
            "First Anthology",
            "Second Anthology",
            "Third Anthology",
            "Official Tournament Sealed Deck",
            "Jabba's Palace Sealed Deck",
            "Jedi Pack",
            "Rebel Leader Pack",
            "Premiere Introductory Two Player Game",
            "Empire Strikes Back Introductory Two Player Game",
            "Virtual",
            "Virtual Set",
            "Virtual Premium Set",
            "Virtual Premium",
            "Virtual Defensive Shields",
            "Virtual Shields",
            "Defensive Shields",
            "Set P",
            "Set D"
        };

        for (var i = 0; i <= 35; i++)
        {
            list.Add($"Set {i}");
            list.Add($"Virtual Set {i}");
        }

        return [.. list];
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private sealed record SwccgRarityDefinition(string Character, string Name, string RarityType);

    private static readonly SwccgRarityDefinition[] RarityDefinitions =
    [
        new("C", "Common", "Common"),
        new("C1", "Common 1", "Common"),
        new("C2", "Common 2", "Common"),
        new("C3", "Common 3", "Common"),
        new("F", "Fixed", "Fixed"),
        new("PM", "Premium", "Premium"),
        new("PM2", "Premium (2 copies)", "Premium"),
        new("PM3", "Premium (3 copies)", "Premium"),
        new("PM5", "Premium (5 copies)", "Premium"),
        new("PV", "Preview", "Preview"),
        new("R", "Rare", "Rare"),
        new("R1", "Rare 1", "Rare"),
        new("R2", "Rare 2", "Rare"),
        new("U", "Uncommon", "Uncommon"),
        new("U1", "Uncommon 1", "Uncommon"),
        new("U2", "Uncommon 2", "Uncommon"),
        new("UR", "Ultra-Rare", "Ultra-Rare"),
        new("XR", "Exclusive Rare", "Exclusive Rare")
    ];

    private static readonly Dictionary<string, SwccgRarityDefinition> RarityByCharacter =
        RarityDefinitions.ToDictionary(r => r.Character, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, SwccgRarityDefinition> RarityByName =
        RarityDefinitions.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    internal Game Game => _game;

    internal void InitializeSetMetadata(IEnumerable<SwccgSet> sets)
    {
        foreach (var s in sets)
        {
            if (!string.IsNullOrWhiteSpace(s.Id))
                _setMetaById[s.Id.Trim()] = s;
            if (!string.IsNullOrWhiteSpace(s.Name))
            {
                var trimmedName = s.Name.Trim();
                _setMetaByName[trimmedName] = s;
                _setNames.Add(trimmedName);
            }
            if (!string.IsNullOrWhiteSpace(s.Abbr))
                _setMetaByAbbr[s.Abbr.Trim()] = s;
            if (!string.IsNullOrWhiteSpace(s.GempName))
            {
                var trimmedGempName = s.GempName.Trim();
                _setMetaByGempName[trimmedGempName] = s;
                _setNames.Add(trimmedGempName);
            }
        }
    }

    internal async Task LoadAsync(CancellationToken ct)
    {
        await db.Games.LoadAsync(ct);
        _game = db.Games.Local.SingleOrDefault(g => g.Name.Trim().Equals(
            GameName, StringComparison.OrdinalIgnoreCase)) ?? new Game { Name = GameName };
        if (db.Entry(_game).State == EntityState.Detached)
            db.Games.Add(_game);

        var sets = await db.Sets.Where(s => s.GameId == _game.Id).ToListAsync(ct);
        foreach (var s in sets)
        {
            if (!string.IsNullOrWhiteSpace(s.Code))
                _setsByCode[s.Code.Trim()] = s;
            if (!string.IsNullOrWhiteSpace(s.Name))
                _setNames.Add(s.Name.Trim());
        }

        var treatments = await db.Treatments.Where(t => t.GameId == _game.Id).ToListAsync(ct);
        foreach (var t in treatments)
            if (!string.IsNullOrWhiteSpace(t.Name))
                _treatmentsByName[t.Name.Trim()] = t;

        var rarities = await db.Rarities.Where(r => r.GameId == _game.Id).ToListAsync(ct);
        foreach (var r in rarities)
            if (!string.IsNullOrWhiteSpace(r.Name))
                _raritiesByName[r.Name.Trim()] = r;

        var cardTypes = await db.CardTypes.Where(ctp => ctp.GameId == _game.Id).ToListAsync(ct);
        foreach (var ctp in cardTypes)
            if (!string.IsNullOrWhiteSpace(ctp.Name))
                _cardTypesByName[ctp.Name.Trim()] = ctp;

        var subTypes = await db.SubTypes.Where(st => st.GameId == _game.Id).ToListAsync(ct);
        foreach (var st in subTypes)
            if (!string.IsNullOrWhiteSpace(st.Name))
                _subTypesByName[st.Name.Trim()] = st;

        var traits = await db.Traits.Where(tr => tr.GameId == _game.Id).ToListAsync(ct);
        foreach (var tr in traits)
            if (!string.IsNullOrWhiteSpace(tr.Name))
                _traitsByName[tr.Name.Trim()] = tr;

        var aspects = await db.Aspects.Where(a => a.GameId == _game.Id).ToListAsync(ct);
        foreach (var a in aspects)
            if (!string.IsNullOrWhiteSpace(a.Name))
                _aspectsByName[a.Name.Trim()] = a;

        if (_game.Id > 0)
        {
            await db.Set<CardCardType>().Where(x => x.Card.GameId == _game.Id).LoadAsync(ct);
            await db.Set<CardSubType>().Where(x => x.Card.GameId == _game.Id).LoadAsync(ct);
            await db.Set<CardTrait>().Where(x => x.Card.GameId == _game.Id).LoadAsync(ct);
            await db.Set<CardAspect>().Where(x => x.Card.GameId == _game.Id).LoadAsync(ct);

            // Clean up any legacy SWCCG Type2 associations if present from earlier syncs
            var legacyType2 = await db.Set<CardCardType>()
                .Where(x => x.Card.GameId == _game.Id && x.SourceRole == "Type2")
                .ToListAsync(ct);
            if (legacyType2.Count > 0)
                db.Set<CardCardType>().RemoveRange(legacyType2);

            // Clean up any legacy SWCCG keywords if present from an earlier sync
            var legacyCardKeywords = await db.Set<CardKeyword>().Where(x => x.Card.GameId == _game.Id).ToListAsync(ct);
            if (legacyCardKeywords.Count > 0)
                db.Set<CardKeyword>().RemoveRange(legacyCardKeywords);

            var legacyKeywords = await db.Keywords.Where(kw => kw.GameId == _game.Id).ToListAsync(ct);
            if (legacyKeywords.Count > 0)
                db.Keywords.RemoveRange(legacyKeywords);
        }
    }

    internal bool Apply(Card card, CardPrinting printing, SwccgCardRecord record, bool includeCardAssociations = true)
    {
        var associationsChanged = false;
        var face = record.Front;

        // Resolve and assign CatalogSet
        var setCodeOrName = record.Set;
        if (string.IsNullOrWhiteSpace(setCodeOrName) && !string.IsNullOrWhiteSpace(record.GempId))
        {
            var under = record.GempId.IndexOf('_');
            if (under > 0) setCodeOrName = record.GempId[..under];
        }
        var set = GetOrAddSet(setCodeOrName);
        printing.CatalogSet = set;

        // Treatment (Standard, Foil, Virtual, Alternate Image)
        var treatmentName = ResolveTreatmentName(record);
        printing.Treatment = GetOrAddTreatment(treatmentName);

        // Rarity
        printing.RarityLookup = GetOrAddRarity(record.Rarity);

        if (!includeCardAssociations)
            return false;

        // Card Types: Primary Type only
        var types = new List<CardCardType>();
        if (!string.IsNullOrWhiteSpace(face?.Type))
        {
            var primaryType = Lookup(face.Type.Trim(), _cardTypesByName, n => new CardType { Game = _game, Name = n });
            types.Add(new CardCardType { Card = card, CardType = primaryType, SourceRole = "Type" });
        }
        associationsChanged |= Sync(card.CardTypes, types, x => (x.CardType, x.SourceRole));

        // SubTypes: populated from JSON subType (front and back)
        var subTypes = new List<CardSubType>();
        var subTypeNames = new[] { face?.SubType, record.Back?.SubType }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var subTypeName in subTypeNames)
        {
            var subType = Lookup(subTypeName, _subTypesByName, n => new SubType { Game = _game, Name = n });
            subTypes.Add(new CardSubType { Card = card, SubType = subType });
        }
        associationsChanged |= Sync(card.CardSubTypes, subTypes, x => x.SubType);

        // Traits: Characteristics + ExtraText + Icons (front and back, excluding SWCCG set names from icons)
        var nonSetIcons = (face?.Icons ?? [])
            .Concat(record.Back?.Icons ?? [])
            .Where(icon => !string.IsNullOrWhiteSpace(icon) && !IsSetName(icon.Trim()));

        var allTraits = (face?.Characteristics ?? [])
            .Concat(record.Back?.Characteristics ?? [])
            .Concat(face?.ExtraText ?? [])
            .Concat(record.Back?.ExtraText ?? [])
            .Concat(nonSetIcons)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        associationsChanged |= Sync(card.CardTraits, allTraits.Select(name => new CardTrait
        {
            Card = card,
            Trait = Lookup(name, _traitsByName, n => new Trait { Game = _game, Name = n })
        }), x => x.Trait);

        // Aspects: Side ("Dark Side" or "Light Side") for facet compatibility
        var sideName = NormalizeSide(record.Side);
        var aspects = new List<CardAspect>();
        if (!string.IsNullOrWhiteSpace(sideName))
        {
            var aspect = Lookup(sideName, _aspectsByName, n => new Aspect
            {
                Game = _game,
                Name = n,
                Color = n.Contains("Dark", StringComparison.OrdinalIgnoreCase) ? "#dc2626" : "#2563eb"
            });
            aspects.Add(new CardAspect { Card = card, Aspect = aspect, Count = 1 });
        }
        associationsChanged |= Sync(card.CardAspects, aspects, x => x.Aspect);

        return associationsChanged;
    }

    internal Set GetOrAddSet(string? setCodeOrName)
    {
        string code = "UNK";
        string? name = null;
        DateOnly? releasedAt = null;
        string? legacyId = null;

        if (!string.IsNullOrWhiteSpace(setCodeOrName))
        {
            var key = setCodeOrName.Trim();
            if (_setMetaById.TryGetValue(key, out var meta)
                || _setMetaByAbbr.TryGetValue(key, out meta)
                || _setMetaByName.TryGetValue(key, out meta)
                || _setMetaByGempName.TryGetValue(key, out meta))
            {
                code = !string.IsNullOrWhiteSpace(meta.Abbr) ? meta.Abbr.Trim() : meta.Id.Trim();
                name = meta.Name.Trim();
                releasedAt = ParseReleaseDate(meta.DateRelease);
                legacyId = meta.Id.Trim();
            }
            else
            {
                code = key;
                name = key;
            }
        }

        if (!_setsByCode.TryGetValue(code, out var set))
        {
            // Check if a set was previously saved with the numeric/string id as Code
            if (!string.IsNullOrWhiteSpace(legacyId)
                && !string.Equals(legacyId, code, StringComparison.OrdinalIgnoreCase)
                && _setsByCode.TryGetValue(legacyId, out var legacySet))
            {
                _setsByCode.Remove(legacyId);
                legacySet.Code = code;
                MarkModified(legacySet, nameof(Set.Code));
                _setsByCode[code] = legacySet;
                set = legacySet;
            }
        }

        if (set is null)
        {
            set = new Set
            {
                Game = _game,
                Code = code,
                Name = name ?? code,
                ReleasedAt = releasedAt,
                LastSyncedAt = clock.GetUtcNow()
            };
            db.Sets.Add(set);
            _setsByCode[code] = set;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name) && set.Name != name)
            {
                set.Name = name;
                MarkModified(set, nameof(Set.Name));
            }
            if (releasedAt.HasValue && set.ReleasedAt != releasedAt)
            {
                set.ReleasedAt = releasedAt;
                MarkModified(set, nameof(Set.ReleasedAt));
            }
            var now = clock.GetUtcNow();
            if (set.LastSyncedAt != now)
            {
                set.LastSyncedAt = now;
                MarkModified(set, nameof(Set.LastSyncedAt));
            }
        }

        return set;
    }

    internal bool IsSetName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var trimmed = candidate.Trim();
        if (_setNames.Contains(trimmed))
            return true;

        return IsVirtualSetName(trimmed);
    }

    internal static bool IsVirtualSetName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var trimmed = candidate.Trim();
        if (trimmed.Equals("Virtual", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Virtual Set", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("Virtual Set", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Virtual Sets", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Virtual Defensive", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Virtual Premium", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Virtual Shield", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("Set ", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed[4..].Trim().TrimStart('#').Trim();
            if (int.TryParse(remainder, out _) || remainder.Length <= 2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes uniqueness indicators (dots such as • or * and diamonds such as &lt;&gt;) from the beginning of a card title,
    /// returning the cleaned title and any extracted uniqueness marker.
    /// </summary>
    internal static (string CleanTitle, string? ExtractedUniqueness) ParseTitleUniqueness(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (string.Empty, null);

        var trimmed = title.Trim();
        var span = trimmed.AsSpan();
        var dotCount = 0;
        var diamondCount = 0;

        while (!span.IsEmpty)
        {
            if (span.StartsWith("<>"))
            {
                diamondCount++;
                span = span[2..];
            }
            else if (span[0] is '•' or '*' or '·' or '●' or '∙')
            {
                dotCount++;
                span = span[1..];
            }
            else if (span[0] is '◇' or '◆' or '◊')
            {
                diamondCount++;
                span = span[1..];
            }
            else
            {
                break;
            }
        }

        string? extracted = null;
        if (diamondCount > 0)
        {
            extracted = string.Concat(Enumerable.Repeat("<>", diamondCount));
        }
        else if (dotCount > 0)
        {
            extracted = new string('*', dotCount);
        }

        span = span.TrimStart();
        var clean = span.IsEmpty ? trimmed : span.ToString();
        return (clean, extracted);
    }

    /// <summary>
    /// Removes uniqueness indicators (dots such as • or * and diamonds such as &lt;&gt;) from the beginning of a card title.
    /// </summary>
    internal static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var (clean, _) = ParseTitleUniqueness(title);
        return clean;
    }

    internal static DateOnly? ParseReleaseDate(string? dateRelease)
    {
        if (string.IsNullOrWhiteSpace(dateRelease))
            return null;

        var trimmed = dateRelease.Trim();
        if (trimmed == "0000-00-00")
            return null;

        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    internal Treatment GetOrAddTreatment(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Standard" : name.Trim();
        if (!_treatmentsByName.TryGetValue(name, out var treatment))
        {
            treatment = new Treatment { Game = _game, Name = name };
            db.Treatments.Add(treatment);
            _treatmentsByName[name] = treatment;
        }
        return treatment;
    }

    internal Rarity GetOrAddRarity(string? rawRarity)
    {
        var key = string.IsNullOrWhiteSpace(rawRarity) ? "Unknown" : rawRarity.Trim();
        string name;
        string? character;
        string? rarityType;

        if (RarityByCharacter.TryGetValue(key, out var def) || RarityByName.TryGetValue(key, out def))
        {
            name = def.Name;
            character = def.Character;
            rarityType = def.RarityType;
        }
        else
        {
            name = key;
            character = string.Equals(key, "Unknown", StringComparison.OrdinalIgnoreCase) ? null : key;
            rarityType = key;
        }

        if (!_raritiesByName.TryGetValue(name, out var rarity))
        {
            // Check if a rarity was previously saved with the abbreviation as its Name
            if (!string.IsNullOrWhiteSpace(character)
                && !string.Equals(character, name, StringComparison.OrdinalIgnoreCase)
                && _raritiesByName.TryGetValue(character, out var legacyRarity))
            {
                _raritiesByName.Remove(character);
                legacyRarity.Name = name;
                MarkModified(legacyRarity, nameof(Rarity.Name));
                _raritiesByName[name] = legacyRarity;
                rarity = legacyRarity;
            }
        }

        if (rarity is null)
        {
            rarity = new Rarity
            {
                Game = _game,
                Name = name,
                Character = character,
                RarityType = rarityType
            };
            db.Rarities.Add(rarity);
            _raritiesByName[name] = rarity;
        }
        else
        {
            if (rarity.Character != character)
            {
                rarity.Character = character;
                MarkModified(rarity, nameof(Rarity.Character));
            }
            if (rarity.RarityType != rarityType)
            {
                rarity.RarityType = rarityType;
                MarkModified(rarity, nameof(Rarity.RarityType));
            }
        }

        return rarity;
    }

    internal static string ResolveTreatmentName(SwccgCardRecord record)
    {
        var title = record.Front?.Title ?? "";
        if (title.Contains("(V)", StringComparison.OrdinalIgnoreCase))
            return "Virtual";
        if (title.Contains("(AI)", StringComparison.OrdinalIgnoreCase))
            return "Alternate Image";
        if (record.Rarity?.Contains("Foil", StringComparison.OrdinalIgnoreCase) == true)
            return "Foil";

        return "Standard";
    }

    internal static string? NormalizeSide(string? rawSide)
    {
        if (string.IsNullOrWhiteSpace(rawSide)) return null;
        var side = rawSide.Trim();
        if (side.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return "Dark Side";
        if (side.Equals("Light", StringComparison.OrdinalIgnoreCase)) return "Light Side";
        return side;
    }

    internal static string BuildRulesStyled(SwccgCardRecord record)
    {
        var sb = new StringBuilder();

        // 1. Rulings
        if (record.Rulings is { Count: > 0 })
        {
            sb.Append("<div class=\"swccg-rulings\"><h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground\">Rulings</h3><ul class=\"list-disc pl-4 space-y-1\">");
            foreach (var ruling in record.Rulings.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                sb.Append($"<li>{WebUtility.HtmlEncode(ruling.Trim())}</li>");
            }
            sb.Append("</ul></div>");
        }

        // 2. Pulled By
        if (record.PulledBy is { Count: > 0 })
        {
            sb.Append("<div class=\"swccg-pulled-by pt-2\"><h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground\">Pulled By</h3><ul class=\"list-disc pl-4 space-y-1\">");
            foreach (var item in record.PulledBy.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                sb.Append($"<li>{WebUtility.HtmlEncode(item.Trim())}</li>");
            }
            sb.Append("</ul></div>");
        }

        // 3. Pulls
        if (record.Pulls is { Count: > 0 })
        {
            sb.Append("<div class=\"swccg-pulls pt-2\"><h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground\">Pulls</h3><ul class=\"list-disc pl-4 space-y-1\">");
            foreach (var item in record.Pulls.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                sb.Append($"<li>{WebUtility.HtmlEncode(item.Trim())}</li>");
            }
            sb.Append("</ul></div>");
        }

        // 4. Combos
        if (record.Combo is { Count: > 0 })
        {
            sb.Append("<div class=\"swccg-combos pt-2\"><h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground\">Combos</h3><ul class=\"list-disc pl-4 space-y-1\">");
            foreach (var combo in record.Combo.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                sb.Append($"<li>{WebUtility.HtmlEncode(combo.Trim())}</li>");
            }
            sb.Append("</ul></div>");
        }

        // 5. Matching & Matching Weapons
        if (record.Matching is { Count: > 0 } || record.MatchingWeapon is { Count: > 0 })
        {
            sb.Append("<div class=\"swccg-matching pt-2\">");
            if (record.Matching is { Count: > 0 })
            {
                sb.Append("<h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground\">Matching</h3><ul class=\"list-disc pl-4 space-y-1\">");
                foreach (var item in record.Matching.Where(m => !string.IsNullOrWhiteSpace(m)))
                {
                    sb.Append($"<li>{WebUtility.HtmlEncode(item.Trim())}</li>");
                }
                sb.Append("</ul>");
            }
            if (record.MatchingWeapon is { Count: > 0 })
            {
                sb.Append("<h3 class=\"text-xs font-semibold uppercase tracking-wider text-muted-foreground mt-2\">Matching Weapon</h3><ul class=\"list-disc pl-4 space-y-1\">");
                foreach (var item in record.MatchingWeapon.Where(m => !string.IsNullOrWhiteSpace(m)))
                {
                    sb.Append($"<li>{WebUtility.HtmlEncode(item.Trim())}</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</div>");
        }

        // 6. Meta: Underlying Card For, Personas, Counterpart, Misc
        var metaItems = new List<string>();
        if (record.UnderlyingCardFor is { Count: > 0 })
        {
            metaItems.Add($"<strong>Underlying Card For:</strong> {WebUtility.HtmlEncode(string.Join(", ", record.UnderlyingCardFor))}");
        }
        if (record.Personas is { Count: > 0 })
        {
            metaItems.Add($"<strong>Personas:</strong> {WebUtility.HtmlEncode(string.Join(", ", record.Personas))}");
        }
        if (!string.IsNullOrWhiteSpace(record.Counterpart))
        {
            metaItems.Add($"<strong>Counterpart:</strong> {WebUtility.HtmlEncode(record.Counterpart.Trim())}");
        }
        if (!string.IsNullOrWhiteSpace(record.Misc))
        {
            metaItems.Add($"<strong>Misc:</strong> {WebUtility.HtmlEncode(record.Misc.Trim())}");
        }

        if (metaItems.Count > 0)
        {
            sb.Append("<div class=\"swccg-meta pt-2 space-y-1\">");
            foreach (var meta in metaItems)
            {
                sb.Append($"<p>{meta}</p>");
            }
            sb.Append("</div>");
        }

        return sb.ToString();
    }

    internal static string BuildExtraCardDataJson(SwccgCardRecord record)
    {
        var data = new Dictionary<string, object?>();

        if (record.PulledBy is { Count: > 0 }) data["pulledBy"] = record.PulledBy;
        if (record.Pulls is { Count: > 0 }) data["pulls"] = record.Pulls;
        if (record.Combo is { Count: > 0 }) data["combo"] = record.Combo;
        if (record.Matching is { Count: > 0 }) data["matching"] = record.Matching;
        if (record.MatchingWeapon is { Count: > 0 }) data["matchingWeapon"] = record.MatchingWeapon;
        if (record.UnderlyingCardFor is { Count: > 0 }) data["underlyingCardFor"] = record.UnderlyingCardFor;
        if (record.Personas is { Count: > 0 }) data["personas"] = record.Personas;
        if (record.Rulings is { Count: > 0 }) data["rulings"] = record.Rulings;
        if (!string.IsNullOrWhiteSpace(record.Misc)) data["misc"] = record.Misc;
        if (!string.IsNullOrWhiteSpace(record.Counterpart)) data["counterpart"] = record.Counterpart;
        if (record.Abbr is { Count: > 0 }) data["abbr"] = record.Abbr;
        if (record.Legacy == true) data["legacy"] = true;

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private T Lookup<T>(string name, Dictionary<string, T> cache, Func<string, T> create) where T : class
    {
        name = name.Trim();
        if (cache.TryGetValue(name, out var entity))
            return entity;
        entity = create(name);
        db.Set<T>().Add(entity);
        cache[name] = entity;
        return entity;
    }

    private static void MarkModified<TEntity>(TEntity entity, string propertyName) where TEntity : class
    {
        // ChangeTracker.DetectChanges() runs once before SaveChangesAsync in SwccgJsonImporter,
        // which automatically detects modified scalar properties on tracked entities.
        _ = entity;
        _ = propertyName;
    }

    private bool Sync<T, TKey>(ICollection<T> current, IEnumerable<T> desired, Func<T, TKey> key)
        where T : class where TKey : notnull
    {
        var changed = false;
        var wanted = desired.ToDictionary(key);
        foreach (var item in current.ToList())
        {
            if (wanted.Remove(key(item))) continue;
            current.Remove(item);
            db.Remove(item);
            changed = true;
        }
        foreach (var item in wanted.Values)
        {
            current.Add(item);
            db.Add(item);
            changed = true;
        }
        return changed;
    }
}
