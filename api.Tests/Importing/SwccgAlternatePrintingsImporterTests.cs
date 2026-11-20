using System.Text;
using api.Data;
using api.Importing;
using api.Importing.Swccg;
using api.Models;
using api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Importing;

public sealed class SwccgAlternatePrintingsImporterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static async Task SeedBaseCatalogAsync(AppDbContext db)
    {
        const string json = """
        {
          "sets": [
            {
              "id": "1",
              "name": "Premiere",
              "gempName": "Premiere",
              "abbr": "P",
              "date_release": "1995-12-01"
            },
            {
              "id": "11",
              "name": "Reflections II",
              "gempName": "Reflections II",
              "abbr": "R2",
              "date_release": "2001-01-01"
            },
            {
              "id": "13",
              "name": "Theed Palace",
              "gempName": "Theed Palace",
              "abbr": "TP",
              "date_release": "2001-06-01"
            }
          ],
          "cards": [
            {
              "id": 100,
              "gempId": "1_168",
              "side": "Dark",
              "rarity": "R1",
              "set": "1",
              "front": {
                "title": "•Darth Vader",
                "type": "Character",
                "imageUrl": "https://res.starwarsccg.org/vader-front.gif"
              }
            },
            {
              "id": 200,
              "gempId": "11_55",
              "side": "Light",
              "rarity": "R",
              "set": "11",
              "front": {
                "title": "Artoo & •Threepio",
                "type": "Character",
                "imageUrl": "https://res.starwarsccg.org/artoo-front.gif"
              },
              "back": {
                "title": "Back Side",
                "imageUrl": "https://res.starwarsccg.org/artoo-back.gif"
              }
            },
            {
              "id": 300,
              "gempId": "13_25",
              "side": "Light",
              "rarity": "R",
              "set": "13",
              "front": {
                "title": "•Padmé Naberrie",
                "type": "Character",
                "imageUrl": "https://res.starwarsccg.org/padme-front.gif"
              }
            }
          ]
        }
        """;

        var jsonImporter = new SwccgJsonImporter(db, new NoopSwccgApiClient(), NullLogger<SwccgJsonImporter>.Instance);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await jsonImporter.ImportFromFileAsync(stream, new ImportOptions(DryRun: false));

        // Add non-Standard (AI) printing plus explicit number/orientation on Artoo & Threepio
        // to verify alternate CSV matching prefers the Standard printing as base metadata source.
        var artoo = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .SingleAsync(c => c.CardUid == "11_55");

        var standardPrinting = artoo.Printings.Single();
        standardPrinting.Number = "55";
        standardPrinting.ArtFrontHorizontal = true;
        standardPrinting.ArtBackHorizontal = false;

        var aiTreatment = new Treatment { GameId = artoo.GameId, Name = "Alternate Image" };
        db.Treatments.Add(aiTreatment);
        await db.SaveChangesAsync();

        db.CardPrintings.Add(new CardPrinting
        {
            Card = artoo,
            CatalogSet = standardPrinting.CatalogSet,
            Treatment = aiTreatment,
            RarityLookup = standardPrinting.RarityLookup,
            Number = "55AI",
            ImageUrl = "https://res.starwarsccg.org/artoo-ai.gif",
            IsFoil = false,
            ArtFrontHorizontal = false
        });
        await db.SaveChangesAsync();
    }

    private static SwccgAlternatePrintingsImporter CreateImporter(IServiceProvider services, AppDbContext db)
        => new(
            db,
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<IWebHostEnvironment>(),
            NullLogger<SwccgAlternatePrintingsImporter>.Instance);

    [Fact]
    public async Task ImportFromStreamsAsync_ImportsNewCardsAlternatePrintingsEncodingAliasesAndNotes()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBaseCatalogAsync(db);

        const string newCardsCsv =
            "Card Name,Side,Expansion,Card Type,Tag,Treatment,Original Set,Border,Notes,Rarity\n" +
            "Official Card List,ANA,Jedi Pack,Rules,,,Jedi Pack,WB,Insert checklist,SR\n" +
            "Dark Tournament Foil,Dark,Tournament Foils,Effect,,Tournament Foil,,BB,Promo note,TF\n";

        const string altPrintingsCsv =
            "Card Name,Side,Expansion,Card Type,Treatment,Original Set,Border,Notes,Rarity,Match Status\n" +
            "Darth Vader,Dark,Revised Unlimited,Character,,Premiere,WB,Corrected text,VR,Matched\n" +
            "Artoo & Threepio,Light,Reflections III,Character,Japanese Foil,Reflections 2,BB,Signed foil,UR,Matched\n" +
            "Padmé Naberrie,Light,First Anthology,Character,Preview,Theed Palace,WB,Preview insert,PV,Matched\n";

        var importer = CreateImporter(scope.ServiceProvider, db);

        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newCardsCsv));
        await using var altStream = new MemoryStream(Encoding.Latin1.GetBytes(altPrintingsCsv));

        var result = await importer.ImportFromStreamsAsync(newStream, altStream, dryRun: false);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(5, result.TotalRowsProcessed);
        Assert.Equal(2, result.NewCardsCreated);
        Assert.Equal(5, result.PrintingsCreated);
        Assert.Equal(0, result.SkippedExisting);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Failures);

        // Verify ANA side, empty treatment -> Standard, SR rarity, empty number, null image
        var anaCard = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .SingleAsync(c => c.Name == "Official Card List");
        Assert.Equal("ANA", anaCard.Side);
        var anaPrinting = Assert.Single(anaCard.Printings);
        Assert.Equal("Standard", anaPrinting.Treatment!.Name);
        Assert.Equal("Super Rare", anaPrinting.RarityLookup!.Name);
        Assert.Equal(string.Empty, anaPrinting.Number);
        Assert.Null(anaPrinting.ImageUrl);
        Assert.False(anaPrinting.IsFoil);
        Assert.Equal("Insert checklist", anaPrinting.PrintingNotes);

        // Verify TF / VR / UR / PV rarities and inherited metadata on Artoo & Threepio
        var artoo = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .SingleAsync(c => c.CardUid == "11_55");

        var artooAlt = artoo.Printings.Single(p => p.CatalogSet!.Name == "Reflections III");
        Assert.Equal("55", artooAlt.Number);
        Assert.Equal("https://res.starwarsccg.org/artoo-front.gif", artooAlt.ImageUrl);
        Assert.Equal("https://res.starwarsccg.org/artoo-back.gif", artooAlt.BackImageUrl);
        Assert.True(artooAlt.ArtFrontHorizontal);
        Assert.False(artooAlt.ArtBackHorizontal);
        Assert.Equal("Japanese Foil", artooAlt.Treatment!.Name);
        Assert.True(artooAlt.IsFoil);
        Assert.Equal("Ultra Rare", artooAlt.RarityLookup!.Name);
        Assert.Equal("Signed foil", artooAlt.PrintingNotes);

        // Verify Vader VR rarity and empty treatment -> Standard
        var vader = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .SingleAsync(c => c.CardUid == "1_168");
        var vaderAlt = vader.Printings.Single(p => p.CatalogSet!.Name == "Revised Unlimited");
        Assert.Equal("Standard", vaderAlt.Treatment!.Name);
        Assert.Equal("Very Rare", vaderAlt.RarityLookup!.Name);
        Assert.False(vaderAlt.IsFoil);
        Assert.Equal("Corrected text", vaderAlt.PrintingNotes);
        Assert.Equal("https://res.starwarsccg.org/vader-front.gif", vaderAlt.ImageUrl);

        // Verify Latin-1 Padmé match + Preview non-foil
        var padme = await db.Cards
            .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
            .Include(c => c.Printings).ThenInclude(p => p.Treatment)
            .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
            .SingleAsync(c => c.CardUid == "13_25");
        var padmeAlt = padme.Printings.Single(p => p.CatalogSet!.Name == "First Anthology");
        Assert.Equal("Preview", padmeAlt.Treatment!.Name);
        Assert.Equal("Preview", padmeAlt.RarityLookup!.Name);
        Assert.False(padmeAlt.IsFoil);
        Assert.Equal("Preview insert", padmeAlt.PrintingNotes);
    }

    [Fact]
    public async Task ImportFromStreamsAsync_SecondRun_IsIdempotentAndSkipsExisting()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBaseCatalogAsync(db);

        const string newCardsCsv =
            "Card Name,Side,Expansion,Card Type,Tag,Treatment,Original Set,Border,Notes,Rarity\n" +
            "Official Card List,ANA,Jedi Pack,Rules,,,Jedi Pack,WB,,XR\n";

        const string altPrintingsCsv =
            "Card Name,Side,Expansion,Card Type,Treatment,Original Set,Border,Notes,Rarity,Match Status\n" +
            "Darth Vader,Dark,Revised Unlimited,Character,,Premiere,WB,,R1,Matched\n";

        var importer = CreateImporter(scope.ServiceProvider, db);

        await importer.ImportFromStreamsAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(newCardsCsv)),
            new MemoryStream(Encoding.UTF8.GetBytes(altPrintingsCsv)),
            dryRun: false);

        var secondResult = await importer.ImportFromStreamsAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(newCardsCsv)),
            new MemoryStream(Encoding.UTF8.GetBytes(altPrintingsCsv)),
            dryRun: false);

        Assert.Equal(2, secondResult.TotalRowsProcessed);
        Assert.Equal(0, secondResult.NewCardsCreated);
        Assert.Equal(0, secondResult.PrintingsCreated);
        Assert.Equal(2, secondResult.SkippedExisting);
        Assert.Equal(0, secondResult.FailedCount);
    }

    [Fact]
    public async Task ImportFromStreamsAsync_UnmatchedRowAndDryRun_ReportsFailureDetailsAndRollsBack()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBaseCatalogAsync(db);

        const string newCardsCsv =
            "Card Name,Side,Expansion,Card Type,Tag,Treatment,Original Set,Border,Notes,Rarity\n" +
            "Dry Run New Card,Dark,Special Edition,Rules,,,Special Edition,WB,,C\n";

        const string altPrintingsCsv =
            "Card Name,Side,Expansion,Card Type,Treatment,Original Set,Border,Notes,Rarity,Match Status\n" +
            "Darth Vadr,Dark,Revised Unlimited,Character,Foil,Premiere,WB,,TF,Unmatched\n";

        var importer = CreateImporter(scope.ServiceProvider, db);
        var result = await importer.ImportFromStreamsAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(newCardsCsv)),
            new MemoryStream(Encoding.UTF8.GetBytes(altPrintingsCsv)),
            dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal("CompletedWithWarnings", result.Status);
        Assert.Equal(1, result.NewCardsCreated);
        Assert.Equal(1, result.PrintingsCreated);
        Assert.Equal(1, result.FailedCount);

        var issue = Assert.Single(result.Failures);
        Assert.Equal("SWCCG_AlternatePrintings.csv", issue.FileName);
        Assert.Equal(2, issue.RowNumber);
        Assert.Equal("Darth Vadr", issue.CardName);
        Assert.Equal("Dark", issue.Side);
        Assert.Equal("Premiere", issue.OriginalSet);
        Assert.Equal("Revised Unlimited", issue.Expansion);
        Assert.Contains("Darth Vader", issue.Reason);

        Assert.Null(await db.Cards.FirstOrDefaultAsync(c => c.Name == "Dry Run New Card"));
        Assert.Null(await db.Sets.FirstOrDefaultAsync(s => s.Name == "Revised Unlimited"));
    }

    private sealed class NoopSwccgApiClient : ISwccgApiClient
    {
        public Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default) => Task.FromResult<List<SwccgSet>>([]);
        public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default) => Task.FromResult<List<SwccgCardRecord>>([]);
        public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default) => Task.FromResult<List<SwccgCardRecord>>([]);
        public Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default) => Task.FromResult<List<SwccgCardRecord>>([]);
    }
}
