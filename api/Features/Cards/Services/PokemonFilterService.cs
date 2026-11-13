using System.Linq.Expressions;
using api.Features.Cards.Dtos;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards.Services;

/// <summary>
/// Shared filtering and facet calculation logic for Pokémon TCG special card mechanics.
/// </summary>
public sealed class PokemonFilterService
{
    public static readonly IReadOnlyList<string> SupportedSpecialTypes =
    [
        "Pokémon V",
        "Pokémon EX / ex",
        "Mega Evolution",
        "Pokémon-GX",
        "Radiant",
        "Tera",
        "Ancient",
        "Future",
        "ACE SPEC",
    ];

    private static readonly Dictionary<string, Expression<Func<CardPrinting, bool>>> Predicates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pokémon V"] = p =>
                (p.Card.Stage != null &&
                 (EF.Functions.Collate(p.Card.Stage, "NOCASE") == "VMAX" ||
                  EF.Functions.Collate(p.Card.Stage, "NOCASE") == "VSTAR" ||
                  EF.Functions.Collate(p.Card.Stage, "NOCASE") == "V-UNION")) ||
                EF.Functions.Like(p.Card.Name, "% V") ||
                EF.Functions.Like(p.Card.Name, "% VMAX") ||
                EF.Functions.Like(p.Card.Name, "% VSTAR") ||
                EF.Functions.Like(p.Card.Name, "% V-UNION") ||
                (p.Card.ExtraCardDataJson != null &&
                 (EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"V\"%") ||
                  EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"VMAX\"%") ||
                  EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"VSTAR\"%") ||
                  EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"V-UNION\"%"))),

            ["Pokémon EX / ex"] = p =>
                EF.Functions.Like(p.Card.Name, "% ex") ||
                EF.Functions.Like(p.Card.Name, "%-EX") ||
                (p.Card.ExtraCardDataJson != null &&
                 EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"ex\"%")),

            ["Mega Evolution"] = p =>
                (p.Card.Stage != null && EF.Functions.Collate(p.Card.Stage, "NOCASE") == "MEGA") ||
                EF.Functions.Like(p.Card.Name, "M %-EX") ||
                EF.Functions.Like(p.Card.Name, "Mega %"),

            ["Pokémon-GX"] = p =>
                EF.Functions.Like(p.Card.Name, "%-GX") ||
                EF.Functions.Like(p.Card.Name, "% GX") ||
                (p.Card.ExtraCardDataJson != null &&
                 EF.Functions.Like(p.Card.ExtraCardDataJson, "%\"suffix\":\"GX\"%")),

            ["Radiant"] = p => EF.Functions.Like(p.Card.Name, "Radiant %"),

            ["Tera"] = p =>
                EF.Functions.Like(p.Card.Name, "%Tera%") ||
                (p.Card.Text != null && EF.Functions.Like(p.Card.Text, "%Tera%")),

            ["Ancient"] = p =>
                EF.Functions.Like(p.Card.Name, "%Ancient%") ||
                (p.Card.Text != null && EF.Functions.Like(p.Card.Text, "%Ancient%")) ||
                (p.Card.ExtraCardDataJson != null &&
                 EF.Functions.Like(p.Card.ExtraCardDataJson, "%Ancient%")),

            ["Future"] = p =>
                EF.Functions.Like(p.Card.Name, "%Future%") ||
                (p.Card.Text != null && EF.Functions.Like(p.Card.Text, "%Future%")) ||
                (p.Card.ExtraCardDataJson != null &&
                 EF.Functions.Like(p.Card.ExtraCardDataJson, "%Future%")),

            ["ACE SPEC"] = p =>
                (p.RarityLookup != null && EF.Functions.Collate(p.RarityLookup.Name, "NOCASE") == "Ace Spec Rare") ||
                (p.Card.Text != null && EF.Functions.Like(p.Card.Text, "%ACE SPEC%")),
        };

    /// <summary>
    /// Filters printings to those matching any of the selected special Pokémon types (OR semantics).
    /// </summary>
    public IQueryable<CardPrinting> ApplySpecialTypeFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyList<string> specialTypes)
    {
        if (specialTypes.Count == 0)
        {
            return query;
        }

        Expression<Func<CardPrinting, bool>>? combined = null;
        foreach (var raw in specialTypes)
        {
            var key = raw.Trim();
            if (!Predicates.TryGetValue(key, out var predicate))
            {
                continue;
            }

            combined = combined is null ? predicate : CombineOr(combined, predicate);
        }

        return combined is null ? query.Where(_ => false) : query.Where(combined);
    }

    /// <summary>
    /// Computes facet counts for each supported special Pokémon type that has matching printings.
    /// </summary>
    public async Task<IReadOnlyList<FacetOption>> ComputeSpecialTypeFacetsAsync(
        IQueryable<CardPrinting> query,
        CancellationToken ct)
    {
        var results = new List<FacetOption>();
        foreach (var label in SupportedSpecialTypes)
        {
            if (!Predicates.TryGetValue(label, out var predicate))
            {
                continue;
            }

            var count = await query.CountAsync(predicate, ct);
            if (count > 0)
            {
                results.Add(new FacetOption { Value = label, Label = label, Count = count });
            }
        }

        return results;
    }

    private static Expression<Func<CardPrinting, bool>> CombineOr(
        Expression<Func<CardPrinting, bool>> left,
        Expression<Func<CardPrinting, bool>> right)
    {
        var param = left.Parameters[0];
        var replacedRight = new ParameterReplacer(right.Parameters[0], param).Visit(right.Body);
        return Expression.Lambda<Func<CardPrinting, bool>>(Expression.OrElse(left.Body, replacedRight), param);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}
