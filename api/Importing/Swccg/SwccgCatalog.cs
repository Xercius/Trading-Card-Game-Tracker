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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    internal Game Game => _game;

    internal void InitializeSetMetadata(IEnumerable<SwccgSet> sets)
    {
        foreach (var s in sets)
        {
            if (!string.IsNullOrWhiteSpace(s.Id))
                _setMetaById[s.Id.Trim()] = s;
            if (!string.IsNullOrWhiteSpace(s.Name))
                _setMetaByName[s.Name.Trim()] = s;
        }
    }

    internal async Task LoadAsync(CancellationToken ct)
    {
        await db.Games.LoadAsync(ct);
        _game = db.Games.Local.SingleOrDefault(g => g.Name.Trim().Equals(
            GameName, StringComparison.OrdinalIgnoreCase)) ?? new Game { Name = GameName };
        if (db.Entry(_game).State == EntityState.Detached)
            db.Games.Add(_game);

        await db.Sets.Where(s => s.Game == _game || s.GameId == _game.Id).LoadAsync(ct);
        await db.Treatments.Where(t => t.Game == _game || t.GameId == _game.Id).LoadAsync(ct);
        await db.Rarities.Where(r => r.Game == _game || r.GameId == _game.Id).LoadAsync(ct);
        await db.CardTypes.Where(ctp => ctp.Game == _game || ctp.GameId == _game.Id).LoadAsync(ct);
        await db.Traits.Where(tr => tr.Game == _game || tr.GameId == _game.Id).LoadAsync(ct);
        await db.Keywords.Where(kw => kw.Game == _game || kw.GameId == _game.Id).LoadAsync(ct);
        await db.Aspects.Where(a => a.Game == _game || a.GameId == _game.Id).LoadAsync(ct);
        await db.Set<CardCardType>().LoadAsync(ct);
        await db.Set<CardTrait>().LoadAsync(ct);
        await db.Set<CardKeyword>().LoadAsync(ct);
        await db.Set<CardAspect>().LoadAsync(ct);
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
        db.Entry(printing).Reference(p => p.CatalogSet).CurrentValue = set;

        // Treatment (Standard, Foil, Virtual, Alternate Image)
        var treatmentName = ResolveTreatmentName(record);
        db.Entry(printing).Reference(p => p.Treatment).CurrentValue = GetOrAddTreatment(treatmentName);

        // Rarity
        var rarityName = string.IsNullOrWhiteSpace(record.Rarity) ? "Unknown" : record.Rarity.Trim();
        var rarity = Lookup(rarityName, n => new Rarity
        {
            Game = _game,
            Name = n,
            Character = n.Length > 0 ? n[..1].ToUpperInvariant() : null
        });
        db.Entry(printing).Reference(p => p.RarityLookup).CurrentValue = rarity;

        if (!includeCardAssociations)
            return false;

        // Card Types: Primary Type and SubType
        var types = new List<CardCardType>();
        if (!string.IsNullOrWhiteSpace(face?.Type))
        {
            var primaryType = Lookup(face.Type.Trim(), n => new CardType { Game = _game, Name = n });
            types.Add(new CardCardType { Card = card, CardType = primaryType, SourceRole = "Type" });
        }
        if (!string.IsNullOrWhiteSpace(face?.SubType))
        {
            var subType = Lookup(face.SubType.Trim(), n => new CardType { Game = _game, Name = n });
            types.Add(new CardCardType { Card = card, CardType = subType, SourceRole = "Type2" });
        }
        associationsChanged |= Sync(card.CardTypes, types, x => (x.CardType, x.SourceRole));

        // Traits: Characteristics + ExtraText
        var allTraits = (face?.Characteristics ?? [])
            .Concat(face?.ExtraText ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        associationsChanged |= Sync(card.CardTraits, allTraits.Select(name => new CardTrait
        {
            Card = card,
            Trait = Lookup(name, n => new Trait { Game = _game, Name = n })
        }), x => x.Trait);

        // Keywords: Icons
        var allIcons = (face?.Icons ?? [])
            .Concat(record.Back?.Icons ?? [])
            .Where(icon => !string.IsNullOrWhiteSpace(icon))
            .Select(icon => icon.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        associationsChanged |= Sync(card.CardKeywords, allIcons.Select(name => new CardKeyword
        {
            Card = card,
            Keyword = Lookup(name, n => new Keyword { Game = _game, Name = n })
        }), x => x.Keyword);

        // Aspects: Side ("Dark Side" or "Light Side") for facet compatibility
        var sideName = NormalizeSide(record.Side);
        var aspects = new List<CardAspect>();
        if (!string.IsNullOrWhiteSpace(sideName))
        {
            var aspect = Lookup(sideName, n => new Aspect
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

        if (!string.IsNullOrWhiteSpace(setCodeOrName))
        {
            var key = setCodeOrName.Trim();
            if (_setMetaById.TryGetValue(key, out var metaById))
            {
                code = metaById.Id;
                name = metaById.Name;
            }
            else if (_setMetaByName.TryGetValue(key, out var metaByName))
            {
                code = metaByName.Id;
                name = metaByName.Name;
            }
            else
            {
                code = key;
                name = key;
            }
        }

        var set = db.Sets.Local.FirstOrDefault(candidate =>
            (candidate.Game == _game || candidate.GameId == _game.Id)
            && string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));

        if (set is null)
        {
            set = new Set
            {
                Game = _game,
                Code = code,
                Name = name ?? code,
                LastSyncedAt = clock.GetUtcNow()
            };
            db.Sets.Add(set);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name) && set.Name != name)
            {
                set.Name = name;
                MarkModified(set, nameof(Set.Name));
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

    internal Treatment GetOrAddTreatment(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Standard" : name.Trim();
        var treatment = db.Treatments.Local.FirstOrDefault(candidate =>
            (candidate.Game == _game || candidate.GameId == _game.Id)
            && string.Equals(candidate.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (treatment is null)
        {
            treatment = new Treatment { Game = _game, Name = name };
            db.Treatments.Add(treatment);
        }
        return treatment;
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

    private T Lookup<T>(string name, Func<string, T> create) where T : class
    {
        name = name.Trim();
        var entity = db.Set<T>().Local.FirstOrDefault(item =>
            db.Entry(item).Reference("Game").CurrentValue == _game
            && string.Equals(db.Entry(item).Property<string>("Name").CurrentValue?.Trim(),
                name, StringComparison.OrdinalIgnoreCase));
        if (entity is not null) return entity;
        entity = create(name);
        db.Set<T>().Add(entity);
        return entity;
    }

    private void MarkModified<TEntity>(TEntity entity, string propertyName) where TEntity : class
    {
        if (db.Entry(entity).State != EntityState.Added)
            db.Entry(entity).Property(propertyName).IsModified = true;
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
