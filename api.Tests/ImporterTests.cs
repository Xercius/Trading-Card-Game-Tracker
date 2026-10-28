using api.Data;
using api.Features.Cards.Dtos;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using Xunit;


namespace api.Tests;

public class ImporterTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly MethodInfo UpsertMethod = typeof(ScryfallImporter)
        .GetMethod("UpsertCardAndPrintingAsync", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not locate UpsertCardAndPrintingAsync.");

    private static readonly Type ScryCardType = typeof(ScryfallImporter)
        .GetNestedType("ScryCard", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing ScryCard type.");

    private static readonly Type ScryFaceType = typeof(ScryfallImporter)
        .GetNestedType("ScryFace", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing ScryFace type.");

    private static readonly Type ScryImagesType = typeof(ScryfallImporter)
        .GetNestedType("ScryImages", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing ScryImages type.");

    [Theory]
    [InlineData("1973-10-04", "1973-08-05", "LEA")]
    [InlineData("1973-08-05", "1973-08-05", "LEB")]
    [InlineData(null, "1973-08-05", "LEA")]
    [InlineData("1973-10-04", null, "LEB")]
    [InlineData(null, null, "LEB")]
    public async Task ScryfallImporter_ImportNewerSetFirst_SelectsOldestReleasedPrinting(
        string? betaRelease, string? alphaRelease, string expectedSet)
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var httpFactory = new StubHttpClientFactory(new ScryfallHandler(
            ("leb", betaRelease), ("lea", alphaRelease)));
        var importer = new ScryfallImporter(db, httpFactory);

        foreach (var set in new[] { "leb", "lea" })
        {
            var summary = await importer.ImportFromRemoteAsync(new ImportOptions(DryRun: false, SetCode: set));
            Assert.Equal(0, summary.Errors);
            Assert.Equal(1, summary.PrintingsCreated);
        }

        db.ChangeTracker.Clear();
        var imported = await db.CardPrintings.Where(p => p.Card.Name == "Chronology Test Card")
            .OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(new[] { "LEB", "LEA" }, imported.Select(p => p.Set));
        Assert.Equal(betaRelease is null ? (DateOnly?)null : DateOnly.Parse(betaRelease), imported[0].ReleasedAt);
        Assert.Equal(alphaRelease is null ? (DateOnly?)null : DateOnly.Parse(alphaRelease), imported[1].ReleasedAt);

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/cards/printings?uniqueCards=true&set=LEB&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var printings = await response.Content.ReadFromJsonAsync<List<PrintingDto>>();
        Assert.NotNull(printings);
        Assert.Equal(expectedSet, Assert.Single(printings).SetName);
    }

    [Fact]
    public async Task ScryfallImporter_Reimport_BackfillsReleaseDateWithoutDuplicatingPrinting()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var httpFactory = new StubHttpClientFactory(new ScryfallHandler(
            ("leb", null), ("leb", "1973-10-04"), ("leb", null)));
        var importer = new ScryfallImporter(db, httpFactory);
        var options = new ImportOptions(DryRun: false, SetCode: "leb");

        await importer.ImportFromRemoteAsync(options);
        var updated = await importer.ImportFromRemoteAsync(options);
        var unchanged = await importer.ImportFromRemoteAsync(options);

        Assert.Equal(0, updated.Errors);
        Assert.Equal(0, updated.PrintingsCreated);
        Assert.Equal(1, updated.PrintingsUpdated);
        Assert.Equal(0, unchanged.Errors);
        Assert.Equal(0, unchanged.PrintingsUpdated);
        db.ChangeTracker.Clear();
        var printing = await db.CardPrintings.SingleAsync(p => p.Set == "LEB");
        Assert.Equal(new DateOnly(1973, 10, 4), printing.ReleasedAt);
    }

    [Fact]
    public async Task ScryfallImporter_Upsert_CreatesAndUpdatesEntitiesCorrectly()
    {
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var httpFactory = new StubHttpClientFactory();
        var importer = new ScryfallImporter(db, httpFactory);
        var summary = new ImportSummary();

        var initialCard = CreateScryCard(
            id: "unit-1",
            name: "Unit Test Card",
            typeLine: "Creature",
            oracleText: "Deal 2 damage to any target",
            rarity: "rare",
            collectorNumber: "007",
            setCode: null,
            finishes: new List<string> { "foil" },
            imageUrl: "https://img.example.com/unit-test.png",
            faces: null);

        await InvokeUpsertAsync(importer, initialCard, setCode: "uts", summary);
        await db.SaveChangesAsync();

        var createdCard = await db.Cards.SingleAsync(c => c.Name == "Unit Test Card");
        Assert.Equal("Magic", createdCard.Game);
        Assert.Equal("Creature", createdCard.CardType);
        Assert.Equal("Deal 2 damage to any target", createdCard.Text);

        var createdPrinting = await db.CardPrintings.SingleAsync(p => p.Set == "UTS" && p.Number == "007");
        Assert.Equal(createdCard.Id, createdPrinting.CardId);
        Assert.Equal("rare", createdPrinting.Rarity);
        Assert.Equal("Foil", (await db.Treatments.SingleAsync(t => t.Id == createdPrinting.TreatmentId)).Name);
        Assert.Equal("https://img.example.com/unit-test.png", createdPrinting.ImageUrl);

        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(0, summary.PrintingsUpdated);

        var faceImage = CreateScryImages("https://img.example.com/unit-face.png");
        var faces = CreateFaceList(CreateScryFace("Front", "Draw a card", faceImage));
        var updatedCard = CreateScryCard(
            id: "unit-1",
            name: "Unit Test Card",
            typeLine: "Sorcery",
            oracleText: null,
            rarity: "mythic",
            collectorNumber: "007",
            setCode: "uts",
            finishes: new List<string> { "nonfoil" },
            imageUrl: null,
            faces: faces);

        await InvokeUpsertAsync(importer, updatedCard, setCode: "uts", summary);
        await db.SaveChangesAsync();

        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(1, summary.CardsUpdated);
        Assert.Equal(1, summary.PrintingsCreated);
        Assert.Equal(1, summary.PrintingsUpdated);

        await db.Entry(createdCard).ReloadAsync();
        await db.Entry(createdPrinting).ReloadAsync();

        Assert.Equal("Sorcery", createdCard.CardType);
        Assert.Equal("Front\nDraw a card", createdCard.Text);

        Assert.Equal("mythic", createdPrinting.Rarity);
        Assert.Equal("Standard", (await db.Treatments.SingleAsync(t => t.Id == createdPrinting.TreatmentId)).Name);
        Assert.Equal("https://img.example.com/unit-face.png", createdPrinting.ImageUrl);
    }

    private static Task InvokeUpsertAsync(
        ScryfallImporter importer,
        object scryCard,
        string setCode,
        ImportSummary summary,
        CancellationToken ct = default)
    {
        var task = (Task?)UpsertMethod.Invoke(importer, new object?[] { scryCard, setCode, summary, ct });
        return task ?? Task.CompletedTask;
    }

    private static object CreateScryCard(
        string id,
        string name,
        string? typeLine,
        string? oracleText,
        string? rarity,
        string collectorNumber,
        string? setCode,
        List<string>? finishes,
        string? imageUrl,
        IList? faces)
    {
        var images = imageUrl is null ? null : CreateScryImages(imageUrl);
        return Activator.CreateInstance(
            ScryCardType,
            id,
            name,
            typeLine,
            oracleText,
            rarity,
            collectorNumber,
            setCode,
            finishes,
            images,
            faces) ?? throw new InvalidOperationException("Failed to create ScryCard instance.");
    }

    private static object CreateScryFace(string? name, string? oracleText, object? imageUris)
        => Activator.CreateInstance(ScryFaceType, name, oracleText, imageUris)
           ?? throw new InvalidOperationException("Failed to create ScryFace instance.");

    private static object CreateScryImages(string? normal)
        => Activator.CreateInstance(ScryImagesType, null, normal, null)
           ?? throw new InvalidOperationException("Failed to create ScryImages instance.");

    private static IList CreateFaceList(object face)
    {
        var listType = typeof(List<>).MakeGenericType(ScryFaceType);
        var list = (IList?)Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException("Failed to create face list.");
        list.Add(face);
        return list;
    }

    private sealed class ScryfallHandler(params (string Set, string? ReleasedAt)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(string Set, string? ReleasedAt)> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (set, releasedAt) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new[] { new { id = set, name = "Chronology Test Card", type_line = "Instant",
                        collector_number = "1", set, released_at = releasedAt, rarity = "common" } },
                    has_more = false
                })
            });
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler? handler = null) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = handler is null ? new() : new(handler);

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }
}
