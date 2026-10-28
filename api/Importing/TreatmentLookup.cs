using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing;

internal static class TreatmentLookup
{
    internal static Treatment Upsert(AppDbContext db, string gameName, string? name)
        => Upsert(db, GetOrAddGame(db, gameName), name);

    internal static Treatment Upsert(AppDbContext db, Game? game, string? name)
    {
        ArgumentNullException.ThrowIfNull(game);
        name = string.IsNullOrWhiteSpace(name) ? "Standard" : name.Trim();
        var normalizedName = name.ToLowerInvariant();
        if (db.Entry(game).State == EntityState.Detached)
            db.Games.Add(game);

        var treatment = db.Treatments.Local.FirstOrDefault(candidate =>
            (candidate.Game == game || (game.Id != 0 && candidate.GameId == game.Id))
            && candidate.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        if (treatment is null && game.Id != 0)
        {
            treatment = db.Treatments.FirstOrDefault(candidate =>
                candidate.GameId == game.Id && candidate.NameNormalized == normalizedName);
        }
        if (treatment is not null)
            return treatment;

        treatment = new Treatment { Game = game, Name = name };
        db.Treatments.Add(treatment);
        return treatment;
    }

    internal static Game GetOrAddGame(AppDbContext db, string gameName)
    {
        var normalizedGame = gameName.Trim().ToLowerInvariant();
        var game = db.Games.Local.FirstOrDefault(candidate =>
                candidate.Name.Trim().Equals(gameName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? db.Games.FirstOrDefault(candidate => candidate.NameNormalized == normalizedGame)
            ?? new Game { Name = gameName.Trim() };
        if (db.Entry(game).State == EntityState.Detached)
            db.Games.Add(game);
        return game;
    }

    internal static void UpsertCardType(AppDbContext db, Card card, string? name, string role = "Type")
    {
        var existing = db.CardCardTypes.Local.FirstOrDefault(association =>
                association.Card == card && association.SourceRole == role)
            ?? (card.Id == 0 ? null : db.CardCardTypes.FirstOrDefault(association =>
                association.CardId == card.Id && association.SourceRole == role));
        if (string.IsNullOrWhiteSpace(name))
        {
            if (existing is not null)
                db.CardCardTypes.Remove(existing);
            return;
        }

        var game = card.Game ?? (card.GameId.HasValue
            ? db.Games.Local.FirstOrDefault(candidate => candidate.Id == card.GameId.Value)
                ?? db.Games.Find(card.GameId.Value)
            : null);
        if (game is null)
            return;

        name = name.Trim();
        var cardType = db.CardTypes.Local.FirstOrDefault(candidate =>
                (candidate.Game == game || game.Id != 0 && candidate.GameId == game.Id)
                && candidate.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? (game.Id == 0 ? null : db.CardTypes.FirstOrDefault(candidate =>
                candidate.GameId == game.Id && candidate.Name == name))
            ?? new CardType { Game = game, Name = name };
        if (db.Entry(cardType).State == EntityState.Detached)
            db.CardTypes.Add(cardType);

        if (existing is not null
            && (existing.CardType == cardType || cardType.Id != 0 && existing.CardTypeId == cardType.Id))
            return;
        if (existing is not null)
            db.CardCardTypes.Remove(existing);
        db.CardCardTypes.Add(new CardCardType
        {
            Card = card,
            CardType = cardType,
            SourceRole = role
        });
    }
}
