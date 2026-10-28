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
        var card = db.Model.FindEntityType(typeof(Card));
        var cardPrinting = db.Model.FindEntityType(typeof(CardPrinting));
        var syncLog = db.Model.FindEntityType(typeof(SyncLog));

        Assert.NotNull(game);
        Assert.NotNull(set);
        Assert.NotNull(card);
        Assert.NotNull(cardPrinting);
        Assert.NotNull(syncLog);

        Assert.Equal("Games", game!.GetTableName());
        Assert.Equal("Sets", set!.GetTableName());
        Assert.Equal("Cards", card!.GetTableName());
        Assert.Equal("CardPrintings", cardPrinting!.GetTableName());
        Assert.Equal("SyncLogs", syncLog!.GetTableName());
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() is "SwuCards" or "SwuCardPrintings");
        Assert.NotNull(card.FindProperty(nameof(Card.GameId)));
        Assert.DoesNotContain(card.GetProperties(), property =>
            new[]
            {
                "Game", "GameNorm", "CardType", "CardType2", "DetailsJson", "Arena", "Aspects",
                "Traits", "Keywords", "ApiCreatedAt", "ApiUpdatedAt", "BaseCardId",
                "BaseStrapiId", "StrapiId"
            }.Contains(property.Name));
        Assert.DoesNotContain(card.GetIndexes(), i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Card.CardUid)));
        Assert.Contains(game.GetIndexes(), i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Game.NameNormalized)));
        Assert.Contains(set.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Set.GameId), nameof(Set.Code) }));
    }

    [Fact]
    public async Task Migration_PreservesSwuSetsAndTheirSyncLogReferences_AndRemovesUnusedSwuTables()
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
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType", "Description")
            VALUES (3, 'Star Wars Unlimited', 'Luke Skywalker', 'Unit', 'lower priority');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType", "DetailsJson")
            VALUES (4, 'Pokémon', 'Pikachu', 'Pokémon', '{{"kept":true}}');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType", "Description")
            VALUES (5, 'Star Wars Unlimited', 'Luke Skywalker', 'Unit', 'higher priority');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType")
            VALUES (10, 'Pokémon', 'Pikachu', 'Pokémon');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style", "DetailsJson")
            VALUES (7, 3, 'sor', 'SOR-001', 'Common', 'Standard', '{{"sourceId":200,"variantOfSourceId":null,"reprintOfSourceId":null}}');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (8, 4, 'sor', 'SVP-001', 'Promo', 'Standard');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style", "DetailsJson")
            VALUES (9, 5, 'sor', 'SOR-001', 'Common', 'Hyperspace', '{{"sourceId":100,"variantOfSourceId":null,"reprintOfSourceId":null}}');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (11, 10, 'sor', 'SVP-002', 'Promo', 'Standard');
            """);
        await migrator.MigrateAsync("20261026143906_RetainSwuPrintingSourceUid");
        var gameEntity = await db.Games.SingleAsync(item => item.Name == "Star Wars Unlimited");
        var aspect = new Aspect { GameId = gameEntity.Id, Name = "Heroism", Color = "#e5d7b2" };
        db.CardAspects.Add(new CardAspect { CardId = 5, Aspect = aspect, Count = 2 });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();

        var game = await db.Games.SingleAsync(item => item.Name == "Star Wars Unlimited");
        var set = await db.Sets.SingleAsync();
        var standardTreatment = await db.Treatments.SingleAsync(item =>
            item.GameId == game.Id && item.Name == "Standard");
        var hyperspaceTreatment = await db.Treatments.SingleAsync(item =>
            item.GameId == game.Id && item.Name == "Hyperspace");
        var otherGameTreatment = await db.Treatments.Include(item => item.Game).SingleAsync(item =>
            item.Game.Name == "Pokémon" && item.Name == "Standard");
        var syncLog = await db.SyncLogs.Include(log => log.Set).SingleAsync();
        var printing = await db.CardPrintings.SingleAsync(item => item.Id == 7);
        var duplicateCardPrinting = await db.CardPrintings.SingleAsync(item => item.Id == 9);
        var otherGamePrinting = await db.CardPrintings.SingleAsync(item => item.Id == 8);
        var copiedAspect = await db.CardAspects.Include(item => item.Aspect).SingleAsync();
        Assert.Equal("Star Wars Unlimited", game.Name);
        Assert.Equal("star wars unlimited", game.NameNormalized);
        Assert.Equal(41, set.Id);
        Assert.Equal(game.Id, set.GameId);
        Assert.Equal(set.Id, syncLog.SetId);
        Assert.Equal(set.Id, syncLog.Set!.Id);
        Assert.Equal(set.Id, printing.SetId);
        Assert.Equal(standardTreatment.Id, printing.TreatmentId);
        Assert.Equal(hyperspaceTreatment.Id, duplicateCardPrinting.TreatmentId);
        Assert.Equal(3, printing.CardId);
        Assert.Equal(3, duplicateCardPrinting.CardId);
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.Null(otherGamePrinting.SetId);
        Assert.Equal(otherGameTreatment.Id, otherGamePrinting.TreatmentId);
        Assert.Equal(3, copiedAspect.CardId);
        Assert.Equal(2, copiedAspect.Count);
        Assert.Equal("Heroism", copiedAspect.Aspect.Name);
        var pokemonGame = await db.Games.SingleAsync(item => item.Name == "Pokémon");
        Assert.Equal(pokemonGame.Id, (await db.Cards.SingleAsync(item => item.Id == 4)).GameId);
        Assert.Equal("Pokémon", await db.CardCardTypes
            .Where(association => association.CardId == 4 && association.SourceRole == "Type")
            .Select(association => association.CardType.Name)
            .SingleAsync());
        var cardColumns = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('Cards')";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                cardColumns.Add(reader.GetString(1));
        }
        Assert.DoesNotContain(cardColumns, column =>
            new[]
            {
                "Game", "GameNorm", "CardType", "CardType2", "DetailsJson", "Arena", "Aspects",
                "Traits", "Keywords", "ApiCreatedAt", "ApiUpdatedAt", "BaseCardId",
                "BaseStrapiId", "StrapiId"
            }.Contains(column));
        Assert.Equal(4, (await db.CardPrintings.SingleAsync(item => item.Id == 11)).CardId);
        Assert.Equal("higher priority", (await db.Cards.SingleAsync(item => item.Id == 3)).Text);
    }

    [Fact]
    public async Task Migration_KeepsOwnedPrintingOnDuplicateCard_WhenPrintingIdentityCollides()
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
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType")
            VALUES (3, 'Star Wars Unlimited', 'Collision card', 'Unit');
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType")
            VALUES (5, 'Star Wars Unlimited', 'Collision card', 'Unit');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (7, 3, 'SOR', '001', 'Common', 'Standard');
            INSERT INTO "CardPrintings" ("Id", "CardId", "Set", "Number", "Rarity", "Style")
            VALUES (9, 5, 'SOR', '001', 'Common', 'Standard');
            """);
        var user = new User { Username = "owner", DisplayName = "Owner" };
        db.UserCards.Add(new UserCard { User = user, CardPrintingId = 9, QuantityOwned = 1 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var owned = await db.UserCards.SingleAsync();
        var printing = await db.CardPrintings.SingleAsync(item => item.Id == 9);
        Assert.Equal(9, owned.CardPrintingId);
        Assert.Equal(3, printing.CardId);
        Assert.Contains(await db.Cards.Select(item => item.Id).ToListAsync(), id => id == 3);
    }

    [Fact]
    public async Task Migration_CanBeRolledBackToDenormalizedCardSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261028150939_ReplaceCardPrintingStyleWithTreatment");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Cards" ("Id", "Game", "Name", "CardType", "CardType2")
            VALUES (1, 'Star Wars Unlimited', 'Rollback card', 'Unit', 'Leader');
            """);

        await migrator.MigrateAsync();
        await migrator.MigrateAsync("20261028150939_ReplaceCardPrintingStyleWithTreatment");

        var card = await db.Database.SqlQueryRaw<string>(
            "SELECT \"Game\" || '|' || \"CardType\" || '|' || \"CardType2\" AS \"Value\" FROM \"Cards\" WHERE \"Id\" = 1")
            .SingleAsync();
        Assert.Equal("Star Wars Unlimited|Unit|Leader", card);
        var columns = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Cards')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
        Assert.DoesNotContain("GameId", columns);
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
        var card = new Card { Game = game, Name = "Test card" };
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
        var secondCard = new Card { GameId = game.Id, Name = "Second card" };
        db.Cards.Add(secondCard);
        await db.SaveChangesAsync();
        db.CardAspects.Add(new CardAspect { CardId = secondCard.Id, AspectId = aspectId, Count = 0 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
