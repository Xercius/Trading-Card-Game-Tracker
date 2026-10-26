using api.Data;
using api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace api.Tests.Data;

public sealed class AppDbContextSwuConfigurationTests
{
    [Fact]
    public void OnModelCreating_ConfiguresSharedCatalogEntities_WithExpectedTablesAndIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new AppDbContext(options);

        var game = db.Model.FindEntityType(typeof(Game));
        var set = db.Model.FindEntityType(typeof(Set));
        var swuCard = db.Model.FindEntityType(typeof(SwuCard));
        var swuCardPrinting = db.Model.FindEntityType(typeof(SwuCardPrinting));
        var syncLog = db.Model.FindEntityType(typeof(SyncLog));

        Assert.NotNull(game);
        Assert.NotNull(set);
        Assert.NotNull(swuCard);
        Assert.NotNull(swuCardPrinting);
        Assert.NotNull(syncLog);

        Assert.Equal("Games", game!.GetTableName());
        Assert.Equal("Sets", set!.GetTableName());
        Assert.Equal("SwuCards", swuCard!.GetTableName());
        Assert.Equal("SwuCardPrintings", swuCardPrinting!.GetTableName());
        Assert.Equal("SyncLogs", syncLog!.GetTableName());
        Assert.Contains(game.GetIndexes(), i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Game.NameNormalized)));
        Assert.Contains(set.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Set.GameId), nameof(Set.Code) }));

        var cardUidIndex = swuCard.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SwuCard.CardUid) }));

        Assert.True(cardUidIndex.IsUnique);
        Assert.Equal("\"CardUid\" IS NOT NULL", cardUidIndex.GetFilter());
    }

    [Fact]
    public async Task Migration_PreservesSwuSetsAndTheirSyncLogReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261025084027_AddCardPrintingReleaseDate");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "SwuSets" ("Id", "Code", "Name", "ReleasedAt", "LastSyncedAt")
            VALUES (41, 'SOR', 'Spark of Rebellion', NULL, '2026-01-01T00:00:00+00:00');
            INSERT INTO "SyncLogs" ("Id", "SwuSetId", "StartedAt", "IsIncremental", "CardsReturned", "CardsUpserted", "Status")
            VALUES (5, 41, '2026-01-01T00:00:00+00:00', 0, 1, 1, 'Succeeded');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType")
            VALUES (3, 'Star Wars Unlimited', 'Luke Skywalker', 'Unit');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType")
            VALUES (4, 'Pokémon', 'Pikachu', 'Pokémon');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (7, 3, 'sor', 'SOR-001', 'Common', 'Standard');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (8, 4, 'sor', 'SVP-001', 'Promo', 'Standard');
            """);

        await migrator.MigrateAsync();

        var game = await db.Games.SingleAsync();
        var set = await db.Sets.SingleAsync();
        var syncLog = await db.SyncLogs.Include(log => log.Set).SingleAsync();
        var printing = await db.CardPrintings.SingleAsync(item => item.Id == 7);
        var otherGamePrinting = await db.CardPrintings.SingleAsync(item => item.Id == 8);
        Assert.Equal("Star Wars Unlimited", game.Name);
        Assert.Equal("star wars unlimited", game.NameNormalized);
        Assert.Equal(41, set.Id);
        Assert.Equal(game.Id, set.GameId);
        Assert.Equal("SOR", set.Code);
        Assert.Equal(set.Id, syncLog.SetId);
        Assert.Equal(set.Id, syncLog.Set!.Id);
        Assert.Equal(set.Id, printing.SetId);
        Assert.Null(otherGamePrinting.SetId);
    }

    [Fact]
    public async Task Database_EnforcesLookupAndAssociationConstraints_AndStoresAspectMultiplicity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var game = await db.Games.SingleAsync();
        db.Games.Add(new Game { Name = " star wars unlimited " });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        game = await db.Games.SingleAsync();
        var card = new Card { Game = game.Name, Name = "Test card", CardType = "Unit" };
        var type = new CardType { GameId = game.Id, Name = "Unit", Value = "unit" };
        var aspect = new Aspect { GameId = game.Id, Name = "Heroism", Color = "#4A90E2" };
        db.AddRange(card, type, aspect);
        await db.SaveChangesAsync();

        db.CardCardTypes.AddRange(
            new CardCardType { Card = card, CardType = type, SourceRole = "Type" },
            new CardCardType { Card = card, CardType = type, SourceRole = "Type2" });
        db.CardAspects.Add(new CardAspect { Card = card, Aspect = aspect, Count = 2 });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.CardCardTypes.CountAsync());
        Assert.Equal(2, (await db.CardAspects.SingleAsync()).Count);
        Assert.Equal("#4A90E2", (await db.Aspects.SingleAsync()).Color);
        Assert.Equal("unit", (await db.CardTypes.SingleAsync()).Value);

        var trait = new Trait { GameId = game.Id, Name = "Rebel" };
        db.CardTraits.Add(new CardTrait { Card = card, Trait = trait });
        await db.SaveChangesAsync();
        var cardId = card.Id;
        var aspectId = aspect.Id;
        var traitId = trait.Id;
        db.ChangeTracker.Clear();
        db.CardTraits.Add(new CardTrait { CardId = cardId, TraitId = traitId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.CardTypes.Add(new CardType { GameId = game.Id, Name = "Unit" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        var secondCard = new Card { Game = game.Name, Name = "Second card", CardType = "Unit" };
        db.Cards.Add(secondCard);
        await db.SaveChangesAsync();
        db.CardAspects.Add(new CardAspect { CardId = secondCard.Id, AspectId = aspectId, Count = 0 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
