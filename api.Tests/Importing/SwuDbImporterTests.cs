using api.Data;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace api.Tests.Importing;

/// <summary>
/// Tests for <see cref="SwuDbImporter"/> using the Strapi-format JSON that the
/// official Star Wars: Unlimited API returns from admin.starwarsunlimited.com/api/card-list.
/// </summary>
public sealed class SwuDbImporterTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Game = "Star Wars Unlimited";

    // ─── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal Strapi-format JSON string representing a single card record.
    /// </summary>
    private static string BuildStrapiJson(
        int id,
        string title,
        string? serialCode,
        string? cardUid,
        int? cardNumber,
        string expansionCode,
        string typeName,
        string rarity,
        bool foil,
        string? imageUrl,
        string? text = null,
        string? subtitle = null,
        string createdAt = "2025-08-15T18:29:41.633Z",
        string updatedAt = "2025-11-10T16:07:21.000Z",
        string publishedAt = "2025-08-15T18:30:00.000Z",
        object? variantOf = null,
        object? reprintOf = null)
    {
        object? artFrontData = imageUrl is not null
            ? (object)new { id = 1, attributes = new { url = imageUrl, formats = (object?)null } }
            : null;

        var record = new
        {
            id,
            attributes = new
            {
                title,
                subtitle,
                cardUid,
                serialCode,
                locale = "en",
                cardNumber,
                rarity,
                text,
                artist = "Test Artist",
                cost = (int?)3,
                power = (int?)2,
                health = (int?)4,
                arena = (string?)null,
                aspects = (string[]?)null,
                traits = (string[]?)null,
                keywords = (string[]?)null,
                createdAt,
                updatedAt,
                publishedAt,
                type = new { data = new { id = 4, attributes = new { name = typeName, value = typeName } } },
                expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = expansionCode } } },
                variantTypes = new
                {
                    data = new[]
                    {
                        new { id = foil ? 51 : 46, attributes = new { name = foil ? "Foil" : "Standard", variantId = foil ? "02" : "01", foil } }
                    }
                },
                variantOf = variantOf ?? new { data = (object?)null },
                reprintOf = reprintOf ?? new { data = (object?)null },
                artFront = new { data = artFrontData },
                artBack = new { data = (object?)null }
            }
        };

        return JsonSerializer.Serialize(new { data = new[] { record }, meta = new { pagination = new { page = 1, pageSize = 100, pageCount = 1, total = 1 } } });
    }

    private static Stream ToStream(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static SwuDbImporter CreateImporter(AppDbContext db, ISWUApiClient? client = null)
        => new(db, client ?? new StubSWUApiClient(), NullLogger<SwuDbImporter>.Instance);

    // ─── tests ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""{"data":{"id":22,"attributes":{"name":"Special"}}}""", "Special")]
    [InlineData("\"Rare\"", "Rare")]
    [InlineData(null, "Unknown")]
    [InlineData("null", "Unknown")]
    [InlineData("""{"data":null}""", "Unknown")]
    [InlineData("""{"data":{"id":22,"attributes":{"name":null}}}""", "Unknown")]
    public async Task ImportFromFileAsync_WithRarity_PersistsNameAndPreservesDetails(string? rarityJson, string expected)
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);
        var json = BuildStrapiJson(39380, "Rarity Card", "06010001", "1020365882", 1,
            "SOR", "Leader", "Common", false, "https://example.invalid/card.png", text: "Card text.");
        var page = JsonNode.Parse(json)!;
        var attributes = page["data"]![0]!["attributes"]!.AsObject();
        if (rarityJson is null)
            attributes.Remove("rarity");
        else
            attributes["rarity"] = JsonNode.Parse(rarityJson);
        var options = new ImportOptions(DryRun: false, SetCode: "SOR");

        var created = await importer.ImportFromFileAsync(ToStream(page.ToJsonString()), options);

        Assert.Equal(0, created.Errors);
        Assert.Equal(1, created.CardsCreated);
        Assert.Equal(1, created.PrintingsCreated);
        var card = await db.Cards.SingleAsync(c => c.Game!.Name == Game);
        var printing = await db.CardPrintings.SingleAsync(p => p.CardId == card.Id);
        Assert.Equal(expected, printing.RarityLookup!.Name);

        attributes["rarity"] = JsonNode.Parse("""{"data":{"id":23,"attributes":{"name":"Legendary"}}}""");
        var updated = await importer.ImportFromFileAsync(ToStream(page.ToJsonString()), options);

        Assert.Equal(0, updated.Errors);
        Assert.Equal(0, updated.CardsCreated);
        Assert.Equal(0, updated.CardsUpdated);
        Assert.Equal(0, updated.PrintingsCreated);
        Assert.Equal(1, updated.PrintingsUpdated);
        db.ChangeTracker.Clear();
        card = await db.Cards.SingleAsync(c => c.Game!.Name == Game);
        printing = await db.CardPrintings.Include(p => p.CatalogSet).Include(p => p.RarityLookup)
            .SingleAsync(p => p.CardId == card.Id);
        Assert.Equal("Leader", await db.CardCardTypes.Where(association =>
                association.CardId == card.Id && association.SourceRole == "Type")
            .Select(association => association.CardType.Name).SingleAsync());
        Assert.Equal("Card text.", card.Text);
        Assert.Equal("Legendary", printing.RarityLookup!.Name);
        Assert.Equal("SOR", printing.CatalogSet!.Code);
        Assert.Equal("1", printing.Number);
        Assert.Equal("Standard", (await db.Treatments.SingleAsync(t => t.Id == printing.TreatmentId)).Name);
        Assert.Equal("https://example.invalid/card.png", printing.ImageUrl);
        Assert.Equal("Legendary", (await db.Rarities.SingleAsync(r => r.Id == printing.RarityId)).Name);
    }

    [Fact]
    public async Task ImportFromFileAsync_Creates_Card_And_Printing_From_Strapi_Json()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 39380,
            title: "Chancellor Palpatine",
            serialCode: "06010001",
            cardUid: "1020365882",
            cardNumber: 1,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/06010001_EN.png",
            text: "Deploy: exhaust this leader.");
        var payload = JsonNode.Parse(json)!;
        payload["data"]![0]!["attributes"]!["variantTypes"] = null;

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(payload.ToJsonString()), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.PrintingsUpdated);

        var card = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine" && c.Game!.Name == Game);
        Assert.Equal("Leader", await db.CardCardTypes.Where(association =>
                association.CardId == card.Id && association.SourceRole == "Type")
            .Select(association => association.CardType.Name).SingleAsync());
        Assert.Equal("Deploy: exhaust this leader.", card.Text);
        Assert.NotNull(card.LastSyncedAt);

        var printing = await db.CardPrintings.Include(p => p.CatalogSet).Include(p => p.RarityLookup)
            .SingleAsync(p => p.CardUid == "1020365882");
        Assert.Equal(card.Id, printing.CardId);
        Assert.Equal("SOR", printing.CatalogSet!.Code);
        Assert.Equal("Legendary", printing.RarityLookup!.Name);
        Assert.Equal("1", printing.Number);
        Assert.Equal("Standard", (await db.Treatments.SingleAsync(t => t.Id == printing.TreatmentId)).Name);
        Assert.Equal("https://cdn.starwarsunlimited.com/06010001_EN.png", printing.ImageUrl);
    }

    [Fact]
    public async Task ImportFromFileAsync_Creates_Foil_Printing_When_VariantType_Foil_True()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 39381,
            title: "Luke Skywalker",
            serialCode: "06020002",
            cardUid: "2579145458",
            cardNumber: 2,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Rare",
            foil: true,
            imageUrl: "https://cdn.starwarsunlimited.com/06020002_EN_foil.png");

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.PrintingsCreated);

        var printing = await db.CardPrintings.SingleAsync(p => p.CardUid == "2579145458");
        Assert.Equal("Foil", (await db.Treatments.SingleAsync(t => t.Id == printing.TreatmentId)).Name);
    }

    [Fact]
    public async Task ImportFromFileAsync_Updates_Existing_Card_And_Printing()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // First import — create initial record.
        var initial = BuildStrapiJson(
            id: 39380,
            title: "Grand Moff Tarkin",
            serialCode: "06010003",
            cardUid: "9988776655",
            cardNumber: 3,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/v1.png",
            text: "Original text.");

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary1 = await importer.ImportFromFileAsync(ToStream(initial), options);
        Assert.Equal(1, summary1.CardsCreated);
        Assert.Equal(1, summary1.PrintingsCreated);

        // Second import — updated text, image, and rarity.
        var updated = BuildStrapiJson(
            id: 39380,
            title: "Grand Moff Tarkin",
            serialCode: "06010003",
            cardUid: "9988776655",
            cardNumber: 3,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Uncommon",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/v2.png",
            text: "Updated text.");

        var summary2 = await importer.ImportFromFileAsync(ToStream(updated), options);
        Assert.Equal(0, summary2.Errors);
        Assert.Equal(0, summary2.CardsCreated);
        Assert.Equal(1, summary2.CardsUpdated);
        Assert.Equal(0, summary2.PrintingsCreated);
        Assert.Equal(1, summary2.PrintingsUpdated);

        var card = await db.Cards.SingleAsync(c => c.Name == "Grand Moff Tarkin" && c.Game!.Name == Game);
        Assert.Equal("Updated text.", card.Text);
        Assert.NotNull(card.LastSyncedAt);

        var printing = await db.CardPrintings.SingleAsync(p => p.CardUid == "9988776655");
        Assert.Equal("Uncommon", printing.RarityLookup!.Name);
        Assert.Equal("https://cdn.starwarsunlimited.com/v2.png", printing.ImageUrl);
    }

    [Fact]
    public async Task ImportFromFileAsync_DryRun_Does_Not_Persist_Records()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 11111,
            title: "Dry Run Card",
            serialCode: "DR000001",
            cardUid: "1111111111",
            cardNumber: 99,
            expansionCode: "SOR",
            typeName: "Event",
            rarity: "Common",
            foil: false,
            imageUrl: null);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.True(summary.DryRun);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);

        // Nothing should be in the DB.
        Assert.False(await db.Cards.AnyAsync(c => c.Name == "Dry Run Card"));
    }

    [Fact]
    public async Task ImportFromFileAsync_Skips_NonEnglish_Records_And_Adds_Warning()
    {
        // Records whose attributes.locale is not "en" must be skipped; no card or
        // printing should be written to the database, and the summary must contain a warning message.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // Build a Strapi response that contains two records: one Italian (should be skipped)
        // and one English (should be imported normally).
        var records = new[]
        {
            new
            {
                id = 77001,
                attributes = new
                {
                    title = "Scheda Italiana",
                    subtitle = (string?)null,
                    cardUid = "IT00000001",
                    serialCode = "IT000001",
                    locale = "it",           // non-English — must be skipped
                    cardNumber = 1,
                    rarity = "Common",
                    text = (string?)null,
                    artist = (string?)null,
                    cost = (int?)1,
                    power = (int?)null,
                    health = (int?)null,
                    arena = (string?)null,
                    aspects = (string[]?)null,
                    traits = (string[]?)null,
                    keywords = (string[]?)null,
                    updatedAt = "2025-11-10T16:07:21.000Z",
                    type = new { data = new { id = 1, attributes = new { name = "Unit", value = "Unit" } } },
                    expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                    variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                    variantOf = new { data = (object?)null },
                    reprintOf = new { data = (object?)null },
                    artFront = new { data = (object?)null },
                    artBack = new { data = (object?)null }
                }
            },
            new
            {
                id = 77002,
                attributes = new
                {
                    title = "English Card",
                    subtitle = (string?)null,
                    cardUid = "EN00000002",
                    serialCode = "EN000002",
                    locale = "en",           // English — must be imported
                    cardNumber = 2,
                    rarity = "Rare",
                    text = (string?)null,
                    artist = (string?)null,
                    cost = (int?)2,
                    power = (int?)null,
                    health = (int?)null,
                    arena = (string?)null,
                    aspects = (string[]?)null,
                    traits = (string[]?)null,
                    keywords = (string[]?)null,
                    updatedAt = "2025-11-10T16:07:21.000Z",
                    type = new { data = new { id = 1, attributes = new { name = "Unit", value = "Unit" } } },
                    expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                    variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                    variantOf = new { data = (object?)null },
                    reprintOf = new { data = (object?)null },
                    artFront = new { data = (object?)null },
                    artBack = new { data = (object?)null }
                }
            }
        };

        var json = JsonSerializer.Serialize(new
        {
            data = records,
            meta = new { pagination = new { page = 1, pageSize = 100, pageCount = 1, total = 2 } }
        });

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        // Only the English card should be created.
        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);

        // The Italian card must not appear in the database.
        Assert.False(await db.Cards.AnyAsync(c => c.Name == "Scheda Italiana" && c.Game!.Name == Game));
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "English Card" && c.Game!.Name == Game));

        // A warning message about the skipped record must be present in the summary.
        Assert.Contains(summary.Messages, m => m.Contains("77001") && m.Contains("it"));
    }

    [Fact]
    public async Task ImportFromFileAsync_UsesExpansionCode_AsSet()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 55001,
            title: "Han Solo",
            serialCode: "07010001",
            cardUid: "1234567890",
            cardNumber: 1,
            expansionCode: "SHD",
            typeName: "Unit",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/07010001_EN.png");

        var options = new ImportOptions(DryRun: false, SetCode: "SHD");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.PrintingsCreated);

        var printing = await db.CardPrintings.Include(p => p.CatalogSet)
            .SingleAsync(p => p.CardUid == "1234567890");
        Assert.Equal("SHD", printing.CatalogSet!.Code);
    }

    [Fact]
    public async Task ImportFromFileAsync_Bare_Array_Format_Is_Accepted()
    {
        // The importer accepts a plain JSON array (not wrapped in a Strapi page envelope)
        // for backwards-compatibility with file imports.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var record = new
        {
            id = 42000,
            attributes = new
            {
                title = "Bare Array Card",
                subtitle = (string?)null,
                cardUid = "7777777777",
                serialCode = "BARE0001",
                locale = "en",
                cardNumber = 42,
                rarity = "Common",
                text = (string?)null,
                artist = (string?)null,
                cost = (int?)1,
                power = (int?)null,
                health = (int?)null,
                arena = (string?)null,
                aspects = (string[]?)null,
                traits = (string[]?)null,
                keywords = (string[]?)null,
                updatedAt = "2025-12-01T00:00:00.000Z",
                type = new { data = new { id = 1, attributes = new { name = "Unit", value = "Unit" } } },
                expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                variantOf = new { data = (object?)null },
                reprintOf = new { data = (object?)null },
                artFront = new { data = (object?)null },
                artBack = new { data = (object?)null }
            }
        };

        // Bare array — no "data"/"meta" envelope.
        var bareArrayJson = JsonSerializer.Serialize(new[] { record });
        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(bareArrayJson), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Bare Array Card" && c.Game!.Name == Game));
    }

    [Fact]
    public async Task ImportFromFileAsync_Card_Without_SerialCode_Uses_Set_And_Number_As_Key()
    {
        // When serialCode is absent, the printing is keyed on set+cardNumber.
        // A re-import of the same data should update the existing printing rather
        // than creating a duplicate.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 55000,
            title: "No Serial Card",
            serialCode: null,
            cardUid: "8888888888",
            cardNumber: 7,
            expansionCode: "TWI",
            typeName: "Event",
            rarity: "Common",
            foil: false,
            imageUrl: null);

        var options = new ImportOptions(DryRun: false, SetCode: "TWI");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);

        // Number should be the cardNumber string when serialCode is null.
        var printing = await db.CardPrintings.Include(p => p.CatalogSet)
            .SingleAsync(p => p.CatalogSet!.Code == "TWI" && p.Number == "7");
        Assert.Equal("TWI", printing.CatalogSet!.Code);
        Assert.Equal("7", printing.Number);

        // Re-import identical data — should detect the existing printing by set+number and not create a new one.
        var summary2 = await importer.ImportFromFileAsync(ToStream(json), options);
        Assert.Equal(0, summary2.Errors);
        Assert.Equal(0, summary2.CardsCreated);
        Assert.Equal(0, summary2.PrintingsCreated);
        Assert.Equal(1, await db.CardPrintings.CountAsync(p => p.CatalogSet!.Code == "TWI" && p.Number == "7"));
    }

    [Fact]
    public async Task ImportFromFileAsync_MultipleCards_DifferentExpansions_StoredWithCorrectSetCodes()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // First card from SOR
        var sorJson = BuildStrapiJson(
            id: 60001,
            title: "Darth Vader",
            serialCode: "06030001",
            cardUid: "1111111111",
            cardNumber: 3,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/06030001_EN.png");

        await importer.ImportFromFileAsync(ToStream(sorJson), new ImportOptions(DryRun: false, SetCode: "SOR"));

        // Second card from SHD
        var shdJson = BuildStrapiJson(
            id: 60002,
            title: "Princess Leia",
            serialCode: "07020002",
            cardUid: "2222222222",
            cardNumber: 2,
            expansionCode: "SHD",
            typeName: "Unit",
            rarity: "Rare",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/07020002_EN.png");

        await importer.ImportFromFileAsync(ToStream(shdJson), new ImportOptions(DryRun: false, SetCode: "SHD"));

        var sorPrinting = await db.CardPrintings.Include(p => p.CatalogSet)
            .SingleAsync(p => p.CardUid == "1111111111");
        Assert.Equal("SOR", sorPrinting.CatalogSet!.Code);

        var shdPrinting = await db.CardPrintings.Include(p => p.CatalogSet)
            .SingleAsync(p => p.CardUid == "2222222222");
        Assert.Equal("SHD", shdPrinting.CatalogSet!.Code);
    }

    [Fact]
    public async Task ImportFromFileAsync_MissingExpansion_FallsBackToUnknownSet()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // Manually build a record with no expansion data.
        var record = new
        {
            data = new[]
            {
                new
                {
                    id = 99001,
                    attributes = new
                    {
                        title = "Mystery Card",
                        subtitle = (string?)null,
                        cardUid = (string?)null,
                        serialCode = (string?)null,
                        locale = "en",
                        cardNumber = 99,
                        rarity = "Common",
                        text = (string?)null,
                        artist = "Unknown",
                        cost = (int?)1,
                        power = (int?)1,
                        health = (int?)1,
                        arena = (string?)null,
                        aspects = (string[]?)null,
                        traits = (string[]?)null,
                        keywords = (string[]?)null,
                        updatedAt = "2025-01-01T00:00:00.000Z",
                        type = new { data = new { id = 1, attributes = new { name = "Unit", value = "Unit" } } },
                        expansion = new { data = (object?)null },
                        variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                        variantOf = new { data = (object?)null },
                        reprintOf = new { data = (object?)null },
                        artFront = new { data = (object?)null },
                        artBack = new { data = (object?)null }
                    }
                }
            },
            meta = new { pagination = new { page = 1, pageSize = 100, pageCount = 1, total = 1 } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(record);
        var options = new ImportOptions(DryRun: false, SetCode: null);
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.PrintingsCreated);

        // When expansion is missing, set falls back to "UNK" and number is used as key.
        var printing = await db.CardPrintings.SingleAsync(p => p.Card.Name == "Mystery Card" && p.Card.Game!.Name == Game);
        Assert.Equal("UNK", printing.CatalogSet!.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportFromFileAsync_Stores_Rich_Attributes_In_NormalizedCatalog(bool relationships)
    {
        // The importer stores optional game-specific fields (aspects, traits, keywords,
        // subtitle, arena, cost, power, health, artist) in the Card's DetailsJson blob
        // so they are available to the front-end without requiring dedicated columns.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var record = new
        {
            id = 66000,
            attributes = new
            {
                title = "Rich Card",
                subtitle = "The Rich One",
                cardUid = "9999999999",
                serialCode = "RICH0001",
                locale = "en",
                cardNumber = 10,
                rarity = "Rare",
                text = "Rich text here.",
                artist = "Jane Doe",
                cost = (int?)5,
                power = (int?)3,
                health = (int?)7,
                arena = "Ground",
                aspects = NameList("Aggression", "Command"),
                traits = NameList("Imperial", "Clone Trooper"),
                keywords = NameList("Sentinel", "Grit"),
                updatedAt = "2025-11-10T00:00:00.000Z",
                type = new { data = new { id = 3, attributes = new { name = "Unit", value = "Unit" } } },
                expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                variantOf = new { data = (object?)null },
                reprintOf = new { data = (object?)null },
                artFront = new { data = (object?)null },
                artBack = new { data = (object?)null }
            }
        };

        var json = JsonSerializer.Serialize(new
        {
            data = new[] { record },
            meta = new { pagination = new { page = 1, pageSize = 100, pageCount = 1, total = 1 } }
        });

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);

        var card = await db.Cards.SingleAsync(c => c.Name == "Rich Card \u2014 The Rich One" && c.Game!.Name == Game);
        Assert.Equal("The Rich One", card.Subtitle);
        Assert.Equal("Jane Doe", (await db.CardPrintings.SingleAsync(p => p.CardId == card.Id)).Artist);
        Assert.Equal(3, card.Power);
        Assert.Equal(7, card.Hp);
        Assert.Equal(5, card.Cost);
        Assert.Equal("Ground", Assert.Single(card.CardArenas).Arena.Name);
        Assert.Equal(new[] { "Aggression", "Command" },
            card.CardAspects.Select(x => x.Aspect.Name));
        Assert.Equal(new[] { "Imperial", "Clone Trooper" },
            card.CardTraits.Select(x => x.Trait.Name));
        Assert.Equal(new[] { "Sentinel", "Grit" },
            card.CardKeywords.Select(x => x.Keyword.Name));
        var printing = await db.CardPrintings.SingleAsync(p => p.CardId == card.Id);
        Assert.Equal("SOR", printing.CatalogSet!.Code);
        Assert.Equal("Rare", printing.RarityLookup!.Name);

        object NameList(params string[] names) => relationships
            ? new { data = names.Select((name, index) => new { id = index + 1, attributes = new { name } }).ToArray() }
            : names;
    }

    [Fact]
    public async Task ImportFromFileAsync_SameTitle_DifferentSubtitle_Creates_Separate_Cards()
    {
        // Cards that share a title but differ by subtitle must be stored as distinct Card rows.
        // Previously the second import would overwrite the first card because only (Game, Name)
        // was used as the upsert key, and Name was set to Title alone.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // First card: "Chancellor Palpatine — Galactic Emperor"
        var json1 = BuildStrapiJson(
            id: 70001,
            title: "Chancellor Palpatine",
            subtitle: "Galactic Emperor",
            serialCode: "SOR-CP-001",
            cardUid: "CP001",
            cardNumber: 1,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/CP001.png",
            text: "The emperor's text.");

        // Second card: "Chancellor Palpatine — How Liberty Dies"
        var json2 = BuildStrapiJson(
            id: 70002,
            title: "Chancellor Palpatine",
            subtitle: "How Liberty Dies",
            serialCode: "SOR-CP-002",
            cardUid: "CP002",
            cardNumber: 2,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/CP002.png",
            text: "How liberty dies text.");

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");

        var summary1 = await importer.ImportFromFileAsync(ToStream(json1), options);
        Assert.Equal(0, summary1.Errors);
        Assert.Equal(1, summary1.CardsCreated);
        Assert.Equal(1, summary1.PrintingsCreated);

        var summary2 = await importer.ImportFromFileAsync(ToStream(json2), options);
        Assert.Equal(0, summary2.Errors);
        Assert.Equal(1, summary2.CardsCreated);
        Assert.Equal(1, summary2.PrintingsCreated);

        // Two distinct Card rows must exist — one per subtitle.
        var card1 = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine \u2014 Galactic Emperor" && c.Game!.Name == Game);
        var card2 = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine \u2014 How Liberty Dies" && c.Game!.Name == Game);
        Assert.NotEqual(card1.Id, card2.Id);

        // Each card's text must be its own, not overwritten by the second import.
        Assert.Equal("The emperor's text.", card1.Text);
        Assert.Equal("How liberty dies text.", card2.Text);

        // Each printing must point to its own card.
        var printing1 = await db.CardPrintings.SingleAsync(p => p.CardUid == "CP001");
        var printing2 = await db.CardPrintings.SingleAsync(p => p.CardUid == "CP002");
        Assert.Equal(card1.Id, printing1.CardId);
        Assert.Equal(card2.Id, printing2.CardId);
    }

    [Fact]
    public async Task ImportFromFileAsync_Stores_RecordLifecycleDates_In_TypedColumns()
    {
        // Verifies that the three Strapi timestamp fields (createdAt, updatedAt, publishedAt)
        // are correctly deserialized from the API response.  The importer persists createdAt
        // and updatedAt inside printingJson so callers can inspect them without a second API call.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        const string expectedCreatedAt = "2025-08-15T18:29:41.633Z";
        const string expectedUpdatedAt = "2025-11-10T16:07:21.000Z";

        var json = BuildStrapiJson(
            id: 50001,
            title: "Darth Vader",
            serialCode: "06010010",
            cardUid: "3344556677",
            cardNumber: 10,
            expansionCode: "SOR",
            typeName: "Leader",
            rarity: "Legendary",
            foil: false,
            imageUrl: null,
            createdAt: expectedCreatedAt,
            updatedAt: expectedUpdatedAt,
            publishedAt: "2025-08-15T18:30:00.000Z");

        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false, SetCode: "SOR"));

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.PrintingsCreated);

        var printing = await db.CardPrintings.SingleAsync(p => p.CardUid == "3344556677");
        Assert.Equal(DateTimeOffset.Parse(expectedCreatedAt), printing.ApiCreatedAt);
        Assert.Equal(DateTimeOffset.Parse(expectedUpdatedAt), printing.ApiUpdatedAt);
        Assert.Equal(DateTimeOffset.Parse("2025-08-15T18:30:00.000Z"), printing.ApiPublishedAt);
        Assert.Null(printing.CatalogSet!.ReleasedAt);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_WithoutUpdatedSince_Uses_DefaultSort_By_UpdatedAt()
    {
        // When no UpdatedSince filter is provided the importer must forward null UpdatedSince
        // to the API client. The updatedAt:asc sort is the API client's responsibility.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cards = ParseRecords(BuildStrapiJson(
            id: 60001, title: "Obi-Wan Kenobi", serialCode: "06010020", cardUid: "9988001122",
            cardNumber: 20, expansionCode: "SOR", typeName: "Unit", rarity: "Rare", foil: false, imageUrl: null));

        var client = new CapturingSWUApiClient(cards, expansionId: 2);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR");
        await importer.ImportFromRemoteAsync(options);

        // UpdatedSince must be null when the option was not provided.
        Assert.NotNull(client.CapturedFilter);
        Assert.Null(client.CapturedFilter.UpdatedSince);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_WithUpdatedSince_Includes_DateFilter_In_QueryString()
    {
        // When UpdatedSince is set the importer must forward it to the API client via the filter
        // so the API only returns cards modified after the given point in time.
        // This is the reliable strategy for incremental update detection (Task 2.4).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cards = ParseRecords(BuildStrapiJson(
            id: 60002, title: "Han Solo", serialCode: "06010021", cardUid: "1122334455",
            cardNumber: 21, expansionCode: "SOR", typeName: "Unit", rarity: "Common", foil: false, imageUrl: null));

        var since = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var client = new CapturingSWUApiClient(cards, expansionId: 2);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR", UpdatedSince: since);
        await importer.ImportFromRemoteAsync(options);

        // UpdatedSince must be forwarded exactly as provided.
        Assert.NotNull(client.CapturedFilter);
        Assert.Equal(since, client.CapturedFilter.UpdatedSince);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Pages_Through_Multiple_Pages()
    {
        // The importer must process all cards returned by the API client (which handles
        // pagination internally) and persist each one.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // API client returns two cards (pagination is the client's concern).
        var cards = new List<StrapiRecord>();
        cards.AddRange(ParseRecords(BuildStrapiPage(page: 1, pageCount: 1, id: 71000, title: "Page One Card", serialCode: "PG010001")));
        cards.AddRange(ParseRecords(BuildStrapiPage(page: 1, pageCount: 1, id: 71001, title: "Page Two Card", serialCode: "PG020001")));

        var client = new StubSWUApiClient(cards);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(2, summary.CardsCreated);
        Assert.Equal(2, summary.PrintingsCreated);
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Page One Card" && c.Game!.Name == Game));
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Page Two Card" && c.Game!.Name == Game));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImportFromRemoteAsync_WithoutSetCode_UsesUnfilteredFeed(string? setCode)
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new CapturingSWUApiClient();
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: false, SetCode: setCode);
        var summary = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, summary.Errors);
        Assert.NotNull(client.CapturedFilter);
        Assert.Null(client.CapturedFilter.ExpansionCode);
        Assert.Null(client.CapturedFilter.ExpansionId);
        Assert.Null(client.CapturedFilter.UpdatedSince);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Propagates_HttpRequestException_On_Http_Error()
    {
        // When the API client throws HttpRequestException, the importer must propagate it unchanged.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new ThrowingSWUApiClient(
            new HttpRequestException("server error", null, HttpStatusCode.InternalServerError));
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        await Assert.ThrowsAsync<HttpRequestException>(() => importer.ImportFromRemoteAsync(options));
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Respects_Limit_Across_Multiple_Pages()
    {
        // When Limit is set the importer must stop after processing that many records
        // even when the API client returns more.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 10 cards total; Limit=3 must stop after 3 upserts.
        var cards = new List<StrapiRecord>();
        cards.AddRange(ParseRecords(BuildStrapiPageWithMultipleRecords(page: 1, pageCount: 2, startId: 82000, count: 5)));
        cards.AddRange(ParseRecords(BuildStrapiPageWithMultipleRecords(page: 2, pageCount: 2, startId: 82005, count: 5)));

        var client = new StubSWUApiClient(cards);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR", Limit: 3);
        var summary = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(3, summary.CardsCreated);
        Assert.Equal(3, summary.PrintingsCreated);
    }

    [Fact]
    public async Task ImportFromFileAsync_Stores_VariantOf_And_ReprintOf_In_DetailsJson()
    {
        // The importer persists CardUid-based relationship data without a schema change.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var variantOfData = new { data = new { id = 12345, attributes = new { title = "Luke Skywalker", cardUid = "9000000001" } } };
        var reprintOfData = new { data = new { id = 99999, attributes = new { title = "Old Luke", cardUid = "8000000001" } } };

        var json = BuildStrapiJson(
            id: 55100,
            title: "Luke Skywalker (Variant)",
            serialCode: "06020099",
            cardUid: "5000000001",
            cardNumber: 99,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Legendary",
            foil: true,
            imageUrl: null,
            variantOf: variantOfData,
            reprintOf: reprintOfData);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);

        var card = await db.Cards.SingleAsync(c => c.Name == "Luke Skywalker (Variant)" && c.Game!.Name == Game);
        var printing = await db.CardPrintings.SingleAsync(p => p.CardId == card.Id);
        using var doc = JsonDocument.Parse(printing.DetailsJson!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("variantOfCardUid", out var variantOfCardUid), "DetailsJson must contain 'variantOfCardUid'");
        Assert.Equal("9000000001", variantOfCardUid.GetString());

        Assert.True(root.TryGetProperty("reprintOfCardUid", out var reprintOfCardUid), "DetailsJson must contain 'reprintOfCardUid'");
        Assert.Equal("8000000001", reprintOfCardUid.GetString());
        Assert.Equal(99, root.GetProperty("cardNumber").GetInt32());
        Assert.False(root.TryGetProperty("variantOfSourceId", out _));
        Assert.False(root.TryGetProperty("reprintOfSourceId", out _));
    }

    [Fact]
    public async Task ImportFromFileAsync_Resolves_BaseCardId_When_BaseCard_Already_Imported()
    {
        // When a variant card is imported after its base card, BaseCardId must be set
        // to the base card's primary key.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // First: import the base card.
        var baseJson = BuildStrapiJson(
            id: 10001,
            title: "Han Solo",
            serialCode: "07010001",
            cardUid: "1000000001",
            cardNumber: 1,
            expansionCode: "SHD",
            typeName: "Unit",
            rarity: "Legendary",
            foil: false,
            imageUrl: null);

        await importer.ImportFromFileAsync(ToStream(baseJson), new ImportOptions(DryRun: false, SetCode: "SHD"));

        var baseCard = await db.Cards.SingleAsync(c => c.Name == "Han Solo" && c.Game!.Name == Game);

        // Second: import the variant card that references the base by title.
        var variantOfData = new { data = new { id = 10001, attributes = new { title = "Han Solo", cardUid = "1000000001" } } };

        var variantJson = BuildStrapiJson(
            id: 10002,
            title: "Han Solo (Showcase)",
            serialCode: "07010001SC",
            cardUid: "1000000002",
            cardNumber: 1,
            expansionCode: "SHD",
            typeName: "Unit",
            rarity: "Legendary",
            foil: false,
            imageUrl: null,
            variantOf: variantOfData);

        var summary = await importer.ImportFromFileAsync(ToStream(variantJson), new ImportOptions(DryRun: false, SetCode: "SHD"));

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);

        var variantCard = await db.Cards.SingleAsync(c => c.Name == "Han Solo (Showcase)" && c.Game!.Name == Game);
        var basePrinting = await db.CardPrintings.SingleAsync(p => p.CardId == baseCard.Id);
        var variantPrinting = await db.CardPrintings.SingleAsync(p => p.CardId == variantCard.Id);
        Assert.Equal(basePrinting.Id, variantPrinting.VariantOfId);
    }

    [Fact]
    public async Task ImportFromFileAsync_BaseCardId_Null_When_Base_Not_Yet_Imported()
    {
        // If the base card is not present in the DB at the time the variant is imported,
        // BaseCardId must remain null (it can be resolved on a subsequent import run).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        // Import only the variant — the base card "Princess Leia" does not exist yet.
        var variantOfData = new { data = new { id = 20001, attributes = new { title = "Princess Leia", cardUid = "2000000001" } } };

        var variantJson = BuildStrapiJson(
            id: 20002,
            title: "Princess Leia (Alternate Art)",
            serialCode: "07020002AA",
            cardUid: "2000000002",
            cardNumber: 2,
            expansionCode: "SHD",
            typeName: "Unit",
            rarity: "Rare",
            foil: false,
            imageUrl: null,
            variantOf: variantOfData);

        var summary = await importer.ImportFromFileAsync(ToStream(variantJson), new ImportOptions(DryRun: false, SetCode: "SHD"));

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);

        var variantCard = await db.Cards.SingleAsync(c => c.Name == "Princess Leia (Alternate Art)" && c.Game!.Name == Game);
        var variantPrinting = await db.CardPrintings.SingleAsync(p => p.CardId == variantCard.Id);
        Assert.Null(variantPrinting.VariantOfId);
    }

    [Fact]
    public async Task ImportFromFileAsync_BaseCard_Without_VariantOf_Has_Null_BaseCardId()
    {
        // Cards that are not variants must have BaseCardId = null.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 30001,
            title: "Darth Maul",
            serialCode: "06030001",
            cardUid: "3000000001",
            cardNumber: 1,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Rare",
            foil: false,
            imageUrl: null);

        await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false, SetCode: "SOR"));

        var card = await db.Cards.SingleAsync(c => c.Name == "Darth Maul" && c.Game!.Name == Game);
        var printing = await db.CardPrintings.SingleAsync(p => p.CardId == card.Id);
        Assert.Null(printing.VariantOfId);
    }

    [Fact]
    public async Task ImportFromFileAsync_NullVariantOf_Stores_Null_VariantOf_Fields_In_DetailsJson()
    {
        // When variantOf is null in the API response the DetailsJson must still be valid JSON
        // and relationship UID fields must be present with null values.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 40001,
            title: "R2-D2",
            serialCode: "06040001",
            cardUid: "4000000001",
            cardNumber: 1,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Common",
            foil: false,
            imageUrl: null);

        await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false, SetCode: "SOR"));

        var card = await db.Cards.SingleAsync(c => c.Name == "R2-D2" && c.Game!.Name == Game);
        var printing = await db.CardPrintings.SingleAsync(p => p.CardId == card.Id);
        using var doc = JsonDocument.Parse(printing.DetailsJson!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("variantOfCardUid", out var variantOfCardUid));
        Assert.Equal(JsonValueKind.Null, variantOfCardUid.ValueKind);
        Assert.True(root.TryGetProperty("reprintOfCardUid", out var reprintOfCardUid));
        Assert.Equal(JsonValueKind.Null, reprintOfCardUid.ValueKind);
        Assert.Equal(1, root.GetProperty("cardNumber").GetInt32());
        Assert.False(root.TryGetProperty("variantOfSourceId", out _));
        Assert.False(root.TryGetProperty("reprintOfSourceId", out _));
    }

    [Fact]
    public async Task ImportFromFileAsync_Card_Without_CardNumber_Falls_Back_To_RecordId()
    {
        // When cardNumber is absent (null) the importer must fall back to record.Id.ToString()
        // as the number, and the card+printing should be upserted successfully (no errors).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = BuildStrapiJson(
            id: 77777,
            title: "No Number Card",
            serialCode: null,
            cardUid: "7777700001",
            cardNumber: null,
            expansionCode: "SOR",
            typeName: "Event",
            rarity: "Common",
            foil: false,
            imageUrl: null);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);

        // Number should be the record id string when both serialCode and cardNumber are null.
        var printing = await db.CardPrintings.Include(p => p.CatalogSet)
            .SingleAsync(p => p.CatalogSet!.Code == "SOR" && p.Number == "77777");
        Assert.Equal("SOR", printing.CatalogSet!.Code);
        Assert.Equal("77777", printing.Number);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Uses_NumericExpansionId_Filter_When_Resolved()
    {
        // When TryResolveExpansionIdAsync returns a numeric ID, the importer must forward it
        // in the filter so the API client uses the more reliable ID-based filter.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cards = ParseRecords(BuildStrapiPage(page: 1, pageCount: 1, id: 80001, title: "Rey",
            serialCode: "08010001", expansionId: 7));

        // Client reports expansion ID 7 from TryResolveExpansionIdAsync.
        var client = new CapturingSWUApiClient(cards, expansionId: 7);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR");
        var summary = await importer.ImportFromRemoteAsync(options);

        // Filter must carry the resolved numeric ID, not the code string.
        Assert.NotNull(client.CapturedFilter);
        Assert.Equal(7, client.CapturedFilter.ExpansionId);
        Assert.Null(client.CapturedFilter.ExpansionCode);

        // No fallback warning when the ID was resolved successfully.
        Assert.DoesNotContain(summary.Messages, m => m.StartsWith("Warning:"));
    }

    [Fact]
    public async Task ImportFromRemoteAsync_FallsBackToCodeFilter_When_IdResolution_Fails()
    {
        // When TryResolveExpansionIdAsync returns null, the importer must fall back to the
        // code-based filter and include a warning in the import summary messages.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cards = ParseRecords(BuildStrapiPage(page: 1, pageCount: 1, id: 80002, title: "Finn",
            serialCode: "08010002"));

        // Client returns null from TryResolveExpansionIdAsync (simulates discovery failure).
        var client = new CapturingSWUApiClient(cards, expansionId: null);
        var importer = CreateImporter(db, client);

        var options = new ImportOptions(DryRun: true, SetCode: "TWI");
        var summary = await importer.ImportFromRemoteAsync(options);

        // The filter must fall back to the code-based filter when ID resolution fails.
        Assert.NotNull(client.CapturedFilter);
        Assert.Equal("TWI", client.CapturedFilter.ExpansionCode);
        Assert.Null(client.CapturedFilter.ExpansionId);

        // A warning must appear in the summary messages.
        Assert.Contains(summary.Messages, m => m.Contains("Warning") && m.Contains("TWI"));
    }

    [Fact]
    public async Task ImportFromFileAsync_PopulatesCatalogDetails_AndUsesOnlyExplicitSetReleaseDate()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        const string json = """
            {
              "data": [
                {
                  "id": 91001,
                  "attributes": {
                    "title": "Sabine Wren",
                    "subtitle": "Galvanized Revolutionary",
                    "cardUid": "06010014",
                    "serialCode": "06010014",
                    "locale": "en",
                    "cardNumber": 14,
                    "rarity": {
                      "data": {
                        "id": 4,
                        "attributes": {
                          "name": "Common",
                          "character": "C",
                          "color": "#a8a8a8"
                        }
                      }
                    },
                    "text": "Action: Deal 1 damage to each base.",
                    "artist": "Test Artist",
                    "cost": 4,
                    "power": 2,
                    "health": 5,
                    "arena": "Ground",
                    "aspects": {
                      "data": [
                        {
                          "id": 1,
                          "attributes": {
                            "name": "Aggression",
                            "color": "#d3202a"
                          }
                        }
                      ]
                    },
                    "aspectDuplicates": {
                      "data": [
                        {
                          "id": 22,
                          "attributes": {
                            "name": "Heroism",
                            "color": "#e5d7b2"
                          }
                        }
                      ]
                    },
                    "traits": ["Rebel", "Mandalorian"],
                    "keywords": ["Raid"],
                    "createdAt": "2024-01-24T22:25:47.763Z",
                    "updatedAt": "2024-02-01T10:00:00.000Z",
                    "publishedAt": "2024-01-24T22:25:47.763Z",
                    "type": {
                      "data": {
                        "id": 4,
                        "attributes": {
                          "name": "Leader",
                          "value": "leader"
                        }
                      }
                    },
                    "expansion": {
                      "data": {
                        "id": 2,
                        "attributes": {
                          "name": "Spark of Rebellion",
                          "code": "SOR",
                          "publishedAt": "2024-01-24T22:25:47.763Z"
                        }
                      }
                    },
                    "variantTypes": {
                      "data": [
                        {
                          "id": 46,
                          "attributes": {
                            "name": "Standard",
                            "variantId": "01",
                            "foil": false
                          }
                        }
                      ]
                    }
                  }
                }
              ],
              "meta": {
                "pagination": {
                  "page": 1,
                  "pageSize": 100,
                  "pageCount": 1,
                  "total": 1
                }
              }
            }
            """;

        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false, SetCode: "SOR"));
        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.PrintingsCreated);

        var set = await db.Sets.SingleAsync(s => s.Code == "SOR");
        Assert.Equal(new DateOnly(2024, 1, 24), set.ReleasedAt);

        var page = JsonNode.Parse(json)!;
        var expansion = page["data"]![0]!["attributes"]!["expansion"]!["data"]!["attributes"]!;
        expansion["releaseDate"] = "2024-02-01";
        await importer.ImportFromFileAsync(ToStream(page.ToJsonString()), new ImportOptions(DryRun: false, SetCode: "SOR"));
        Assert.Equal(new DateOnly(2024, 2, 1), (await db.Sets.SingleAsync(s => s.Code == "SOR")).ReleasedAt);

        expansion.AsObject().Remove("releaseDate");
        expansion["publishedAt"] = "2025-02-01T00:00:00.000Z";
        await importer.ImportFromFileAsync(ToStream(page.ToJsonString()), new ImportOptions(DryRun: false, SetCode: "SOR"));
        db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2025, 2, 1), (await db.Sets.SingleAsync(s => s.Code == "SOR")).ReleasedAt);

        var game = await db.Games.SingleAsync(candidate => candidate.Name == Game);
        var rarity = await db.Rarities.SingleAsync(r => r.GameId == game.Id && r.Name == "Common");
        Assert.Equal("C", rarity.Character);
        Assert.Equal("#a8a8a8", rarity.Color);

        var aggression = await db.Aspects.SingleAsync(a => a.Name == "Aggression");
        Assert.Equal("#d3202a", aggression.Color);

        var heroism = await db.Aspects.SingleAsync(a => a.Name == "Heroism");
        Assert.Equal("#e5d7b2", heroism.Color);
    }

    [Fact]
    public async Task ImportFromFileAsync_MultiplePrintingsWithDistinctCardUids_ShareSingleLogicalCard()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var standardJson = BuildStrapiJson(
            id: 92001,
            title: "Luke Skywalker",
            subtitle: "Jedi Knight",
            serialCode: "06010050",
            cardUid: "06010050",
            cardNumber: 50,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Legendary",
            foil: false,
            imageUrl: "https://cdn.starwarsunlimited.com/06010050_EN.png",
            text: "Standard text.");

        var variantOfData = new { data = new { id = 92001, attributes = new { title = "Luke Skywalker", cardUid = "06010050" } } };
        var foilJson = BuildStrapiJson(
            id: 92002,
            title: "Luke Skywalker",
            subtitle: "Jedi Knight",
            serialCode: "06020050",
            cardUid: "06020050",
            cardNumber: 50,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Legendary",
            foil: true,
            imageUrl: "https://cdn.starwarsunlimited.com/06020050_EN.png",
            text: "Variant text without reminder.",
            variantOf: variantOfData);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary1 = await importer.ImportFromFileAsync(ToStream(standardJson), options);
        var summary2 = await importer.ImportFromFileAsync(ToStream(foilJson), options);

        Assert.Equal(0, summary1.Errors);
        Assert.Equal(1, summary1.CardsCreated);
        Assert.Equal(1, summary1.PrintingsCreated);

        Assert.Equal(0, summary2.Errors);
        Assert.Equal(0, summary2.CardsCreated);
        Assert.Equal(0, summary2.CardsUpdated);
        Assert.Equal(1, summary2.PrintingsCreated);

        var card = await db.Cards.Include(c => c.Printings).SingleAsync(c => c.Game!.Name == Game);
        Assert.Equal("Luke Skywalker \u2014 Jedi Knight", card.Name);
        Assert.Equal("Standard text.", card.Text);
        Assert.Equal("06010050", card.CardUid);
        Assert.Equal(2, card.Printings.Count);
        Assert.Contains(card.Printings, p => p.Number == "50" && p.CardUid == "06010050" && p.VariantOfId == null);
        Assert.Contains(card.Printings, p => p.Number == "50" && p.CardUid == "06020050" && p.VariantOfId != null);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a single-record Strapi page response with explicit pagination metadata,
    /// optionally overriding the numeric expansion ID embedded in the card attributes.
    /// Used for pagination and expansion-ID tests.
    /// </summary>
    private static string BuildStrapiPage(int page, int pageCount, int id, string title, string serialCode,
        int expansionId = 2)
    {
        var record = new
        {
            id,
            attributes = new
            {
                title,
                subtitle = (string?)null,
                cardUid = id.ToString(),
                serialCode,
                locale = "en",
                cardNumber = id,
                rarity = "Common",
                text = (string?)null,
                artist = (string?)null,
                cost = (int?)null,
                power = (int?)null,
                health = (int?)null,
                arena = (string?)null,
                aspects = (string[]?)null,
                traits = (string[]?)null,
                keywords = (string[]?)null,
                updatedAt = "2025-11-10T16:07:21.000Z",
                type = new { data = new { id = 3, attributes = new { name = "Unit", value = "Unit" } } },
                expansion = new { data = new { id = expansionId, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                variantOf = new { data = (object?)null },
                reprintOf = new { data = (object?)null },
                artFront = new { data = (object?)null },
                artBack = new { data = (object?)null }
            }
        };

        return JsonSerializer.Serialize(new
        {
            data = new[] { record },
            meta = new { pagination = new { page, pageSize = 1, pageCount, total = pageCount } }
        });
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Deserialises a Strapi-format JSON string into a flat list of <see cref="StrapiRecord"/>
    /// objects so remote-import tests can supply pre-built card data to the stub API client.
    /// </summary>
    private static IReadOnlyList<StrapiRecord> ParseRecords(string strapiJson)
    {
        var paged = JsonSerializer.Deserialize<StrapiPagedResponse>(strapiJson, JsonOptions);
        return paged?.Data ?? [];
    }

    /// <summary>Returns a preconfigured list of cards for every <c>GetAllCardsAsync</c> call.</summary>
    private sealed class StubSWUApiClient(
        IReadOnlyList<StrapiRecord>? cards = null,
        int? expansionId = null) : ISWUApiClient
    {
        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StrapiRecord>>(cards ?? []);

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default)
            => Task.FromResult(expansionId);
    }

    /// <summary>
    /// Records the <see cref="SWUCardFilter"/> passed to <c>GetAllCardsAsync</c> so tests
    /// can assert on what filter the importer built.
    /// </summary>
    private sealed class CapturingSWUApiClient(
        IReadOnlyList<StrapiRecord>? cards = null,
        int? expansionId = null) : ISWUApiClient
    {
        public SWUCardFilter? CapturedFilter { get; private set; }

        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default)
        {
            CapturedFilter = filter;
            return Task.FromResult<IReadOnlyList<StrapiRecord>>(cards ?? []);
        }

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default)
            => Task.FromResult(expansionId);
    }

    /// <summary>Throws the configured exception on <c>GetAllCardsAsync</c>.</summary>
    private sealed class ThrowingSWUApiClient(Exception exception) : ISWUApiClient
    {
        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default)
            => throw exception;

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default)
            => Task.FromResult<int?>(null);
    }

    /// <summary>
    /// Builds a Strapi page response containing <paramref name="count"/> minimal card records,
    /// used to simulate multi-record pages for limit and pagination tests.
    /// </summary>
    private static string BuildStrapiPageWithMultipleRecords(int page, int pageCount, int startId, int count)
    {
        var records = Enumerable.Range(0, count).Select(i =>
        {
            int id = startId + i;
            return (object)new
            {
                id,
                attributes = new
                {
                    title = $"Card {id}",
                    subtitle = (string?)null,
                    cardUid = id.ToString(),
                    serialCode = $"LT{id:D8}",
                    locale = "en",
                    cardNumber = id,
                    rarity = "Common",
                    text = (string?)null,
                    artist = (string?)null,
                    cost = (int?)null,
                    power = (int?)null,
                    health = (int?)null,
                    arena = (string?)null,
                    aspects = (string[]?)null,
                    traits = (string[]?)null,
                    keywords = (string[]?)null,
                    updatedAt = "2025-11-10T16:07:21.000Z",
                    type = new { data = new { id = 3, attributes = new { name = "Unit", value = "Unit" } } },
                    expansion = new { data = new { id = 2, attributes = new { name = "Spark of Rebellion", code = "SOR" } } },
                    variantTypes = new { data = new[] { new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } } } },
                    variantOf = new { data = (object?)null },
                    reprintOf = new { data = (object?)null },
                    artFront = new { data = (object?)null },
                    artBack = new { data = (object?)null }
                }
            };
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            data = records,
            meta = new { pagination = new { page, pageSize = count, pageCount, total = pageCount * count } }
        });
    }

    [Fact]
    public async Task ImportFromFileAsync_RecordsWithEmptyVariantTypes_ResolvesPrestigeTreatmentsFromSerialCode_WithoutConflict()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var json = """
        {
          "data": [
            {
              "id": 51387,
              "attributes": {
                "title": "Senator Chuchi",
                "subtitle": "Voice for the Voiceless",
                "serialCode": "06361125",
                "cardUid": "2437821363",
                "cardNumber": 1125,
                "locale": "en",
                "rarity": "Uncommon",
                "expansion": { "data": { "id": 73, "attributes": { "name": "Secrets of Power", "code": "SEC" } } },
                "variantTypes": { "data": [] },
                "type": { "data": { "id": 9, "attributes": { "name": "Unit", "value": "Unit" } } }
              }
            },
            {
              "id": 51492,
              "attributes": {
                "title": "Senator Chuchi",
                "subtitle": "Voice for the Voiceless",
                "serialCode": "06371125",
                "cardUid": "5372118662",
                "cardNumber": 1125,
                "locale": "en",
                "rarity": "Uncommon",
                "expansion": { "data": { "id": 73, "attributes": { "name": "Secrets of Power", "code": "SEC" } } },
                "variantTypes": { "data": [] },
                "type": { "data": { "id": 9, "attributes": { "name": "Unit", "value": "Unit" } } }
              }
            }
          ]
        }
        """;

        var summary = await importer.ImportFromFileAsync(ToStream(json), new ImportOptions(DryRun: false, SetCode: "SEC"));

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(2, summary.PrintingsCreated);

        var printings = await db.CardPrintings
            .Where(p => p.CatalogSet!.Code == "SEC")
            .Include(p => p.Treatment)
            .OrderBy(p => p.Id)
            .ToListAsync();

        Assert.Equal(2, printings.Count);
        Assert.Equal("Rose Gold Prestige", printings[0].Treatment?.Name);
        Assert.Equal("Gold Prestige", printings[1].Treatment?.Name);
    }
}
