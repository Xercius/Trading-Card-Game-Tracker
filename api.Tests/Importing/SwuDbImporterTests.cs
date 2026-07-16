using api.Data;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
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
        int cardNumber,
        string expansionCode,
        string typeName,
        string rarity,
        bool foil,
        string? imageUrl,
        string? text = null,
        string createdAt = "2025-08-15T18:29:41.633Z",
        string updatedAt = "2025-11-10T16:07:21.000Z",
        string publishedAt = "2025-08-15T18:30:00.000Z")
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
                subtitle = (string?)null,
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
                variantOf = new { data = (object?)null },
                reprintOf = new { data = (object?)null },
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

    private static SwuDbImporter CreateImporter(AppDbContext db, CapturingHttpMessageHandler handler)
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

        Assert.NotEmpty(handler.CapturedRequests);
        var rawQuery = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);

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

        Assert.NotEmpty(handler.CapturedRequests);
        var rawQuery = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);

        // The date filter must appear in the query string.
        Assert.Contains("filters[updatedAt][$gt]", rawQuery);

        // The timestamp must be in ISO-8601 UTC format (yyyy-MM-ddTHH:mm:ss.fffZ).
        Assert.Contains("2025-11-01T00:00:00.000Z", rawQuery);

        // Results must be ordered by updatedAt ascending for correct incremental paging.
        Assert.Contains("updatedAt:asc", rawQuery);
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
}
