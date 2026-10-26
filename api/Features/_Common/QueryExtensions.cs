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
                EF.Property<string>(uc.CardPrinting.Card, "GameNorm") == normalized);
        }

        if (!string.IsNullOrWhiteSpace(set))
        {
            var normalized = SqliteCaseNormalizer.Normalize(set);
            query = query.Where(uc =>
                EF.Property<string>(uc.CardPrinting, "SetNorm") == normalized);
        }

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var normalized = SqliteCaseNormalizer.Normalize(rarity);
            query = query.Where(uc =>
                EF.Property<string>(uc.CardPrinting, "RarityNorm") == normalized);
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
        IReadOnlyCollection<int> gameIds)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            var pattern = $"%{term}%";
            query = query.Where(c =>
                EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(c.CardType, "NOCASE"), pattern) ||
                c.CardTypes.Any(type => EF.Functions.Like(
                    EF.Functions.Collate(type.CardType.Name, "NOCASE"), pattern)));
        }

        if (games.Count > 0)
        {
            var normalizedGames = games
                .Select(g => g is null ? null : SqliteCaseNormalizer.Normalize(g))
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();
            var includesSwu = normalizedGames.Remove("star wars unlimited");
            query = query.Where(c =>
                normalizedGames.Contains(EF.Property<string>(c, "GameNorm")) ||
                (includesSwu && (
                    c.CardTypes.Any(type => gameIds.Contains(type.CardType.GameId)) ||
                    c.Printings.Any(printing => printing.CatalogSet != null &&
                        gameIds.Contains(printing.CatalogSet.GameId)) ||
                    (!c.CardTypes.Any() && !c.Printings.Any(printing => printing.CatalogSet != null) &&
                        EF.Property<string>(c, "GameNorm") == "star wars unlimited"))));
        }

        if (sets.Count > 0)
        {
            var normalizedSets = sets
                .Select(s => s is null ? null : SqliteCaseNormalizer.Normalize(s))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            query = query.Where(c =>
                c.Printings.Any(p => p.CatalogSet != null
                    ? normalizedSets.Contains(EF.Functions.Collate(p.CatalogSet.Code, "NOCASE")) ||
                      (p.CatalogSet.Name != null && normalizedSets.Contains(
                          EF.Functions.Collate(p.CatalogSet.Name, "NOCASE")))
                    : normalizedSets.Contains(EF.Property<string>(p, "SetNorm"))));
        }

        if (rarities.Count > 0)
        {
            var normalizedRarities = rarities
                .Select(r => r is null ? null : SqliteCaseNormalizer.Normalize(r))
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();
            query = query.Where(c =>
                c.Printings.Any(p => p.RarityLookup != null
                    ? normalizedRarities.Contains(EF.Functions.Collate(p.RarityLookup.Name, "NOCASE"))
                    : normalizedRarities.Contains(EF.Property<string>(p, "RarityNorm"))));
        }

        if (types.Count > 0)
        {
            var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
            query = query.Where(c => c.CardTypes.Any(type =>
                normalizedTypes.Contains(EF.Functions.Collate(type.CardType.Name, "NOCASE"))));
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
                Game = c.Game,
                Name = c.Name,
                CardType = c.CardType,
                PrintingsCount = c.Printings.Count(),
                Primary = c.Printings
                    .OrderByDescending(p => !string.IsNullOrEmpty(p.ImageUrl))
                    .ThenByDescending(p => p.Style == "Standard")
                    .ThenBy(p => p.CatalogSet == null
                        ? p.Set
                        : p.CatalogSet.Name ?? p.CatalogSet.Code)
                    .ThenBy(p => p.Number)
                    .ThenBy(p => p.Id)
                    .Select(p => new CardListItemResponse.PrimaryPrintingResponse
                    {
                        Id = p.Id,
                        Set = p.CatalogSet == null
                            ? p.Set
                            : p.CatalogSet.Name ?? p.CatalogSet.Code,
                        SetCode = p.CatalogSet == null ? null : p.CatalogSet.Code,
                        Number = p.Number,
                        Rarity = p.RarityLookup == null ? p.Rarity : p.RarityLookup.Name,
                        Style = p.Style,
                        ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? placeholderImage : p.ImageUrl,
                        ReleaseDate = p.CatalogSet == null ? p.ReleasedAt : p.CatalogSet.ReleasedAt
                    })
                    .FirstOrDefault()
            });

}
