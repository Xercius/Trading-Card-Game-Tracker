using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

namespace api.Importing;

/// <summary>Maintains the shared catalog's game-scoped lookups and card associations.</summary>
internal sealed class SwuCatalog(AppDbContext db, TimeProvider clock)
{
    private Game _game = null!;

    internal async Task LoadAsync(CancellationToken ct)
    {
        await db.Games.LoadAsync(ct);
        _game = db.Games.Local.SingleOrDefault(g => g.Name.Trim().Equals(
            "Star Wars Unlimited", StringComparison.OrdinalIgnoreCase)) ?? new Game { Name = "Star Wars Unlimited" };
        if (db.Entry(_game).State == EntityState.Detached) db.Games.Add(_game);
        await db.Sets.LoadAsync(ct);
        await db.Rarities.LoadAsync(ct);
        await db.CardTypes.LoadAsync(ct);
        await db.Traits.LoadAsync(ct);
        await db.Keywords.LoadAsync(ct);
        await db.Arenas.LoadAsync(ct);
        await db.Aspects.LoadAsync(ct);
        await db.Set<CardCardType>().LoadAsync(ct);
        await db.Set<CardTrait>().LoadAsync(ct);
        await db.Set<CardKeyword>().LoadAsync(ct);
        await db.Set<CardArena>().LoadAsync(ct);
        await db.Set<CardAspect>().LoadAsync(ct);
    }

    internal void Apply(Card card, CardPrinting printing, SwuCardAttributes attrs)
    {
        var expansion = attrs.Expansion?.Data?.Attributes;
        var set = db.Sets.Local.SingleOrDefault(s => s.Game == _game && s.Code == printing.Set);
        if (set is null)
        {
            set = new Set { Game = _game, Code = printing.Set };
            db.Sets.Add(set);
        }
        if (!string.IsNullOrWhiteSpace(expansion?.Name)) set.Name = expansion.Name.Trim();
        if (expansion?.ReleaseDate is { } releaseDate) set.ReleasedAt = releaseDate;
        set.LastSyncedAt = clock.GetUtcNow();
        printing.CatalogSet = set;
        printing.RarityLookup = Lookup(printing.Rarity, name => new Rarity { Game = _game, Name = name });

        var types = new List<CardCardType>();
        AddType(attrs.Type?.Data?.Attributes, "Type");
        AddType(attrs.Type2?.Data?.Attributes, "Type2");
        Sync(card.CardTypes, types, x => (x.CardType, x.SourceRole));
        Sync(card.CardTraits, Names(attrs.Traits).Select(name => new CardTrait
        {
            Card = card,
            Trait = Lookup(name, n => new Trait { Game = _game, Name = n })
        }), x => x.Trait);
        Sync(card.CardKeywords, Names(attrs.Keywords).Select(name => new CardKeyword
        {
            Card = card,
            Keyword = Lookup(name, n => new Keyword { Game = _game, Name = n })
        }), x => x.Keyword);
        Sync(card.CardArenas, Names(attrs.Arenas ?? (attrs.Arena is null ? [] : [attrs.Arena]))
            .Select(name => new CardArena
            {
                Card = card,
                Arena = Lookup(name, n => new Arena { Game = _game, Name = n })
            }), x => x.Arena);
        var aspects = (attrs.Aspects ?? []).Concat(attrs.AspectDuplicates ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new CardAspect
            {
                Card = card,
                Aspect = Lookup(group.Key, n => new Aspect { Game = _game, Name = n }),
                Count = group.Count()
            }).ToList();
        foreach (var aspect in aspects)
        {
            var existing = card.CardAspects.SingleOrDefault(x => x.Aspect == aspect.Aspect);
            if (existing is not null) existing.Count = aspect.Count;
        }
        Sync(card.CardAspects, aspects, x => x.Aspect);

        void AddType(SwuTypeAttributes? source, string role)
        {
            if (string.IsNullOrWhiteSpace(source?.Name)) return;
            var type = Lookup(source.Name, n => new CardType { Game = _game, Name = n });
            type.Value = source.Value;
            types.Add(new CardCardType { Card = card, CardType = type, SourceRole = role });
        }
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

    private void Sync<T, TKey>(ICollection<T> current, IEnumerable<T> desired, Func<T, TKey> key)
        where T : class where TKey : notnull
    {
        var wanted = desired.ToDictionary(key);
        foreach (var item in current.ToList())
        {
            if (wanted.Remove(key(item))) continue;
            current.Remove(item);
            db.Remove(item);
        }
        foreach (var item in wanted.Values)
        {
            current.Add(item);
            db.Add(item);
        }
    }

    private static IEnumerable<string> Names(string[]? names) => (names ?? [])
        .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
