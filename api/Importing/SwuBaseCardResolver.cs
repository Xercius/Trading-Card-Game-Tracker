using Microsoft.EntityFrameworkCore;

using api.Data;
using api.Models;

namespace api.Importing;

internal static class SwuBaseCardResolver
{
    /// <summary>
    /// Resolves durable upstream references across all staging sets without manufacturing canonical cards.
    /// </summary>
    public static async Task<IReadOnlyList<SwuCard>> ResolveAsync(AppDbContext db, CancellationToken ct)
    {
        var candidates = await db.SwuCards
            .Where(card => card.BaseStrapiId != null)
            .ToListAsync(ct);
        var targetIds = candidates.Where(card => card.BaseStrapiId.HasValue)
            .Select(card => card.BaseStrapiId!.Value).Distinct().ToArray();
        var targets = await db.SwuCards
            .Where(card => targetIds.Contains(card.StrapiId))
            .ToDictionaryAsync(card => card.StrapiId, ct);
        var changed = new List<SwuCard>();
        foreach (var card in candidates)
        {
            var target = card.BaseStrapiId.HasValue ? targets.GetValueOrDefault(card.BaseStrapiId.Value) : null;
            var targetId = target is not null && target.Id != card.Id ? target.Id : (int?)null;
            if (card.BaseCardId == targetId)
            {
                continue;
            }

            card.BaseCardId = targetId;
            changed.Add(card);
        }

        return changed;
    }
}
