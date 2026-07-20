using api.Data;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
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

    private static SwuDbImporter CreateImporter(AppDbContext db)
        => new(db, new StubHttpClientFactory());

    private static SwuDbImporter CreateImporter(AppDbContext db, HttpMessageHandler handler)
        => new(db, new StubHttpClientFactory(handler));

    // ─── tests ──────────────────────────────────────────────────────────────

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

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromFileAsync(ToStream(json), options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.PrintingsUpdated);

        var card = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine" && c.Game == Game);
        Assert.Equal("Leader", card.CardType);
        Assert.Equal("Deploy: exhaust this leader.", card.Description);

        // Printing stored with serialCode as the Number key.
        var printing = await db.CardPrintings.SingleAsync(p => p.Number == "06010001");
        Assert.Equal(card.Id, printing.CardId);
        Assert.Equal("SOR", printing.Set);
        Assert.Equal("Legendary", printing.Rarity);
        Assert.Equal("Standard", printing.Style);
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

        var printing = await db.CardPrintings.SingleAsync(p => p.Number == "06020002");
        Assert.Equal("Foil", printing.Style);
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

        var card = await db.Cards.SingleAsync(c => c.Name == "Grand Moff Tarkin" && c.Game == Game);
        Assert.Equal("Updated text.", card.Description);

        var printing = await db.CardPrintings.SingleAsync(p => p.Number == "06010003");
        Assert.Equal("Uncommon", printing.Rarity);
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
        Assert.False(await db.Cards.AnyAsync(c => c.Name == "Scheda Italiana" && c.Game == Game));
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "English Card" && c.Game == Game));

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

        var printing = await db.CardPrintings.SingleAsync(p => p.Number == "07010001");
        Assert.Equal("SHD", printing.Set);
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
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Bare Array Card" && c.Game == Game));
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
        var printing = await db.CardPrintings.SingleAsync(p => p.Set == "TWI" && p.Number == "7");
        Assert.Equal("TWI", printing.Set);
        Assert.Equal("7", printing.Number);

        // Re-import identical data — should detect the existing printing by set+number and not create a new one.
        var summary2 = await importer.ImportFromFileAsync(ToStream(json), options);
        Assert.Equal(0, summary2.Errors);
        Assert.Equal(0, summary2.CardsCreated);
        Assert.Equal(0, summary2.PrintingsCreated);
        Assert.Equal(1, await db.CardPrintings.CountAsync(p => p.Set == "TWI" && p.Number == "7"));
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

        var sorPrinting = await db.CardPrintings.SingleAsync(p => p.Number == "06030001");
        Assert.Equal("SOR", sorPrinting.Set);

        var shdPrinting = await db.CardPrintings.SingleAsync(p => p.Number == "07020002");
        Assert.Equal("SHD", shdPrinting.Set);
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
        var printing = await db.CardPrintings.SingleAsync(p => p.Card.Name == "Mystery Card" && p.Card.Game == Game);
        Assert.Equal("UNK", printing.Set);
    }

    [Fact]
    public async Task ImportFromFileAsync_Stores_Rich_Attributes_In_DetailsJson()
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
                aspects = new[] { "Aggression", "Command" },
                traits = new[] { "Imperial", "Clone Trooper" },
                keywords = new[] { "Sentinel", "Grit" },
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

        var card = await db.Cards.SingleAsync(c => c.Name == "Rich Card \u2014 The Rich One" && c.Game == Game);
        Assert.NotNull(card.DetailsJson);
        // Spot-check that all optional fields are present in the stored JSON blob.
        Assert.Contains("\"subtitle\":\"The Rich One\"", card.DetailsJson);
        Assert.Contains("\"artist\":\"Jane Doe\"", card.DetailsJson);
        Assert.Contains("\"power\":3", card.DetailsJson);
        Assert.Contains("\"health\":7", card.DetailsJson);
        Assert.Contains("\"cost\":5", card.DetailsJson);
        Assert.Contains("\"arena\":\"Ground\"", card.DetailsJson);
        Assert.Contains("Aggression", card.DetailsJson);
        Assert.Contains("Imperial", card.DetailsJson);
        Assert.Contains("Sentinel", card.DetailsJson);
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
        var card1 = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine \u2014 Galactic Emperor" && c.Game == Game);
        var card2 = await db.Cards.SingleAsync(c => c.Name == "Chancellor Palpatine \u2014 How Liberty Dies" && c.Game == Game);
        Assert.NotEqual(card1.Id, card2.Id);

        // Each card's text must be its own, not overwritten by the second import.
        Assert.Equal("The emperor's text.", card1.Description);
        Assert.Equal("How liberty dies text.", card2.Description);

        // Each printing must point to its own card.
        var printing1 = await db.CardPrintings.SingleAsync(p => p.Number == "SOR-CP-001");
        var printing2 = await db.CardPrintings.SingleAsync(p => p.Number == "SOR-CP-002");
        Assert.Equal(card1.Id, printing1.CardId);
        Assert.Equal(card2.Id, printing2.CardId);
    }

    [Fact]
    public async Task ImportFromFileAsync_Stores_UpdatedAt_And_CreatedAt_In_DetailsJson()
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

        var printing = await db.CardPrintings.SingleAsync(p => p.Number == "06010010");
        Assert.NotNull(printing.DetailsJson);

        using var doc = JsonDocument.Parse(printing.DetailsJson);
        var root = doc.RootElement;

        // createdAt must be present and match the value from the API response
        Assert.True(root.TryGetProperty("createdAt", out var createdAtProp), "DetailsJson must contain 'createdAt'");
        Assert.Contains(expectedCreatedAt[..19], createdAtProp.GetString()); // compare up to seconds precision

        // updatedAt must be present and match the value from the API response
        Assert.True(root.TryGetProperty("updatedAt", out var updatedAtProp), "DetailsJson must contain 'updatedAt'");
        Assert.Contains(expectedUpdatedAt[..19], updatedAtProp.GetString());
    }

    [Fact]
    public async Task ImportFromRemoteAsync_WithoutUpdatedSince_Uses_DefaultSort_By_UpdatedAt()
    {
        // When no UpdatedSince filter is provided the importer must still sort by updatedAt:asc
        // so that incremental callers can track the highest timestamp they have seen.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var singleCardJson = BuildStrapiJson(
            id: 60001,
            title: "Obi-Wan Kenobi",
            serialCode: "06010020",
            cardUid: "9988001122",
            cardNumber: 20,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Rare",
            foil: false,
            imageUrl: null);

        var handler = new CapturingHttpMessageHandler(singleCardJson);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR");
        await importer.ImportFromRemoteAsync(options);

        // CapturedRequests[0] is the ID-resolution discovery request;
        // CapturedRequests[1] is the first (and only) paging request.
        Assert.True(handler.CapturedRequests.Count >= 2,
            $"Expected at least 2 requests (discovery + page 1), but got {handler.CapturedRequests.Count}.");
        var rawQuery = Uri.UnescapeDataString(handler.CapturedRequests[1].Query);

        // Must sort by updatedAt ascending so incremental callers can page predictably.
        Assert.Contains("updatedAt:asc", rawQuery);

        // Must NOT include updatedAt filter when UpdatedSince is absent.
        Assert.DoesNotContain("filters[updatedAt]", rawQuery);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_WithUpdatedSince_Includes_DateFilter_In_QueryString()
    {
        // When UpdatedSince is set the importer must add filters[updatedAt][$gt]=<timestamp>
        // so the API only returns cards modified after the given point in time.
        // This is the reliable strategy for incremental update detection (Task 2.4).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var singleCardJson = BuildStrapiJson(
            id: 60002,
            title: "Han Solo",
            serialCode: "06010021",
            cardUid: "1122334455",
            cardNumber: 21,
            expansionCode: "SOR",
            typeName: "Unit",
            rarity: "Common",
            foil: false,
            imageUrl: null,
            updatedAt: "2025-12-01T09:00:00.000Z");

        var handler = new CapturingHttpMessageHandler(singleCardJson);
        var importer = CreateImporter(db, handler);

        var since = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new ImportOptions(DryRun: true, SetCode: "SOR", UpdatedSince: since);
        await importer.ImportFromRemoteAsync(options);

        // CapturedRequests[0] is the ID-resolution discovery request;
        // CapturedRequests[1] is the first (and only) paging request.
        Assert.True(handler.CapturedRequests.Count >= 2,
            $"Expected at least 2 requests (discovery + page 1), but got {handler.CapturedRequests.Count}.");
        var rawQuery = Uri.UnescapeDataString(handler.CapturedRequests[1].Query);

        // The date filter must appear in the query string.
        Assert.Contains("filters[updatedAt][$gt]", rawQuery);

        // The timestamp must be in ISO-8601 UTC format (yyyy-MM-ddTHH:mm:ss.fffZ).
        Assert.Contains("2025-11-01T00:00:00.000Z", rawQuery);

        // Results must be ordered by updatedAt ascending for correct incremental paging.
        Assert.Contains("updatedAt:asc", rawQuery);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Pages_Through_Multiple_Pages()
    {
        // The importer reads meta.pagination.pageCount on page 1 and then fetches
        // every subsequent page until all records are collected.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Two-page response: page 1 declares pageCount=2 and contains card A;
        // page 2 contains card B.
        var page1 = BuildStrapiPage(page: 1, pageCount: 2, id: 71000, title: "Page One Card", serialCode: "PG010001");
        var page2 = BuildStrapiPage(page: 2, pageCount: 2, id: 71001, title: "Page Two Card", serialCode: "PG020001");

        var pageResponses = new Dictionary<int, string> { [1] = page1, [2] = page2 };
        using var handler = new FakePagedHttpMessageHandler(pageResponses);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        var summary = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(2, summary.CardsCreated);
        Assert.Equal(2, summary.PrintingsCreated);
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Page One Card" && c.Game == Game));
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Page Two Card" && c.Game == Game));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImportFromRemoteAsync_ThrowsArgumentException_When_SetCode_Is_Null_Or_Whitespace(string? setCode)
    {
        // SetCode is required for the remote import — the importer must throw before any HTTP call.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importer = CreateImporter(db);

        var options = new ImportOptions(DryRun: false, SetCode: setCode);
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportFromRemoteAsync(options));
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Propagates_HttpRequestException_On_Http_Error()
    {
        // When the API returns a non-success status code, EnsureSuccessStatusCode() throws
        // HttpRequestException which must propagate to the caller unchanged.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var handler = new ErrorHttpMessageHandler(HttpStatusCode.InternalServerError);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR");
        await Assert.ThrowsAsync<HttpRequestException>(() => importer.ImportFromRemoteAsync(options));
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Respects_Limit_Across_Multiple_Pages()
    {
        // When Limit is set the importer fetches all pages first and then processes at most
        // Limit records, so only that many cards and printings must be created.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Two pages of 5 cards each (10 total); Limit=3 must stop after 3 upserts.
        var page1 = BuildStrapiPageWithMultipleRecords(page: 1, pageCount: 2, startId: 82000, count: 5);
        var page2 = BuildStrapiPageWithMultipleRecords(page: 2, pageCount: 2, startId: 82005, count: 5);

        var pageResponses = new Dictionary<int, string> { [1] = page1, [2] = page2 };
        using var handler = new FakePagedHttpMessageHandler(pageResponses);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: false, SetCode: "SOR", Limit: 3);
        var summary = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, summary.Errors);
        Assert.Equal(3, summary.CardsCreated);
        Assert.Equal(3, summary.PrintingsCreated);
    }

    [Fact]
    public async Task ImportFromFileAsync_Stores_VariantOf_And_ReprintOf_In_DetailsJson()
    {
        // The importer must persist variantOfSourceId/variantOfCardUid and
        // reprintOfSourceId/reprintOfCardUid in the Card's DetailsJson so the
        // relationship data is available without a schema change.
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

        var card = await db.Cards.SingleAsync(c => c.Name == "Luke Skywalker (Variant)" && c.Game == Game);
        Assert.NotNull(card.DetailsJson);

        using var doc = JsonDocument.Parse(card.DetailsJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("variantOfSourceId", out var variantOfSourceId), "DetailsJson must contain 'variantOfSourceId'");
        Assert.Equal(12345, variantOfSourceId.GetInt32());

        Assert.True(root.TryGetProperty("variantOfCardUid", out var variantOfCardUid), "DetailsJson must contain 'variantOfCardUid'");
        Assert.Equal("9000000001", variantOfCardUid.GetString());

        Assert.True(root.TryGetProperty("reprintOfSourceId", out var reprintOfSourceId), "DetailsJson must contain 'reprintOfSourceId'");
        Assert.Equal(99999, reprintOfSourceId.GetInt32());

        Assert.True(root.TryGetProperty("reprintOfCardUid", out var reprintOfCardUid), "DetailsJson must contain 'reprintOfCardUid'");
        Assert.Equal("8000000001", reprintOfCardUid.GetString());
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

        var baseCard = await db.Cards.SingleAsync(c => c.Name == "Han Solo" && c.Game == Game);

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

        var variantCard = await db.Cards.SingleAsync(c => c.Name == "Han Solo (Showcase)" && c.Game == Game);
        Assert.Equal(baseCard.Id, variantCard.BaseCardId);
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

        var variantCard = await db.Cards.SingleAsync(c => c.Name == "Princess Leia (Alternate Art)" && c.Game == Game);
        // Base not yet in DB — BaseCardId must be null.
        Assert.Null(variantCard.BaseCardId);
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

        var card = await db.Cards.SingleAsync(c => c.Name == "Darth Maul" && c.Game == Game);
        Assert.Null(card.BaseCardId);
    }

    [Fact]
    public async Task ImportFromFileAsync_NullVariantOf_Stores_Null_VariantOf_Fields_In_DetailsJson()
    {
        // When variantOf is null in the API response the DetailsJson must still be valid JSON
        // and the variantOfSourceId / variantOfCardUid fields must be present with null values.
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

        var card = await db.Cards.SingleAsync(c => c.Name == "R2-D2" && c.Game == Game);
        Assert.NotNull(card.DetailsJson);

        using var doc = JsonDocument.Parse(card.DetailsJson);
        var root = doc.RootElement;

        // Fields must be present but null.
        Assert.True(root.TryGetProperty("variantOfSourceId", out var variantOfSourceId));
        Assert.Equal(JsonValueKind.Null, variantOfSourceId.ValueKind);

        Assert.True(root.TryGetProperty("reprintOfSourceId", out var reprintOfSourceId));
        Assert.Equal(JsonValueKind.Null, reprintOfSourceId.ValueKind);
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
        var printing = await db.CardPrintings.SingleAsync(p => p.Set == "SOR" && p.Number == "77777");
        Assert.Equal("SOR", printing.Set);
        Assert.Equal("77777", printing.Number);
    }

    [Fact]
    public async Task ImportFromRemoteAsync_Uses_NumericExpansionId_Filter_When_Resolved()
    {
        // The discovery step fetches a single card to read expansion.data.id, then all
        // subsequent paging requests must use filters[expansion][id][$eq] instead of the
        // code-based filter (see docs/SWUAPI_DOCUMENTATION.txt §5).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Build JSON with a distinctive expansion ID (7) so we can assert it appears in the filter.
        var cardJson = BuildStrapiPage(page: 1, pageCount: 1, id: 80001, title: "Rey",
            serialCode: "08010001", expansionId: 7);

        var handler = new CapturingHttpMessageHandler(cardJson);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: true, SetCode: "SOR");
        var summary = await importer.ImportFromRemoteAsync(options);

        // There must be at least 2 requests: one discovery + one paging request.
        Assert.True(handler.CapturedRequests.Count >= 2,
            $"Expected at least 2 requests (discovery + page 1), but got {handler.CapturedRequests.Count}.");

        // CapturedRequests[0] is the discovery request – it must use the code-based filter with pageSize=1.
        var discoveryQuery = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("filters[expansion][code][$eq]=SOR", discoveryQuery);
        Assert.Contains("pagination[pageSize]=1", discoveryQuery);
        Assert.DoesNotContain("pagination[page]=", discoveryQuery);

        // CapturedRequests[1] is the first paging request – it must use the numeric ID filter.
        var pagingQuery = Uri.UnescapeDataString(handler.CapturedRequests[1].Query);
        Assert.Contains("filters[expansion][id][$eq]=7", pagingQuery);
        Assert.DoesNotContain("filters[expansion][code]", pagingQuery);

        // No fallback warning must be emitted when the ID was resolved successfully.
        Assert.DoesNotContain(summary.Messages, m => m.StartsWith("Warning:"));
    }

    [Fact]
    public async Task ImportFromRemoteAsync_FallsBackToCodeFilter_When_IdResolution_Fails()
    {
        // When the discovery request returns an HTTP error, the importer must fall back to the
        // code-based filter and include a warning in the import summary messages.
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cardJson = BuildStrapiPage(page: 1, pageCount: 1, id: 80002, title: "Finn",
            serialCode: "08010002");

        // First request (discovery) returns a server error; subsequent requests succeed.
        var handler = new FailFirstRequestCapturingHandler(cardJson);
        var importer = CreateImporter(db, handler);

        var options = new ImportOptions(DryRun: true, SetCode: "TWI");
        var summary = await importer.ImportFromRemoteAsync(options);

        // There must be at least 2 requests: one failed discovery + one paging request.
        Assert.True(handler.CapturedRequests.Count >= 2,
            $"Expected at least 2 requests (discovery + page 1), but got {handler.CapturedRequests.Count}.");

        // The paging request must fall back to the code-based filter.
        var pagingQuery = Uri.UnescapeDataString(handler.CapturedRequests[1].Query);
        Assert.Contains("filters[expansion][code][$eq]=TWI", pagingQuery);
        Assert.DoesNotContain("filters[expansion][id]", pagingQuery);

        // A warning must appear in the summary messages.
        Assert.Contains(summary.Messages, m => m.Contains("Warning") && m.Contains("TWI"));
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

    private sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory() => _client = new HttpClient();
        public StubHttpClientFactory(HttpMessageHandler handler) => _client = new HttpClient(handler);
        public HttpClient CreateClient(string name) => _client;
        public void Dispose() => _client.Dispose();
    }

    /// <summary>
    /// Captures every request URL sent through the <see cref="HttpClient"/> and returns a
    /// configurable canned Strapi JSON response so unit tests can inspect query parameters
    /// without making real network calls.
    /// </summary>
    internal sealed class CapturingHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public List<Uri> CapturedRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Returns a pre-built JSON string for each requested page number,
    /// allowing pagination tests to simulate multi-page API responses
    /// without hitting a real network endpoint.
    /// </summary>
    private sealed class FakePagedHttpMessageHandler(Dictionary<int, string> pages) : HttpMessageHandler
    {
        private static readonly string FallbackJson =
            "{\"data\":[],\"meta\":{\"pagination\":{\"page\":1,\"pageSize\":100,\"pageCount\":1,\"total\":0}}}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            int page = int.TryParse(query["pagination[page]"], out int p) ? p : 1;
            string json = pages.TryGetValue(page, out string? r) ? r : FallbackJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Simulates a server error on the first HTTP request (the ID-resolution discovery probe)
    /// and returns a configurable canned JSON response for all subsequent requests.
    /// Captures every request URL so tests can assert on query parameters.
    /// </summary>
    private sealed class FailFirstRequestCapturingHandler(string fallbackJson) : HttpMessageHandler
    {
        private int _callCount;
        public List<Uri> CapturedRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedRequests.Add(request.RequestUri!);
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fallbackJson, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Always returns the configured HTTP status code, allowing tests to verify
    /// that non-success responses are surfaced as <see cref="HttpRequestException"/>.
    /// </summary>
    private sealed class ErrorHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(statusCode));
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
}
