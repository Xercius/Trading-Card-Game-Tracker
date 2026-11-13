using System.Net.Http.Json;
using api.Data;
using api.Features.Cards.Dtos;
using api.Importing;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Features.Cards;

public class PrintingsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Get_with_card_type_search_matches_facet_results()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var response = await client.GetAsync("/api/cards/printings?q=cReAtUrE&uniqueCards=true");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var printings = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        var facets = await client.GetFromJsonAsync<CardsFacetResponse>("/api/cards/facets?q=cReAtUrE&printingSearch=true");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.GoblinGuidePrintingId, printing.PrintingId);
        Assert.NotNull(facets);
        Assert.Equal(printing.Game, Assert.Single(facets.Games).Value);
        Assert.Equal(printing.SetName, Assert.Single(facets.Sets).Value);
        Assert.Equal(printing.Rarity, Assert.Single(facets.Rarities).Value);
    }

    [Fact]
    public async Task Get_with_number_filters_to_exact_match()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=A1");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, printing.PrintingId);
        Assert.Equal("A1", printing.Number);
    }

    [Fact]
    public async Task Upsert_with_mismatched_card_id_uses_existing_printing_game_for_treatment()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
        var request = new UpsertPrintingRequest(
            TestDataSeeder.GoblinGuidePrintingId,
            TestDataSeeder.ElsaCardId,
            null,
            null,
            null,
            "Showcase",
            null);

        var response = await client.PostAsJsonAsync("/api/cards/printing", request);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printing = await db.CardPrintings
            .Include(p => p.Treatment)
            .SingleAsync(p => p.Id == TestDataSeeder.GoblinGuidePrintingId);
        var magicGameId = await db.Games
            .Where(game => game.NameNormalized == "magic")
            .Select(game => game.Id)
            .SingleAsync();

        Assert.Equal("Showcase", printing.Treatment!.Name);
        Assert.Equal(magicGameId, printing.Treatment.GameId);
    }

    [Fact]
    public async Task Get_with_style_filter_matches_treatment_name()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var printing = await db.CardPrintings.SingleAsync(
                p => p.Id == TestDataSeeder.GoblinGuidePrintingId);
            printing.Treatment = TreatmentLookup.Upsert(db, "Magic", "Hyperspace");
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
        var hyperspace = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Hyperspace");
        var showcase = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Showcase");
        var both = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=Hyperspace%2C%20Showcase");
        var partial = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?style=space");

        Assert.Contains(hyperspace!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.DoesNotContain(showcase!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.Contains(both!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.DoesNotContain(partial!, p => p.PrintingId == TestDataSeeder.GoblinGuidePrintingId);
    }

    [Fact]
    public async Task Get_with_unknown_number_returns_empty_list()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?number=UNKNOWN");

        Assert.NotNull(printings);
        Assert.Empty(printings);
    }

    [Fact]
    public async Task Get_with_single_game_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.Equal("Magic", p.Game));
    }

    [Fact]
    public async Task Get_with_multiple_games_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Lorcana");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.Contains(printings, p => p.Game == "Magic");
        Assert.Contains(printings, p => p.Game == "Lorcana");
        Assert.All(printings, p => Assert.True(p.Game == "Magic" || p.Game == "Lorcana"));
    }

    [Fact]
    public async Task Get_with_csv_games_normalizes_whitespace()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var normalized = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Lorcana");
        var withWhitespace = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=%20Magic%20,%20Lorcana%20");

        Assert.NotNull(normalized);
        Assert.NotNull(withWhitespace);
        Assert.Equal(normalized.Select(p => p.PrintingId), withWhitespace.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_csv_games_removes_duplicates()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var single = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");
        var duplicates = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic,Magic,Magic");

        Assert.NotNull(single);
        Assert.NotNull(duplicates);
        Assert.Equal(single.Select(p => p.PrintingId), duplicates.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_single_set_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal("Alpha", printing.SetName);
    }

    [Fact]
    public async Task Get_with_unique_cards_returns_oldest_printing_for_each_card()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true");

        Assert.NotNull(printings);
        Assert.Equal(4, printings.Count);
        var lightningBolt = Assert.Single(printings, p => p.CardId == TestDataSeeder.LightningBoltCardId);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, lightningBolt.PrintingId);
        Assert.Equal("Alpha", lightningBolt.SetName);
    }

    [Fact]
    public async Task Get_with_unique_cards_usesCandidateGameForHoloPreference()
    {
        await factory.ResetDatabaseAsync();
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pokemonGame = TreatmentLookup.GetOrAddGame(db, "Pokemon TCG");
                var magicGame = await db.Games.SingleAsync(game => game.NameNormalized == "magic");
                var pokemonCard = new Card { Game = pokemonGame, Name = "PokemonPreference" };
                var magicCard = new Card { Game = magicGame, Name = "MagicPreference" };
                db.Cards.AddRange(pokemonCard, magicCard);
                var pokemonSet = TreatmentLookup.UpsertSet(db, pokemonGame, "Pokemon Preference");
                var magicSet = TreatmentLookup.UpsertSet(db, magicGame, "Magic Preference");
                await db.SaveChangesAsync();

                db.CardPrintings.AddRange(
                    new CardPrinting
                    {
                        Card = pokemonCard,
                        SetId = pokemonSet.Id,
                        Number = "1",
                        Treatment = TreatmentLookup.Upsert(db, "Pokemon TCG", "Reverse Holo"),
                        ApiCreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    },
                    new CardPrinting
                    {
                        Card = pokemonCard,
                        SetId = pokemonSet.Id,
                        Number = "2",
                        Treatment = TreatmentLookup.Upsert(db, "Pokemon TCG", "Holo"),
                        ApiCreatedAt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    },
                    new CardPrinting
                    {
                        Card = magicCard,
                        SetId = magicSet.Id,
                        Number = "1",
                        Treatment = TreatmentLookup.Upsert(db, "Magic", "Reverse Holo"),
                        ApiCreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    },
                    new CardPrinting
                    {
                        Card = magicCard,
                        SetId = magicSet.Id,
                        Number = "2",
                        Treatment = TreatmentLookup.Upsert(db, "Magic", "Holo"),
                        ApiCreatedAt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    });
                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
            var allGames = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?uniqueCards=true&q=Preference");
            var pokemonOnly = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?uniqueCards=true&game=Pokemon%20TCG&q=PokemonPreference");
            var multiGame = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?uniqueCards=true&game=Pokemon%20TCG,Magic&q=Preference");

            Assert.NotNull(allGames);
            Assert.Equal("Holo", Assert.Single(allGames, printing => printing.CardName == "PokemonPreference").Style);
            Assert.NotNull(pokemonOnly);
            Assert.Equal("Holo", Assert.Single(pokemonOnly).Style);
            Assert.NotNull(multiGame);
            Assert.Equal("Holo", Assert.Single(multiGame, printing => printing.CardName == "PokemonPreference").Style);
            Assert.Equal("Reverse Holo", Assert.Single(multiGame, printing => printing.CardName == "MagicPreference").Style);
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task Get_with_unique_cards_set_filter_matches_later_printing_but_returns_oldest()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true&set=Beta");
        var noMatch = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?uniqueCards=true&set=Unknown");

        Assert.NotNull(printings);
        var printing = Assert.Single(printings);
        Assert.Equal(TestDataSeeder.LightningBoltAlphaPrintingId, printing.PrintingId);
        Assert.Equal("Alpha", printing.SetName);
        Assert.NotNull(noMatch);
        Assert.Empty(noMatch);
    }

    [Fact]
    public async Task Get_with_multiple_sets_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha,Beta");

        Assert.NotNull(printings);
        Assert.Equal(2, printings.Count);
        Assert.Contains(printings, p => p.SetName == "Alpha");
        Assert.Contains(printings, p => p.SetName == "Beta");
    }

    [Fact]
    public async Task Get_with_single_rarity_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.Equal("Common", p.Rarity));
    }

    [Fact]
    public async Task Get_with_multiple_rarities_csv()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common,Uncommon");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p => Assert.True(p.Rarity == "Common" || p.Rarity == "Uncommon"));
    }

    [Fact]
    public async Task Get_with_combined_csv_filters()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var printings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic&rarity=Common,Uncommon");

        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
        Assert.All(printings, p =>
        {
            Assert.Equal("Magic", p.Game);
            Assert.True(p.Rarity == "Common" || p.Rarity == "Uncommon");
        });
    }

    [Fact]
    public async Task Get_with_empty_csv_returns_all()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var allPrintings = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings");
        var emptyFilter = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=");

        Assert.NotNull(allPrintings);
        Assert.NotNull(emptyFilter);
        Assert.Equal(allPrintings.Select(p => p.PrintingId), emptyFilter.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_game_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=magic");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=MAGIC");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?game=Magic");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_set_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=alpha");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=ALPHA");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?set=Alpha");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_with_case_insensitive_rarity_filter()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var lowercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=common");
        var uppercase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=COMMON");
        var mixedcase = await client.GetFromJsonAsync<List<PrintingDto>>("/api/cards/printings?rarity=Common");

        Assert.NotNull(lowercase);
        Assert.NotNull(uppercase);
        Assert.NotNull(mixedcase);
        Assert.NotEmpty(lowercase);

        // All case variations should return the same results
        Assert.Equal(lowercase.Select(p => p.PrintingId), uppercase.Select(p => p.PrintingId));
        Assert.Equal(lowercase.Select(p => p.PrintingId), mixedcase.Select(p => p.PrintingId));
    }

    [Fact]
    public async Task Get_pages_return_all_printings_in_stable_order_without_duplicates()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
        var allPrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
            "/api/cards/printings?page=1&pageSize=500");
        Assert.NotNull(allPrintings);
        Assert.True(allPrintings!.Count > 2);

        var pagedPrintings = new List<PrintingDto>();
        for (var page = 1; page <= allPrintings.Count; page++)
        {
            var pagePrintings = await client.GetFromJsonAsync<List<PrintingDto>>(
                $"/api/cards/printings?page={page}&pageSize=2");
            Assert.NotNull(pagePrintings);
            pagedPrintings.AddRange(pagePrintings!);
            if (pagePrintings.Count < 2)
            {
                break;
            }
        }

        Assert.Equal(allPrintings.Select(p => p.PrintingId), pagedPrintings.Select(p => p.PrintingId));
        Assert.Equal(pagedPrintings.Count, pagedPrintings.Select(p => p.PrintingId).Distinct().Count());
    }

    [Fact]
    public async Task Get_with_game_and_q_and_sort_name_and_unique_cards()
    {
        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        var response = await client.GetAsync("/api/cards/printings?game=Magic&uniqueCards=true&sort=name&q=Lightning");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var printings = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        Assert.NotNull(printings);
        Assert.NotEmpty(printings);
    }

    [Fact]
    public async Task Get_with_swccg_filters()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var swccg = TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var subType = new api.Models.SubType { Game = swccg, Name = "Site" };
            db.SubTypes.Add(subType);

            var card = new api.Models.Card
            {
                Game = swccg,
                Name = "Tatooine: Mos Eisley",
                Side = "Light",
                Destiny = "0",
                Deploy = "0",
                Forfeit = "0",
                Power = 3,
                Ability = 2,
                LightSideIcons = 2,
                DarkSideIcons = 1,
            };
            card.CardSubTypes.Add(new api.Models.CardSubType { Card = card, SubType = subType });
            db.Cards.Add(card);

            var printing = new api.Models.CardPrinting
            {
                Card = card,
                CatalogSet = TreatmentLookup.UpsertSet(db, swccg, "Premiere"),
                RarityLookup = TreatmentLookup.UpsertRarity(db, swccg, "Uncommon"),
                Number = "50",
            };
            db.CardPrintings.Add(printing);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        try
        {
            var matchResponse = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&subType=Site&destiny=0&deploy=0&forfeit=0&power=3&ability=2&lightSideIcon=2&darkSideIcon=1");
            Assert.NotNull(matchResponse);
            Assert.Single(matchResponse!);
            Assert.Equal("Tatooine: Mos Eisley", matchResponse[0].CardName);

            var mismatchResponse = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&subType=Vehicle");
            Assert.NotNull(mismatchResponse);
            Assert.Empty(mismatchResponse!);
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task Get_with_persona_exact_match_does_not_partial_match_unrelated_words()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var swccg = TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var set = TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var rarity = TreatmentLookup.UpsertRarity(db, swccg, "Rare");

            var hanSolo = new api.Models.Card
            {
                Game = swccg,
                Name = "Han Solo",
                Persona = "Han",
            };
            db.Cards.Add(hanSolo);
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = hanSolo,
                CatalogSet = set,
                RarityLookup = rarity,
                Number = "101",
            });

            var falcon = new api.Models.Card
            {
                Game = swccg,
                Name = "Millennium Falcon",
                Persona = "Falcon, Han",
            };
            db.Cards.Add(falcon);
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = falcon,
                CatalogSet = set,
                RarityLookup = rarity,
                Number = "102",
            });

            var invisibleHand = new api.Models.Card
            {
                Game = swccg,
                Name = "Invisible Hand: Bridge",
                Persona = "Invisible Hand",
            };
            db.Cards.Add(invisibleHand);
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = invisibleHand,
                CatalogSet = set,
                RarityLookup = rarity,
                Number = "103",
            });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();

        try
        {
            // Searching for "Han" should match "Han Solo" and "Millennium Falcon", but NOT "Invisible Hand: Bridge"
            var hanResults = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&persona=Han");
            Assert.NotNull(hanResults);
            Assert.Equal(2, hanResults!.Count);
            Assert.Contains(hanResults, p => p.CardName == "Han Solo");
            Assert.Contains(hanResults, p => p.CardName == "Millennium Falcon");
            Assert.DoesNotContain(hanResults, p => p.CardName == "Invisible Hand: Bridge");

            // Searching for "Invisible Hand" should match only "Invisible Hand: Bridge"
            var handResults = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&persona=Invisible+Hand");
            Assert.NotNull(handResults);
            Assert.Single(handResults!);
            Assert.Equal("Invisible Hand: Bridge", handResults[0].CardName);
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task Get_with_rarity_matching_rarity_type_returns_expected_cards()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var swccg = TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var set = TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var rare1 = new api.Models.Rarity { Game = swccg, Name = "Rare 1", RarityType = "Rare" };
            var common1 = new api.Models.Rarity { Game = swccg, Name = "Common 1", RarityType = "Common" };
            db.Rarities.AddRange(rare1, common1);

            var cardRare = new api.Models.Card { Game = swccg, Name = "Obi-Wan Kenobi" };
            var cardCommon = new api.Models.Card { Game = swccg, Name = "Rebel Trooper" };
            db.Cards.AddRange(cardRare, cardCommon);

            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = cardRare,
                CatalogSet = set,
                RarityLookup = rare1,
                Number = "101",
            });
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = cardCommon,
                CatalogSet = set,
                RarityLookup = common1,
                Number = "102",
            });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
        try
        {
            var results = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&rarity=Rare");
            Assert.NotNull(results);
            Assert.Single(results!);
            Assert.Equal("Obi-Wan Kenobi", results[0].CardName);
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }

    [Fact]
    public async Task Get_with_comparison_operators_on_numeric_stats()
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var swccg = TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var set = TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var rarity = TreatmentLookup.UpsertRarity(db, swccg, "Rare");

            var weakCard = new api.Models.Card
            {
                Game = swccg,
                Name = "Jawa",
                Power = 2,
                Ability = 1,
                LightSideIcons = 0,
                DarkSideIcons = 2,
            };
            var strongCard = new api.Models.Card
            {
                Game = swccg,
                Name = "Darth Vader",
                Power = 6,
                Ability = 6,
                LightSideIcons = 3,
                DarkSideIcons = 0,
            };
            db.Cards.AddRange(weakCard, strongCard);

            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = weakCard,
                CatalogSet = set,
                RarityLookup = rarity,
                Number = "201",
            });
            db.CardPrintings.Add(new api.Models.CardPrinting
            {
                Card = strongCard,
                CatalogSet = set,
                RarityLookup = rarity,
                Number = "202",
            });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient().WithUser(TestDataSeeder.AliceUserId).AsAdmin();
        try
        {
            // Greater than
            var gtPower = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&power=>3");
            Assert.NotNull(gtPower);
            Assert.Single(gtPower!);
            Assert.Equal("Darth Vader", gtPower[0].CardName);

            // Less than
            var ltPower = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&power=<3");
            Assert.NotNull(ltPower);
            Assert.Single(ltPower!);
            Assert.Equal("Jawa", ltPower[0].CardName);

            // Greater than or equal
            var gtePower = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&power=>=6");
            Assert.NotNull(gtePower);
            Assert.Single(gtePower!);
            Assert.Equal("Darth Vader", gtePower[0].CardName);

            // Less than or equal
            var ltePower = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&power=<=2");
            Assert.NotNull(ltePower);
            Assert.Single(ltePower!);
            Assert.Equal("Jawa", ltePower[0].CardName);

            // Exact without =
            var exactPower = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&power=6");
            Assert.NotNull(exactPower);
            Assert.Single(exactPower!);
            Assert.Equal("Darth Vader", exactPower[0].CardName);

            // Ability > 2
            var gtAbility = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&ability=>2");
            Assert.NotNull(gtAbility);
            Assert.Single(gtAbility!);
            Assert.Equal("Darth Vader", gtAbility[0].CardName);

            // LightSideIcons > 1
            var gtLs = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&lightSideIcon=>1");
            Assert.NotNull(gtLs);
            Assert.Single(gtLs!);
            Assert.Equal("Darth Vader", gtLs[0].CardName);

            // DarkSideIcons > 1
            var gtDs = await client.GetFromJsonAsync<List<PrintingDto>>(
                "/api/cards/printings?game=Star+Wars+CCG&darkSideIcon=>1");
            Assert.NotNull(gtDs);
            Assert.Single(gtDs!);
            Assert.Equal("Jawa", gtDs[0].CardName);
        }
        finally
        {
            await factory.ResetDatabaseAsync();
        }
    }
}
