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
            var trimmed = game.Trim();
            query = query.Where(uc => uc.CardPrinting.Card.Game == trimmed);
        }

        if (!string.IsNullOrWhiteSpace(set))
        {
            var trimmed = set.Trim();
            query = query.Where(uc => uc.CardPrinting.Set == trimmed);
        }

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var trimmed = rarity.Trim();
            query = query.Where(uc => uc.CardPrinting.Rarity == trimmed);
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
        IReadOnlyCollection<string> rarities)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            var pattern = $"%{term}%";
            query = query.Where(c =>
                EF.Functions.Like(EF.Functions.Collate(c.Name, "NOCASE"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(c.CardType, "NOCASE"), pattern));
        }

        if (games.Count > 0)
        {
            query = query.Where(c => games.Contains(c.Game));
        }

        if (sets.Count > 0)
        {
            query = query.Where(c => c.Printings.Any(p => sets.Contains(p.Set)));
        }

        if (rarities.Count > 0)
        {
            query = query.Where(c => c.Printings.Any(p => rarities.Contains(p.Rarity)));
        }

        return query;
    }

    internal static IQueryable<CardListItemResponse> SelectCardSummaries(
        this IQueryable<Card> query,
        string placeholderImage)
        => query.Select(c => new CardListItemResponse
        {
            CardId = c.Id,
            Game = c.Game,
            Name = c.Name,
            CardType = c.CardType,
            PrintingsCount = c.Printings.Count(),
            Primary = c.Printings
                .OrderByDescending(p => !string.IsNullOrEmpty(p.ImageUrl))
                .ThenByDescending(p => p.Style == "Standard")
                .ThenBy(p => p.Set)
                .ThenBy(p => p.Number)
                .ThenBy(p => p.Id)
                .Select(p => new CardListItemResponse.PrimaryPrintingResponse
                {
                    Id = p.Id,
                    Set = p.Set,
                    Number = p.Number,
                    Rarity = p.Rarity,
                    Style = p.Style,
                    ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? placeholderImage : p.ImageUrl
                })
                .FirstOrDefault()
        });
}
