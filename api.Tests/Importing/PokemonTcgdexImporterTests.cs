using api.Data;
using api.Importing.Pokemon;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Importing;

public sealed class PokemonTcgdexImporterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private sealed class StubPokemonApiClient : IPokemonApiClient
    {
        public List<TcgdexSetResume> Sets { get; set; } = [];
        public Dictionary<string, TcgdexSetDetail> SetDetails { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TcgdexCard> Cards { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<TcgdexSetResume>> GetSetsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TcgdexSetResume>>(Sets);

        public Task<TcgdexSetDetail?> GetSetAsync(string setId, CancellationToken ct = default)
            => Task.FromResult(SetDetails.TryGetValue(setId, out var s) ? s : null);

        public Task<TcgdexCard?> GetCardAsync(string cardId, CancellationToken ct = default)
            => Task.FromResult(Cards.TryGetValue(cardId, out var c) ? c : null);

        public Task<IReadOnlyList<TcgdexSerieResume>> GetSeriesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TcgdexSerieResume>>(new List<TcgdexSerieResume>
            {
                new() { Id = "bw", Name = "Black & White" }
            });
    }

    [Fact]
    public async Task SyncSetsAsync_PopulatesSetsWithEraAndReleaseDate()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new StubPokemonApiClient
        {
            Sets =
            [
                new() { Id = "bw01", Name = "Black & White", CardCount = new() { Total = 115, Official = 114 } }
            ],
            SetDetails =
            {
                ["bw01"] = new()
                {
                    Id = "bw01",
                    Name = "Black & White",
                    ReleaseDate = "2011-04-25",
                    Serie = new() { Id = "bw", Name = "Black & White" },
                    CardCount = new() { Total = 115, Official = 114 }
                }
            }
        };

        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        var summary = await importer.SyncSetsAsync();

        Assert.Contains("Successfully synced 1 Pokemon sets.", summary.Messages);

        var set = await db.Sets.FirstOrDefaultAsync(s => s.Code == "bw01");
        Assert.NotNull(set);
        Assert.Equal("Black & White", set.Name);
        Assert.Equal("Black & White", set.Era);
        Assert.Equal(new DateOnly(2011, 4, 25), set.ReleasedAt);

        // Run second time (idempotent)
        var summary2 = await importer.SyncSetsAsync();
        Assert.Contains("Successfully synced 1 Pokemon sets.", summary2.Messages);
        var setCount = await db.Sets.CountAsync(s => s.Code == "bw01");
        Assert.Equal(1, setCount);
    }

    [Fact]
    public async Task SyncCardsAsync_DecomposesVariantsAndLinksAspects()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new StubPokemonApiClient
        {
            Sets =
            [
                new() { Id = "base1", Name = "Base Set", CardCount = new() { Total = 102, Official = 102 } }
            ],
            SetDetails =
            {
                ["base1"] = new()
                {
                    Id = "base1",
                    Name = "Base Set",
                    ReleaseDate = "1999-01-09",
                    Serie = new() { Id = "base", Name = "Base" },
                    Cards =
                    [
                        new() { Id = "base1-4", LocalId = "4", Name = "Charizard", Image = "https://assets.tcgdex.net/en/base/base1/4" }
                    ]
                }
            },
            Cards =
            {
                ["base1-4"] = new()
                {
                    Id = "base1-4",
                    LocalId = "4",
                    Name = "Charizard",
                    Category = "Pokemon",
                    Hp = 120,
                    Types = ["Fire"],
                    Stage = "Stage2",
                    Rarity = "Rare Holo",
                    Image = "https://assets.tcgdex.net/en/base/base1/4",
                    Variants = new()
                    {
                        Normal = false,
                        Reverse = false,
                        Holo = true,
                        FirstEdition = true
                    },
                    Attacks =
                    [
                        new() { Cost = ["Fire", "Fire", "Fire", "Fire"], Name = "Fire Spin", Damage = "100", Effect = "Discard 2 Energy cards." }
                    ],
                    Abilities =
                    [
                        new() { Type = "Pokemon Power", Name = "Energy Burn", Effect = "As often as you like during your turn..." }
                    ],
                    Updated = "2024-01-01T00:00:00.000Z"
                }
            }
        };

        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        await importer.SyncSetsAsync();

        var cardSummary = await importer.SyncCardsAsync(["base1"]);
        Assert.Equal(1, cardSummary.CardsCreated);
        // Holo + 1st Edition variants = 2 printings
        Assert.Equal(2, cardSummary.PrintingsCreated);

        var card = await db.Cards
            .Include(c => c.CardAspects).ThenInclude(ca => ca.Aspect)
            .Include(c => c.CardTypes).ThenInclude(ct => ct.CardType)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .FirstOrDefaultAsync(c => c.CardUid == "base1-4");

        Assert.NotNull(card);
        Assert.Equal("Charizard", card.Name);
        Assert.Equal(120, card.Hp);

        // Aspect is Fire with red/orange hex color
        Assert.Single(card.CardAspects);
        Assert.Equal("Fire", card.CardAspects.First().Aspect.Name);
        Assert.Equal("#F08030", card.CardAspects.First().Aspect.Color);

        // CardType has SourceRole = "Type"
        Assert.Contains(card.CardTypes, ct => ct.CardType.Name == "Pokemon" && ct.SourceRole == "Type");

        // Printings
        Assert.Equal(2, card.Printings.Count);
        Assert.Contains(card.Printings, p => p.Treatment?.Name == "Holo" && p.IsFoil);
        Assert.Contains(card.Printings, p => p.Treatment?.Name == "1st Edition");

        // Stage
        Assert.Equal("Stage 2", card.Stage);

        // Rules text has attacks & abilities in Text
        Assert.Contains("[Pokemon Power] Energy Burn", card.Text);
        Assert.Contains("[Fire][Fire][Fire][Fire] Fire Spin (100)", card.Text);
    }

    [Fact]
    public async Task SyncCardsAsync_DryRun_DoesNotPersistToDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new StubPokemonApiClient
        {
            Sets =
            [
                new() { Id = "test1", Name = "Test Set" }
            ],
            SetDetails =
            {
                ["test1"] = new()
                {
                    Id = "test1",
                    Name = "Test Set",
                    Cards = [new() { Id = "test1-1", LocalId = "1", Name = "Testmon" }]
                }
            },
            Cards =
            {
                ["test1-1"] = new()
                {
                    Id = "test1-1",
                    LocalId = "1",
                    Name = "Testmon",
                    Category = "Pokemon",
                    Variants = new() { Normal = true }
                }
            }
        };

        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        await importer.SyncSetsAsync();

        var dryRunSummary = await importer.SyncCardsAsync(["test1"], dryRun: true);
        Assert.True(dryRunSummary.DryRun);

        var card = await db.Cards.FirstOrDefaultAsync(c => c.CardUid == "test1-1");
        Assert.Null(card);
    }
}
