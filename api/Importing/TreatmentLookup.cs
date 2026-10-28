using api.Data;
using api.Models;

namespace api.Importing;

internal static class TreatmentLookup
{
    internal static Treatment Upsert(AppDbContext db, string gameName, string? name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Standard" : name.Trim();
        var normalizedName = name.ToLowerInvariant();
        var normalizedGame = gameName.Trim().ToLowerInvariant();
        var game = db.Games.Local.FirstOrDefault(candidate =>
                candidate.Name.Trim().Equals(normalizedGame, StringComparison.OrdinalIgnoreCase))
            ?? db.Games.FirstOrDefault(candidate => candidate.NameNormalized == normalizedGame)
            ?? new Game { Name = gameName.Trim() };
        if (db.Entry(game).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
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
}
