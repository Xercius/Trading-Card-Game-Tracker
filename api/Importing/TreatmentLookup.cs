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

    internal static Set UpsertSet(AppDbContext db, Game? game, string code, DateOnly? releasedAt = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        code = code.Trim();
        if (db.Entry(game).State == EntityState.Detached)
            db.Games.Add(game);

        var set = db.Sets.Local.FirstOrDefault(candidate =>
                (candidate.Game == game || game.Id != 0 && candidate.GameId == game.Id)
                && candidate.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? (game.Id == 0 ? null : db.Sets.FirstOrDefault(candidate =>
                candidate.GameId == game.Id
                && EF.Functions.Collate(candidate.Code, "NOCASE") == code));
        if (set is null)
        {
            set = new Set { Game = game, Code = code };
            db.Sets.Add(set);
        }
        if (releasedAt is not null)
            set.ReleasedAt = releasedAt;
        return set;
    }

    internal static Rarity UpsertRarity(AppDbContext db, Game? game, string? name)
    {
        ArgumentNullException.ThrowIfNull(game);
        name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim();
        if (db.Entry(game).State == EntityState.Detached)
            db.Games.Add(game);

        var rarity = db.Rarities.Local.FirstOrDefault(candidate =>
                (candidate.Game == game || game.Id != 0 && candidate.GameId == game.Id)
                && candidate.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? (game.Id == 0 ? null : db.Rarities.FirstOrDefault(candidate =>
                candidate.GameId == game.Id
                && EF.Functions.Collate(candidate.Name, "NOCASE") == name));
        if (rarity is not null)
            return rarity;

        rarity = new Rarity { Game = game, Name = name };
        db.Rarities.Add(rarity);
        return rarity;
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
