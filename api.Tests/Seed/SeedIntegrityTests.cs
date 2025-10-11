using api.Data;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Infrastructure;

public class SeedIntegrityTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task SeededData_HasNoOrphanedForeignKeys()
    {
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var orphanedPrintings = await db.CardPrintings
            .Where(p => !db.Cards.Any(c => c.CardId == p.CardId))
            .ToListAsync();
        Assert.Empty(orphanedPrintings);

        var orphanedDeckCards = await db.DeckCards
            .Where(dc => !db.Decks.Any(d => d.Id == dc.DeckId)
                         || !db.CardPrintings.Any(p => p.Id == dc.CardPrintingId))
            .ToListAsync();
        Assert.Empty(orphanedDeckCards);
    }
}
