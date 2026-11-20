using api.Data;
using api.Features.Decks.Dtos;
using api.Features.Decks.Services;
using api.Importing;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Features.Decks;

public class DecklistTextImportExportTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public void LineParser_ParsesFormatsCommentsAndZoneHeaders()
    {
        const string text = """
            // MTG Commander list
            # A comment to ignore
            Commander:
            Atraxa, Praetors' Voice

            Main:
            4 Lightning Bolt
            2x Goblin Guide (ZEN) #126

            Sideboard (1):
            1 Abrade [VOW] 139

            Ideas:
            1 Sol Ring (CMD) 261
            """;

        var lines = DecklistLineParser.Parse(text);
        Assert.Equal(5, lines.Count);

        Assert.Equal("commander", lines[0].Zone);
        Assert.Equal(1, lines[0].Quantity);
        Assert.Equal("Atraxa, Praetors' Voice", lines[0].CardName);

        Assert.Equal("main", lines[1].Zone);
        Assert.Equal(4, lines[1].Quantity);
        Assert.Equal("Lightning Bolt", lines[1].CardName);

        Assert.Equal("main", lines[2].Zone);
        Assert.Equal(2, lines[2].Quantity);
        Assert.Equal("Goblin Guide", lines[2].CardName);
        Assert.Equal("ZEN", lines[2].SetCode);
        Assert.Equal("126", lines[2].CollectorNumber);

        Assert.Equal("sideboard", lines[3].Zone);
        Assert.Equal("VOW", lines[3].SetCode);
        Assert.Equal("139", lines[3].CollectorNumber);

        Assert.Equal("ideas", lines[4].Zone);
        Assert.Equal("CMD", lines[4].SetCode);
    }

    [Fact]
    public async Task DecklistImportPreviewAndApply_ResolvesExactAmbiguousUnmatchedAndZones()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        const string text = """
            // Test decklist
            Commander:
            1 Goblin Guide (Zendikar) #Z3

            Main:
            4 Lightning Bolt
            1 Unknown Card Name

            Sideboard:
            2 Lightning Bolt (Beta) B2

            Ideas:
            1 Goblin Guide
            """;

        var previewRes = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceEmptyDeckId}/import-text/preview",
            new DecklistImportPreviewRequest(text));
        previewRes.EnsureSuccessStatusCode();

        var preview = await previewRes.Content.ReadFromJsonAsync<DecklistImportPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(5, preview!.Lines.Count);
        Assert.Equal(3, preview.ResolvedCount);   // Goblin Guide (cmdr), Lightning Bolt (Beta) B2, Goblin Guide (ideas)
        Assert.Equal(1, preview.AmbiguousCount);  // Lightning Bolt (3 printings)
        Assert.Equal(1, preview.UnmatchedCount);  // Unknown Card Name

        var ambiguousLine = Assert.Single(preview.Lines, l => l.Status == "ambiguous");
        Assert.Equal(3, ambiguousLine.Candidates.Count);
        Assert.NotNull(ambiguousLine.SelectedPrintingId);

        var applyItems = preview.Lines
            .Where(l => l.SelectedPrintingId.HasValue)
            .Select(l => new DecklistImportApplyItem(l.SelectedPrintingId!.Value, l.Quantity, l.Zone))
            .ToList();

        var applyRes = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceEmptyDeckId}/import-text/apply",
            new DecklistImportApplyRequest(applyItems, ReplaceExisting: true));
        applyRes.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deckCards = await db.DeckCards
            .Where(dc => dc.DeckId == TestDataSeeder.AliceEmptyDeckId)
            .ToListAsync();

        var guide = Assert.Single(deckCards, c => c.CardPrintingId == TestDataSeeder.GoblinGuidePrintingId);
        Assert.Equal(1, guide.QuantityInDeck);
        Assert.Equal(1, guide.QuantityIdea);
        Assert.True(guide.IsCommander);

        var betaBolt = Assert.Single(deckCards, c => c.CardPrintingId == TestDataSeeder.LightningBoltBetaPrintingId);
        Assert.Equal(2, betaBolt.QuantitySideboard);
    }

    [Fact]
    public async Task DecklistImportAndExport_SupportsAllFourGamesIncludingSwuSwccgAndPokemon()
    {
        await factory.ResetDatabaseAsync();
        int swuDeckId;
        int swccgDeckId;
        int pokemonDeckId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var swu = await db.Games.SingleAsync(g => g.Name == "Star Wars Unlimited");
            var swccg = TreatmentLookup.GetOrAddGame(db, "Star Wars CCG");
            var pokemon = TreatmentLookup.GetOrAddGame(db, "Pokemon TCG");

            var swuSet = TreatmentLookup.UpsertSet(db, swu, "SOR");
            var leaderType = new CardType { Game = swu, Name = "Leader" };
            var baseType = new CardType { Game = swu, Name = "Base" };
            var lukeLeader = new Card { Game = swu, Name = "Luke Skywalker", Subtitle = "Faithful Friend" };
            lukeLeader.CardTypes.Add(new CardCardType { Card = lukeLeader, CardType = leaderType, SourceRole = "Type" });
            var jediBase = new Card { Game = swu, Name = "Administrator's Tower" };
            jediBase.CardTypes.Add(new CardCardType { Card = jediBase, CardType = baseType, SourceRole = "Type" });
            db.CardPrintings.AddRange(
                new CardPrinting { Card = lukeLeader, CatalogSet = swuSet, Number = "005" },
                new CardPrinting { Card = jediBase, CatalogSet = swuSet, Number = "024" });

            var swccgSet = TreatmentLookup.UpsertSet(db, swccg, "Premiere");
            var vader = new Card { Game = swccg, Name = "•Darth Vader" };
            db.CardPrintings.Add(new CardPrinting { Card = vader, CatalogSet = swccgSet, Number = "1" });

            var pkmSet = TreatmentLookup.UpsertSet(db, pokemon, "SVI");
            var pikachu = new Card { Game = pokemon, Name = "Pikachu" };
            db.CardPrintings.Add(new CardPrinting { Card = pikachu, CatalogSet = pkmSet, Number = "25" });

            var swuDeck = new Deck { Game = swu.Name, Name = "Luke Premier" };
            var swccgDeck = new Deck { Game = swccg.Name, Name = "Dark Mains" };
            var pokemonDeck = new Deck { Game = pokemon.Name, Name = "Pika Deck" };
            db.Decks.AddRange(swuDeck, swccgDeck, pokemonDeck);
            await db.SaveChangesAsync();

            swuDeckId = swuDeck.Id;
            swccgDeckId = swccgDeck.Id;
            pokemonDeckId = pokemonDeck.Id;
        }

        using var client = factory.CreateClient();

        // 1. SWU
        var swuPreview = await (await client.PostAsJsonAsync(
            $"/api/decks/{swuDeckId}/import-text/preview",
            new DecklistImportPreviewRequest("Leader:\nLuke Skywalker, Faithful Friend\nBase:\nAdministrator's Tower"))).Content
            .ReadFromJsonAsync<DecklistImportPreviewResponse>();
        Assert.Equal(2, swuPreview!.ResolvedCount);
        var swuApply = await client.PostAsJsonAsync(
            $"/api/decks/{swuDeckId}/import-text/apply",
            new DecklistImportApplyRequest(swuPreview.Lines.Select(l => new DecklistImportApplyItem(l.SelectedPrintingId!.Value, l.Quantity, l.Zone)).ToList()));
        swuApply.EnsureSuccessStatusCode();

        var swuExport = await client.GetFromJsonAsync<DecklistExportTextResponse>($"/api/decks/{swuDeckId}/export-text");
        Assert.Contains("Leader:", swuExport!.Text);
        Assert.Contains("1 Luke Skywalker, Faithful Friend (SOR) 005", swuExport.Text);
        Assert.Contains("Base:", swuExport.Text);

        // 2. SWCCG bullet-normalized + Starting section
        var swccgPreview = await (await client.PostAsJsonAsync(
            $"/api/decks/{swccgDeckId}/import-text/preview",
            new DecklistImportPreviewRequest("Starting:\n1x Darth Vader (Premiere) #1"))).Content
            .ReadFromJsonAsync<DecklistImportPreviewResponse>();
        Assert.Equal(1, swccgPreview!.ResolvedCount);
        var swccgApply = await client.PostAsJsonAsync(
            $"/api/decks/{swccgDeckId}/import-text/apply",
            new DecklistImportApplyRequest(swccgPreview.Lines.Select(l => new DecklistImportApplyItem(l.SelectedPrintingId!.Value, l.Quantity, l.Zone)).ToList()));
        swccgApply.EnsureSuccessStatusCode();

        var swccgExport = await client.GetFromJsonAsync<DecklistExportTextResponse>($"/api/decks/{swccgDeckId}/export-text");
        Assert.Contains("Starting:", swccgExport!.Text);
        Assert.Contains("1 •Darth Vader (Premiere) 1", swccgExport.Text);

        // 3. Pokémon TCG Live format (4 Pikachu SVI 025)
        var pkmPreview = await (await client.PostAsJsonAsync(
            $"/api/decks/{pokemonDeckId}/import-text/preview",
            new DecklistImportPreviewRequest("Pokemon:\n4 Pikachu SVI 025"))).Content
            .ReadFromJsonAsync<DecklistImportPreviewResponse>();
        Assert.Equal(1, pkmPreview!.ResolvedCount);
        var pkmApply = await client.PostAsJsonAsync(
            $"/api/decks/{pokemonDeckId}/import-text/apply",
            new DecklistImportApplyRequest(pkmPreview.Lines.Select(l => new DecklistImportApplyItem(l.SelectedPrintingId!.Value, l.Quantity, l.Zone)).ToList()));
        pkmApply.EnsureSuccessStatusCode();

        var pkmExport = await client.GetFromJsonAsync<DecklistExportTextResponse>($"/api/decks/{pokemonDeckId}/export-text");
        Assert.Contains("4 Pikachu (SVI) 25", pkmExport!.Text);
    }

    [Fact]
    public async Task DecklistImportPreview_WithEmptyText_ReturnsValidationProblem()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync(
            $"/api/decks/{TestDataSeeder.AliceMagicDeckId}/import-text/preview",
            new DecklistImportPreviewRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
