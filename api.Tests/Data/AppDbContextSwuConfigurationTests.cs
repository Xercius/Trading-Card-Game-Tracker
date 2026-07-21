using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api.Tests.Data;

public sealed class AppDbContextSwuConfigurationTests
{
    [Fact]
    public void OnModelCreating_ConfiguresSwuEntities_WithExpectedTablesAndIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new AppDbContext(options);

        var swuSet = db.Model.FindEntityType(typeof(SwuSet));
        var swuCard = db.Model.FindEntityType(typeof(SwuCard));
        var swuCardPrinting = db.Model.FindEntityType(typeof(SwuCardPrinting));
        var syncLog = db.Model.FindEntityType(typeof(SyncLog));

        Assert.NotNull(swuSet);
        Assert.NotNull(swuCard);
        Assert.NotNull(swuCardPrinting);
        Assert.NotNull(syncLog);

        Assert.Equal("SwuSets", swuSet!.GetTableName());
        Assert.Equal("SwuCards", swuCard!.GetTableName());
        Assert.Equal("SwuCardPrintings", swuCardPrinting!.GetTableName());
        Assert.Equal("SyncLogs", syncLog!.GetTableName());

        var cardUidIndex = swuCard.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SwuCard.CardUid) }));

        Assert.True(cardUidIndex.IsUnique);
        Assert.Equal("\"CardUid\" IS NOT NULL", cardUidIndex.GetFilter());
    }
}
