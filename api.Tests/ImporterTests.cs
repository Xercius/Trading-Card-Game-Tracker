using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using api.Data;
using api.Importing;
using api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests;

public class ImporterTests : IClassFixture<CustomWebApplicationFactory>
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

    private readonly CustomWebApplicationFactory _factory;

    public ImporterTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ScryfallImporter_Upsert_CreatesAndUpdatesEntitiesCorrectly()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
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
        Assert.Equal("Deal 2 damage to any target", createdCard.Description);

        var createdPrinting = await db.CardPrintings.SingleAsync(p => p.Set == "UTS" && p.Number == "007");
        Assert.Equal(createdCard.Id, createdPrinting.CardId);
        Assert.Equal("rare", createdPrinting.Rarity);
        Assert.Equal("Foil", createdPrinting.Style);
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
        Assert.Equal("Front\nDraw a card", createdCard.Description);

        Assert.Equal("mythic", createdPrinting.Rarity);
        Assert.Equal("Standard", createdPrinting.Style);
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

    private sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new();

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }
}
