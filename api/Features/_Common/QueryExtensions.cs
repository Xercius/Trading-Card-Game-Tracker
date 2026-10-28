using api.Features.Cards.Dtos;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Features._Common;

/// <summary>
/// Shared query helpers for card-centric controllers.
/// </summary>
internal static class QueryExtensions
{
    internal static IQueryable<UserCard> FilterByPrintingMetadata(
        this IQueryable<UserCard> query,
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        bool useCaseInsensitiveName)
    {
        if (!string.IsNullOrWhiteSpace(game))
        {
            var normalized = SqliteCaseNormalizer.Normalize(game);
            query = query.Where(uc =>
                uc.CardPrinting.Card.Game != null
                && uc.CardPrinting.Card.Game.NameNormalized == normalized);
        }

        if (!string.IsNullOrWhiteSpace(set))
        {
            var value = set.Trim();
            query = query.Where(uc =>
                uc.CardPrinting.CatalogSet != null
                && (EF.Functions.Collate(uc.CardPrinting.CatalogSet.Code, "NOCASE") == value
                    || uc.CardPrinting.CatalogSet.Name != null
                    && EF.Functions.Collate(uc.CardPrinting.CatalogSet.Name, "NOCASE") == value));
        }

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var value = rarity.Trim();
            query = query.Where(uc =>
                uc.CardPrinting.RarityLookup != null
                && EF.Functions.Collate(uc.CardPrinting.RarityLookup.Name, "NOCASE") == value);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{name.Trim()}%";
            query = useCaseInsensitiveName
                ? query.Where(uc => EF.Functions.Like(
                    EF.Functions.Collate(uc.CardPrinting.Card.Name, "NOCASE"),
                    pattern))
                : query.Where(uc => EF.Functions.Like(uc.CardPrinting.Card.Name, pattern));
        }

        if (cardPrintingId.HasValue)
        {
            query = query.Where(uc => uc.CardPrintingId == cardPrintingId.Value);
        }

        return query;
    }

    internal static IOrderedQueryable<UserCard> OrderByCardNameAndPrinting(this IQueryable<UserCard> query)
        => query.OrderBy(uc => uc.CardPrinting.Card.Name).ThenBy(uc => uc.CardPrintingId);

    internal static IQueryable<Card> ApplyCardSearchFilters(
        this IQueryable<Card> query,
        string? searchTerm,
        IReadOnlyCollection<string> games,
        IReadOnlyCollection<string> sets,
        IReadOnlyCollection<string> rarities,
        IReadOnlyCollection<string> types,
        IReadOnlyCollection<int> gameIds,
        IReadOnlyCollection<string>? arenas = null,
        IReadOnlyCollection<string>? aspects = null,
        IReadOnlyCollection<string>? traits = null)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            var pattern = $"%{term}%";
            query = query.Where(c =>
                EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern) ||
                c.CardTypes.Any(type => EF.Functions.Like(
                    EF.Functions.Collate(type.CardType.Name, "NOCASE"), pattern)));
        }

        if (games.Count > 0)
        {
            var normalizedGames = games
                .Select(g => g is null ? null : SqliteCaseNormalizer.Normalize(g))
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();
            query = query.Where(c =>
                c.Game != null && normalizedGames.Contains(c.Game.NameNormalized)
                || c.GameId.HasValue && gameIds.Contains(c.GameId.Value));
        }

        if (sets.Count > 0)
        {
            var normalizedSets = sets
                .Select(s => s is null ? null : SqliteCaseNormalizer.Normalize(s))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            query = query.Where(c =>
                c.Printings.Any(p => p.CatalogSet != null
                    && (normalizedSets.Contains(EF.Functions.Collate(p.CatalogSet.Code, "NOCASE")) ||
                        p.CatalogSet.Name != null && normalizedSets.Contains(
                            EF.Functions.Collate(p.CatalogSet.Name, "NOCASE")))));
        }

        if (rarities.Count > 0)
        {
            var normalizedRarities = rarities
                .Select(r => r is null ? null : SqliteCaseNormalizer.Normalize(r))
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();
            query = query.Where(c =>
                c.Printings.Any(p => p.RarityLookup != null
                    && normalizedRarities.Contains(EF.Functions.Collate(p.RarityLookup.Name, "NOCASE"))));
        }

        if (types.Count > 0)
        {
            var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(c =>
                c.CardTypes.Any(type =>
                    normalizedTypes.Contains(EF.Functions.Collate(type.CardType.Name, "NOCASE"))));
        }

        if (arenas is { Count: > 0 })
        {
            var normalizedArenas = arenas.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(c =>
                c.CardArenas.Any(arena =>
                    normalizedArenas.Contains(EF.Functions.Collate(arena.Arena.Name, "NOCASE"))));
        }

        if (aspects is { Count: > 0 })
        {
            var normalizedAspects = aspects
                .Select(SqliteCaseNormalizer.Normalize)
                .Where(a => a != "all")
                .ToList();
            if (normalizedAspects.Count > 0)
            {
                query = query.Where(c => c.CardAspects.Any(aspect =>
                    normalizedAspects.Contains(EF.Functions.Collate(aspect.Aspect.Name, "NOCASE"))));
            }
        }

        if (traits is { Count: > 0 })
        {
            var normalizedTraits = traits.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(c => c.CardTraits.Any(trait =>
                normalizedTraits.Contains(EF.Functions.Collate(trait.Trait.Name, "NOCASE"))));
        }

        return query;
    }

    internal static IQueryable<CardListItemResponse> SelectCardSummaries(
        this IQueryable<Card> query,
        string placeholderImage)
        => query.Select(c =>
            new CardListItemResponse
            {
                CardId = c.Id,
                Game = c.Game == null ? string.Empty : c.Game.Name,
                Name = c.Name,
                CardType = c.CardTypes
                    .Where(type => type.SourceRole == "Type")
                    .Select(type => type.CardType.Name)
                    .FirstOrDefault() ?? string.Empty,
                PrintingsCount = c.Printings.Count(),
                Primary = c.Printings
                    .OrderByDescending(p => !string.IsNullOrEmpty(p.ImageUrl))
                    .ThenByDescending(p => p.Treatment != null
                        && p.Treatment.NameNormalized == "standard")
                    .ThenBy(p => p.CatalogSet == null ? null : p.CatalogSet.Name ?? p.CatalogSet.Code)
                    .ThenBy(p => p.Number)
                    .ThenBy(p => p.Id)
                    .Select(p => new CardListItemResponse.PrimaryPrintingResponse
                    {
                        Id = p.Id,
                        Set = p.CatalogSet == null ? string.Empty : p.CatalogSet.Name ?? p.CatalogSet.Code,
                        SetCode = p.CatalogSet == null ? null : p.CatalogSet.Code,
                        Number = p.Number,
                        Rarity = p.RarityLookup == null ? string.Empty : p.RarityLookup.Name,
                        Style = p.Treatment == null ? "Standard" : p.Treatment.Name,
                        ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? placeholderImage : p.ImageUrl,
                        ReleaseDate = p.CatalogSet == null ? null : p.CatalogSet.ReleasedAt
                    })
                    .FirstOrDefault()
            });

}
