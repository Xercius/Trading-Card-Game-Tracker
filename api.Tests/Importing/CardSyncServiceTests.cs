using api.Data;
using api.Importing;
using api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api.Tests.Importing;

public sealed class CardSyncServiceTests
{
    [Fact]
    public async Task SyncNewAndUpdatedCardsAsync_WhenNoSyncHistory_HandlesFirstSync()
    {
        await using var db = await CreateDbContextAsync();
        var createdAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var client = new StubSwuApiClient(
            records:
            [
                CreateRecord(
                    id: 1001,
                    title: "Luke Skywalker",
                    cardUid: "1001",
                    serialCode: "SOR-001",
                    setCode: "SOR",
                    setName: "Spark of Rebellion",
                    subtitle: "Faithful Friend",
                    text: "Deal 3 damage.",
                    artist: "Artist One",
                    cost: 5,
                    power: 4,
                    health: 6,
                    arena: "Ground",
                    aspects: ["Heroism", "Command"],
                    traits: ["Rebel", "Jedi"],
                    keywords: ["Restore"],
                    rarity: "Legendary",
                    imageUrl: "https://example.test/front.png",
                    backImageUrl: "https://example.test/back.png",
                    createdAt: createdAt,
                    updatedAt: updatedAt)
            ]);
        var service = new CardSyncService(db, client);

        var summary = await service.SyncNewAndUpdatedCardsAsync("swu", "SOR");

        Assert.NotNull(client.CapturedFilter);
        Assert.Null(client.CapturedFilter!.UpdatedSince);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(0, summary.PrintingsUpdated);
        Assert.Equal(0, summary.Errors);

        var set = await db.SwuSets.SingleAsync();
        Assert.Equal("SOR", set.Code);
        Assert.Equal("Spark of Rebellion", set.Name);
        Assert.Equal(updatedAt, set.LastSyncedAt);

        var card = await db.SwuCards.SingleAsync();
        Assert.Equal(1001, card.StrapiId);
        Assert.Equal("1001", card.CardUid);
        Assert.Equal("Luke Skywalker", card.Title);
        Assert.Equal("Faithful Friend", card.Subtitle);
        Assert.Equal("Unit", card.CardType);
        Assert.Equal("Deal 3 damage.", card.Description);
        Assert.Equal("Ground", card.Arena);
        Assert.Equal(5, card.Cost);
        Assert.Equal(4, card.Power);
        Assert.Equal(6, card.Health);
        Assert.Equal("Artist One", card.Artist);
        Assert.Equal("Heroism|Command", card.Aspects);
        Assert.Equal("Rebel|Jedi", card.Traits);
        Assert.Equal("Restore", card.Keywords);
        Assert.Equal(set.Id, card.SwuSetId);
        Assert.Equal(createdAt, card.ApiCreatedAt);
        Assert.Equal(updatedAt, card.ApiUpdatedAt);
        Assert.Equal(updatedAt, card.LastSyncedAt);

        var printing = await db.SwuCardPrintings.SingleAsync();
        Assert.Equal(1001, printing.StrapiId);
        Assert.Equal(card.Id, printing.SwuCardId);
        Assert.Equal(set.Id, printing.SwuSetId);
        Assert.Equal("SOR-001", printing.Number);
        Assert.Equal("Legendary", printing.Rarity);
        Assert.Equal("Standard", printing.Style);
        Assert.Equal("https://example.test/front.png", printing.ImageUrl);
        Assert.Equal("https://example.test/back.png", printing.BackImageUrl);
        Assert.Equal(createdAt, printing.ApiCreatedAt);
        Assert.Equal(updatedAt, printing.ApiUpdatedAt);
        Assert.Equal(updatedAt, printing.LastSyncedAt);
    }

    [Fact]
    public async Task SyncNewAndUpdatedCardsAsync_WhenCardsMatchDatabase_HandlesNoChangeSync()
    {
        await using var db = await CreateDbContextAsync();
        var lastSync = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

        var set = new SwuSet
        {
            Code = "SOR",
            Name = "Spark of Rebellion",
            LastSyncedAt = lastSync
        };
        db.SwuSets.Add(set);

        var card = new SwuCard
        {
            StrapiId = 2001,
            CardUid = "2001",
            Title = "Leia Organa",
            Subtitle = null,
            CardType = "Unit",
            Description = "Give another unit +1/+1.",
            Arena = "Ground",
            Cost = 3,
            Power = 2,
            Health = 4,
            Artist = "Artist A",
            Aspects = "Heroism|Command",
            Traits = "Rebel|Leader",
            Keywords = "Sentinel",
            SwuSet = set,
            ApiCreatedAt = createdAt,
            ApiUpdatedAt = updatedAt,
            LastSyncedAt = lastSync
        };
        db.SwuCards.Add(card);

        db.SwuCardPrintings.Add(new SwuCardPrinting
        {
            StrapiId = 2001,
            SwuCard = card,
            SwuSet = set,
            Number = "SOR-010",
            Rarity = "Rare",
            Style = "Standard",
            ImageUrl = "https://example.test/front.png",
            BackImageUrl = null,
            ApiCreatedAt = createdAt,
            ApiUpdatedAt = updatedAt,
            LastSyncedAt = lastSync
        });

        db.ImportSyncHistories.Add(new ImportSyncHistory
        {
            ImporterKey = "swu",
            SetCode = "SOR",
            LastSyncedAt = lastSync
        });

        await db.SaveChangesAsync();

        var client = new StubSwuApiClient(
            records:
            [
                CreateRecord(
                    id: 2001,
                    title: "Leia Organa",
                    cardUid: "2001",
                    serialCode: "SOR-010",
                    setCode: "SOR",
                    setName: "Spark of Rebellion",
                    typeName: "Unit",
                    text: "Give another unit +1/+1.",
                    artist: "Artist A",
                    cost: 3,
                    power: 2,
                    health: 4,
                    arena: "Ground",
                    aspects: ["Heroism", "Command"],
                    traits: ["Rebel", "Leader"],
                    keywords: ["Sentinel"],
                    rarity: "Rare",
                    imageUrl: "https://example.test/front.png",
                    createdAt: createdAt,
                    updatedAt: updatedAt)
            ]);
        var service = new CardSyncService(db, client);

        var summary = await service.SyncNewAndUpdatedCardsAsync("swu", "SOR");

        Assert.NotNull(client.CapturedFilter);
        Assert.Equal(lastSync, client.CapturedFilter!.UpdatedSince);
        Assert.Equal(0, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(0, summary.PrintingsCreated);
        Assert.Equal(0, summary.PrintingsUpdated);
        Assert.Equal(0, summary.Errors);
    }

    [Fact]
    public async Task SyncNewAndUpdatedCardsAsync_WhenDatabaseDiffers_IdentifiesUpdatedCards()
    {
        await using var db = await CreateDbContextAsync();
        var lastSync = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var set = new SwuSet
        {
            Code = "SOR",
            Name = "Spark of Rebellion",
            LastSyncedAt = lastSync
        };
        db.SwuSets.Add(set);

        var card = new SwuCard
        {
            StrapiId = 3001,
            CardUid = "3001",
            Title = "Han Solo",
            Subtitle = null,
            CardType = "Unit",
            Description = "Old text",
            Arena = "Ground",
            Cost = 4,
            Power = 3,
            Health = 5,
            Artist = "Artist B",
            Aspects = "Heroism|Cunning",
            Traits = "Rebel|Smuggler",
            Keywords = "Raid",
            SwuSet = set,
            ApiCreatedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            ApiUpdatedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            LastSyncedAt = lastSync
        };
        db.SwuCards.Add(card);

        db.SwuCardPrintings.Add(new SwuCardPrinting
        {
            StrapiId = 3001,
            SwuCard = card,
            SwuSet = set,
            Number = "SOR-020",
            Rarity = "Common",
            Style = "Standard",
            ImageUrl = "https://example.test/old-front.png",
            BackImageUrl = null,
            ApiCreatedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            ApiUpdatedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            LastSyncedAt = lastSync
        });

        await db.SaveChangesAsync();

        var client = new StubSwuApiClient(
            records:
            [
                CreateRecord(
                    id: 3001,
                    title: "Han Solo",
                    cardUid: "3001",
                    serialCode: "SOR-020",
                    setCode: "SOR",
                    setName: "Spark of Rebellion Remastered",
                    typeName: "Unit",
                    text: "New text",
                    artist: "Artist B",
                    cost: 4,
                    power: 3,
                    health: 5,
                    arena: "Ground",
                    aspects: ["Heroism", "Cunning"],
                    traits: ["Rebel", "Smuggler"],
                    keywords: ["Raid"],
                    rarity: "Legendary",
                    imageUrl: "https://example.test/new-front.png",
                    createdAt: new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
                    updatedAt: new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero))
            ]);
        var service = new CardSyncService(db, client);

        var summary = await service.SyncNewAndUpdatedCardsAsync("swu", "SOR");

        Assert.Equal(0, summary.CardsCreated);
        Assert.Equal(1, summary.CardsUpdated);
        Assert.Equal(0, summary.PrintingsCreated);
        Assert.Equal(1, summary.PrintingsUpdated);
        Assert.Equal(0, summary.Errors);

        var updatedSet = await db.SwuSets.SingleAsync(s => s.Code == "SOR");
        Assert.Equal("Spark of Rebellion Remastered", updatedSet.Name);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero), updatedSet.LastSyncedAt);

        var updatedCard = await db.SwuCards.SingleAsync(c => c.StrapiId == 3001);
        Assert.Equal("New text", updatedCard.Description);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero), updatedCard.ApiUpdatedAt);
        Assert.Equal(updatedSet.Id, updatedCard.SwuSetId);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero), updatedCard.LastSyncedAt);

        var updatedPrinting = await db.SwuCardPrintings.SingleAsync(p => p.StrapiId == 3001);
        Assert.Equal("Legendary", updatedPrinting.Rarity);
        Assert.Equal("https://example.test/new-front.png", updatedPrinting.ImageUrl);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero), updatedPrinting.ApiUpdatedAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero), updatedPrinting.LastSyncedAt);
    }

    [Fact]
    public async Task SyncNewAndUpdatedCardsAsync_WhenApiFails_HandlesApiFailures()
    {
        await using var db = await CreateDbContextAsync();
        db.ImportSyncHistories.Add(new ImportSyncHistory
        {
            ImporterKey = "swu",
            SetCode = "SOR",
            LastSyncedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var client = new StubSwuApiClient(Array.Empty<StrapiRecord>())
        {
            GetAllCardsException = new HttpRequestException("SWU API unavailable")
        };
        var service = new CardSyncService(db, client);

        var summary = await service.SyncNewAndUpdatedCardsAsync("swu", "SOR");

        Assert.Equal(1, summary.Errors);
        Assert.Contains(summary.Messages, message => message.Contains("SWU API unavailable", StringComparison.Ordinal));
        Assert.Equal(0, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(0, summary.PrintingsCreated);
        Assert.Equal(0, summary.PrintingsUpdated);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero),
            await service.GetLastSyncTimeAsync("swu", "SOR"));
    }

    private static async Task<AppDbContext> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static StrapiRecord CreateRecord(
        int id,
        string title,
        string cardUid,
        string serialCode,
        string setCode,
        string setName,
        string typeName = "Unit",
        string? subtitle = null,
        string? text = null,
        string? artist = null,
        int? cost = null,
        int? power = null,
        int? health = null,
        string? arena = null,
        string[]? aspects = null,
        string[]? traits = null,
        string[]? keywords = null,
        string rarity = "Common",
        string? imageUrl = null,
        string? backImageUrl = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null) =>
        new(
            id,
            new SwuCardAttributes(
                Title: title,
                Subtitle: subtitle,
                CardUid: cardUid,
                SerialCode: serialCode,
                Locale: "en",
                CardNumber: 1,
                Rarity: rarity,
                Text: text,
                Artist: artist,
                Cost: cost,
                Power: power,
                Health: health,
                Arena: arena,
                Aspects: aspects,
                Traits: traits,
                Keywords: keywords,
                CreatedAt: createdAt,
                UpdatedAt: updatedAt,
                PublishedAt: updatedAt,
                Type: new StrapiRelation<SwuTypeAttributes>(
                    new StrapiRelationData<SwuTypeAttributes>(1, new SwuTypeAttributes(typeName, null))),
                Expansion: new StrapiRelation<SwuExpansionAttributes>(
                    new StrapiRelationData<SwuExpansionAttributes>(1, new SwuExpansionAttributes(setName, setCode))),
                VariantTypes: null,
                VariantOf: null,
                ReprintOf: null,
                ArtFront: imageUrl is null
                    ? null
                    : new StrapiRelation<SwuImageAttributes>(
                        new StrapiRelationData<SwuImageAttributes>(
                            1,
                            new SwuImageAttributes(imageUrl, null))),
                ArtBack: backImageUrl is null
                    ? null
                    : new StrapiRelation<SwuImageAttributes>(
                        new StrapiRelationData<SwuImageAttributes>(
                            2,
                            new SwuImageAttributes(backImageUrl, null)))));

    private sealed class StubSwuApiClient(IReadOnlyList<StrapiRecord> records) : ISWUApiClient
    {
        public SWUCardFilter? CapturedFilter { get; private set; }

        public Exception? GetAllCardsException { get; init; }

        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default)
        {
            CapturedFilter = filter;

            if (GetAllCardsException is not null)
            {
                throw GetAllCardsException;
            }

            return Task.FromResult(records);
        }

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(code == "SOR" ? 42 : null);
    }
}
