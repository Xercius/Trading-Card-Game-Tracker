using api.Data;
using api.Importing.Pokemon;
using api.Models;
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
            .FirstOrDefaultAsync(c => c.Printings.Any(p => p.CardUid == "base1-4:holo"));

        Assert.NotNull(card);
        Assert.StartsWith("pkmn:charizard:", card.CardUid);
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
    public async Task SyncCardsAsync_ConsolidatesSameCardAcrossTreatmentsAndSets_WhileKeepingDistinctCardsSeparate()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sharedAbilities = new List<TcgdexAbility>
        {
            new() { Type = "Ability", Name = "Psychic Embrace", Effect = "Attach Basic Psychic Energy from discard." }
        };
        var sharedAttacks = new List<TcgdexAttack>
        {
            new() { Cost = ["Psychic", "Psychic", "Colorless"], Name = "Miracle Force", Damage = "190", Effect = "Recover from all Special Conditions." }
        };

        var client = new StubPokemonApiClient
        {
            Sets =
            [
                new() { Id = "sv01", Name = "Scarlet & Violet" },
                new() { Id = "sv04.5", Name = "Paldean Fates" }
            ],
            SetDetails =
            {
                ["sv01"] = new()
                {
                    Id = "sv01",
                    Name = "Scarlet & Violet",
                    ReleaseDate = "2023-03-31",
                    Serie = new() { Id = "sv", Name = "Scarlet & Violet" },
                    Cards =
                    [
                        new() { Id = "sv01-086", LocalId = "086", Name = "Gardevoir ex" },
                        new() { Id = "sv01-228", LocalId = "228", Name = "Gardevoir ex" },
                        new() { Id = "sv01-245", LocalId = "245", Name = "Gardevoir ex" },
                        new() { Id = "sv01-999", LocalId = "999", Name = "Gardevoir ex" }
                    ]
                },
                ["sv04.5"] = new()
                {
                    Id = "sv04.5",
                    Name = "Paldean Fates",
                    ReleaseDate = "2024-01-26",
                    Serie = new() { Id = "sv", Name = "Scarlet & Violet" },
                    Cards =
                    [
                        new() { Id = "sv04.5-029", LocalId = "029", Name = "Gardevoir ex" }
                    ]
                }
            },
            Cards =
            {
                ["sv01-086"] = new()
                {
                    Id = "sv01-086",
                    LocalId = "086",
                    Name = "Gardevoir ex",
                    Category = "Pokemon",
                    Hp = 310,
                    Types = ["Psychic"],
                    Stage = "Stage2",
                    Rarity = "Double rare",
                    Illustrator = "N-DESIGN Inc.",
                    Image = "https://assets.tcgdex.net/en/sv/sv01/086",
                    Variants = new() { Holo = true },
                    Abilities = sharedAbilities,
                    Attacks = sharedAttacks
                },
                ["sv01-228"] = new()
                {
                    Id = "sv01-228",
                    LocalId = "228",
                    Name = "Gardevoir ex",
                    Category = "Pokemon",
                    Hp = 310,
                    Types = ["Psychic"],
                    Stage = "Stage2",
                    Rarity = "Ultra Rare",
                    Illustrator = "N-DESIGN Inc.",
                    Image = "https://assets.tcgdex.net/en/sv/sv01/228",
                    Variants = new() { Holo = true },
                    Abilities = sharedAbilities,
                    Attacks = sharedAttacks
                },
                ["sv01-245"] = new()
                {
                    Id = "sv01-245",
                    LocalId = "245",
                    Name = "Gardevoir ex",
                    Category = "Pokemon",
                    Hp = 310,
                    Types = ["Psychic"],
                    Stage = "Stage2",
                    Rarity = "Special illustration rare",
                    Illustrator = "Jiro Sasumo",
                    Image = "https://assets.tcgdex.net/en/sv/sv01/245",
                    Variants = new() { Holo = true },
                    Abilities = sharedAbilities,
                    Attacks = sharedAttacks
                },
                ["sv04.5-029"] = new()
                {
                    Id = "sv04.5-029",
                    LocalId = "029",
                    Name = "Gardevoir ex",
                    Category = "Pokemon",
                    Hp = 310,
                    Types = ["Psychic"],
                    Stage = "Stage2",
                    Rarity = "Double rare",
                    Illustrator = "N-DESIGN Inc.",
                    Image = "https://assets.tcgdex.net/en/sv/sv04.5/029",
                    Variants = new() { Holo = true },
                    Abilities = sharedAbilities,
                    Attacks = sharedAttacks
                },
                // Mechanically distinct card with the same name
                ["sv01-999"] = new()
                {
                    Id = "sv01-999",
                    LocalId = "999",
                    Name = "Gardevoir ex",
                    Category = "Pokemon",
                    Hp = 230,
                    Types = ["Psychic"],
                    Stage = "Stage2",
                    Rarity = "Double rare",
                    Image = "https://assets.tcgdex.net/en/sv/sv01/999",
                    Variants = new() { Holo = true },
                    Attacks =
                    [
                        new() { Cost = ["Psychic", "Colorless"], Name = "Psybeam", Damage = "120" }
                    ]
                }
            }
        };

        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        await importer.SyncSetsAsync();
        await importer.SyncCardsAsync(["sv01", "sv04.5"]);

        var gardevoirCards = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Where(c => c.Name == "Gardevoir ex")
            .ToListAsync();

        // Only 2 unique mechanical cards should exist
        Assert.Equal(2, gardevoirCards.Count);

        var mainGardevoir = Assert.Single(gardevoirCards, c => c.Hp == 310);
        Assert.Equal(4, mainGardevoir.Printings.Count);
        Assert.Contains(mainGardevoir.Printings, p => p.CatalogSet?.Code == "sv01" && p.Number == "086" && p.RarityLookup?.Name == "Double rare");
        Assert.Contains(mainGardevoir.Printings, p => p.CatalogSet?.Code == "sv01" && p.Number == "228" && p.RarityLookup?.Name == "Ultra Rare");
        Assert.Contains(mainGardevoir.Printings, p => p.CatalogSet?.Code == "sv01" && p.Number == "245" && p.RarityLookup?.Name == "Special illustration rare");
        Assert.Contains(mainGardevoir.Printings, p => p.CatalogSet?.Code == "sv04.5" && p.Number == "029" && p.RarityLookup?.Name == "Double rare");

        var distinctGardevoir = Assert.Single(gardevoirCards, c => c.Hp == 230);
        Assert.Single(distinctGardevoir.Printings);
    }

    [Fact]
    public async Task SyncCardsAsync_UsesVariantsDetailedForFoilsAndStampsAndSkipsJumbo()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new StubPokemonApiClient
        {
            Sets = [new() { Id = "sv03.5", Name = "151" }],
            SetDetails =
            {
                ["sv03.5"] = new()
                {
                    Id = "sv03.5",
                    Name = "151",
                    ReleaseDate = "2023-09-22",
                    Serie = new() { Id = "sv", Name = "Scarlet & Violet" },
                    Cards = [new() { Id = "sv03.5-025", LocalId = "025", Name = "Pikachu" }]
                }
            },
            Cards =
            {
                ["sv03.5-025"] = new()
                {
                    Id = "sv03.5-025",
                    LocalId = "025",
                    Name = "Pikachu",
                    Category = "Pokemon",
                    Hp = 60,
                    Types = ["Lightning"],
                    Stage = "Basic",
                    Rarity = "Common",
                    VariantsDetailed =
                    [
                        new() { Type = "normal", Size = "standard" },
                        new() { Type = "reverse", Size = "standard", Foil = "pokeball" },
                        new() { Type = "reverse", Size = "standard", Foil = "masterball" },
                        new() { Type = "holo", Size = "jumbo" }
                    ],
                    Attacks = [new() { Cost = ["Lightning"], Name = "Charge", Effect = "Search deck for Lightning Energy." }]
                }
            }
        };

        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        await importer.SyncSetsAsync();
        await importer.SyncCardsAsync(["sv03.5"]);

        var pikachu = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .FirstOrDefaultAsync(c => c.Printings.Any(p => p.CardUid != null && p.CardUid.StartsWith("sv03.5-025:")));

        Assert.NotNull(pikachu);
        Assert.Equal(3, pikachu.Printings.Count);
        Assert.Contains(pikachu.Printings, p => p.Treatment?.Name == "Standard" && !p.IsFoil);
        Assert.Contains(pikachu.Printings, p => p.Treatment?.Name == "Poké Ball Reverse Holo" && p.IsFoil);
        Assert.Contains(pikachu.Printings, p => p.Treatment?.Name == "Master Ball Reverse Holo" && p.IsFoil);
    }

    [Fact]
    public async Task SyncSetsAsync_ConsolidatesExistingLegacyDuplicateCards_PreservingUserCards()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var catalog = new PokemonCatalog(db);
        await catalog.InitializeAsync();
        var game = catalog.Game;
        var psychic = catalog.GetOrCreateAspect("Psychic");
        var pokemonType = catalog.GetOrCreateCardType("Pokemon");
        var holoTreatment = catalog.GetOrCreateTreatment("Holo");

        var set = new Set
        {
            GameId = game.Id,
            Code = "legacy-sv01",
            Name = "Legacy SV01",
            ReleasedAt = new DateOnly(2023, 3, 31)
        };
        db.Sets.Add(set);
        await db.SaveChangesAsync();

        var legacyCard1 = new Card
        {
            GameId = game.Id,
            CardUid = "legacy-sv01-086",
            Name = "Legacy Gardevoir ex",
            Hp = 310,
            Stage = "Stage 2",
            Text = "[Ability] Psychic Embrace: Attach Energy.\n\n[Psychic][Psychic][Colorless] Miracle Force (190)"
        };
        legacyCard1.CardAspects.Add(new CardAspect { Card = legacyCard1, Aspect = psychic, Count = 1 });
        legacyCard1.CardTypes.Add(new CardCardType { Card = legacyCard1, CardType = pokemonType, SourceRole = "Type" });
        var printing1 = new CardPrinting
        {
            Card = legacyCard1,
            SetId = set.Id,
            Number = "086",
            CardUid = "legacy-sv01-086:holo",
            Treatment = holoTreatment,
            IsFoil = true
        };
        legacyCard1.Printings.Add(printing1);

        var legacyCard2 = new Card
        {
            GameId = game.Id,
            CardUid = "legacy-sv01-245",
            Name = "Legacy Gardevoir ex",
            Hp = 310,
            Stage = "Stage 2",
            Text = "[Ability] Psychic Embrace: Attach Energy.\n\n[Psychic][Psychic][Colorless] Miracle Force (190)"
        };
        legacyCard2.CardAspects.Add(new CardAspect { Card = legacyCard2, Aspect = psychic, Count = 1 });
        legacyCard2.CardTypes.Add(new CardCardType { Card = legacyCard2, CardType = pokemonType, SourceRole = "Type" });
        var printing2 = new CardPrinting
        {
            Card = legacyCard2,
            SetId = set.Id,
            Number = "245",
            CardUid = "legacy-sv01-245:holo",
            Treatment = holoTreatment,
            IsFoil = true
        };
        legacyCard2.Printings.Add(printing2);

        db.Cards.AddRange(legacyCard1, legacyCard2);
        await db.SaveChangesAsync();

        var printing1Id = printing1.Id;
        var printing2Id = printing2.Id;

        db.UserCards.Add(new UserCard
        {
            UserId = 1,
            CardPrintingId = printing2Id,
            QuantityOwned = 2
        });
        await db.SaveChangesAsync();

        var client = new StubPokemonApiClient
        {
            Sets = [new() { Id = "legacy-sv01", Name = "Legacy SV01" }]
        };
        var importer = new PokemonTcgdexImporter(db, client, NullLogger<PokemonTcgdexImporter>.Instance);
        var summary = await importer.SyncSetsAsync();

        Assert.Contains(summary.Messages, m => m.Contains("Consolidated 1 duplicate Pokemon cards"));

        var remainingCards = await db.Cards
            .Include(c => c.Printings)
            .Where(c => c.GameId == game.Id && c.Name == "Legacy Gardevoir ex")
            .ToListAsync();

        var canonical = Assert.Single(remainingCards);
        Assert.StartsWith("pkmn:legacy-gardevoir-ex:", canonical.CardUid);
        Assert.Equal(2, canonical.Printings.Count);
        Assert.Contains(canonical.Printings, p => p.Id == printing1Id && p.Number == "086");
        Assert.Contains(canonical.Printings, p => p.Id == printing2Id && p.Number == "245");

        var userCard = await db.UserCards.FirstOrDefaultAsync(uc => uc.CardPrintingId == printing2Id);
        Assert.NotNull(userCard);
        Assert.Equal(2, userCard.QuantityOwned);
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

        var card = await db.Cards.FirstOrDefaultAsync(c => c.Name == "Testmon");
        Assert.Null(card);
    }
}
