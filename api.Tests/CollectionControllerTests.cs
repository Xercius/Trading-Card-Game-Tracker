// Run these tests from Visual Studio Test Explorer by opening api.sln, building the solution,
// and selecting "Run All Tests" or right-clicking individual tests in Test Explorer.

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using api.Data;
using api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace api.Tests;

public sealed class CollectionControllerTests : IClassFixture<CollectionApiFactory>
{
    private readonly CollectionApiFactory _factory;

    public CollectionControllerTests(CollectionApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_ApiCollection_WithFilters_ReturnsOnlyCallerRows()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var allResponse = await client.GetAsync("/api/collection");
        allResponse.EnsureSuccessStatusCode();
        var allItems = await ReadCollectionAsync(allResponse);

        var expectedIds = new[]
        {
            CollectionApiFactory.CardPrintingAlphaCommonId,
            CollectionApiFactory.CardPrintingMysticShieldId
        };
        Assert.Equal(expectedIds.Order().ToArray(), allItems.Select(i => i.CardPrintingId).Order().ToArray());
        Assert.DoesNotContain(allItems, item => item.CardPrintingId == CollectionApiFactory.CardPrintingStarSaberId);

        var filterResponse = await client.GetAsync(
            $"/api/collection?game=Mythic+Battles&set=Mystic+Storm&rarity=Uncommon&name=Shield");
        filterResponse.EnsureSuccessStatusCode();
        var filtered = await ReadCollectionAsync(filterResponse);

        var single = Assert.Single(filtered);
        Assert.Equal(CollectionApiFactory.CardPrintingMysticShieldId, single.CardPrintingId);

        var specificResponse = await client.GetAsync($"/api/collection?cardPrintingId={CollectionApiFactory.CardPrintingAlphaRareId}");
        specificResponse.EnsureSuccessStatusCode();
        var specific = await ReadCollectionAsync(specificResponse);
        Assert.Empty(specific); // card belongs to other user until created later
    }

    [Fact]
    public async Task Get_ApiCollection_WithoutUserHeader_ReturnsForbidden()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/collection");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ApiCollection_Upsert_CreatesAndUpdatesWithClamping()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = CollectionApiFactory.CardPrintingAlphaRareId,
                quantityOwned = 4,
                quantityWanted = 1,
                quantityProxyOwned = 2
            });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, createResponse.StatusCode);

        var createdItems = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaRareId);
        var created = Assert.Single(createdItems);
        Assert.Equal(4, created.QuantityOwned);
        Assert.Equal(1, created.QuantityWanted);
        Assert.Equal(2, created.QuantityProxyOwned);

        var updateResponse = await client.PostAsJsonAsync(
            "/api/collection",
            new
            {
                cardPrintingId = CollectionApiFactory.CardPrintingAlphaRareId,
                quantityOwned = -5,
                quantityWanted = 3,
                quantityProxyOwned = -1
            });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updatedItems = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaRareId);
        var updated = Assert.Single(updatedItems);
        Assert.Equal(0, updated.QuantityOwned);
        Assert.Equal(3, updated.QuantityWanted);
        Assert.Equal(0, updated.QuantityProxyOwned);
    }

    [Fact]
    public async Task Put_ApiCollection_SetQuantities_UpdatesAndClamps()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var response = await client.PutAsJsonAsync(
            $"/api/collection/{CollectionApiFactory.CardPrintingMysticShieldId}",
            new
            {
                quantityOwned = -10,
                quantityWanted = 6,
                quantityProxyOwned = 4
            });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var items = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingMysticShieldId);
        var item = Assert.Single(items);
        Assert.Equal(0, item.QuantityOwned);
        Assert.Equal(6, item.QuantityWanted);
        Assert.Equal(4, item.QuantityProxyOwned);
    }

    [Fact]
    public async Task Put_ApiCollection_WhenRowMissing_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var response = await client.PutAsJsonAsync(
            $"/api/collection/{CollectionApiFactory.CardPrintingStarSaberId}",
            new
            {
                quantityOwned = 1,
                quantityWanted = 0,
                quantityProxyOwned = 0
            });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_ApiCollection_AllowsPartialUpdatesAndClamps()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var patchContent = JsonContent.Create(new { quantityOwned = -2 });
        var response = await client.PatchAsync($"/api/collection/{CollectionApiFactory.CardPrintingAlphaCommonId}", patchContent);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var items = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaCommonId);
        var item = Assert.Single(items);
        Assert.Equal(0, item.QuantityOwned);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonWanted, item.QuantityWanted);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonProxy, item.QuantityProxyOwned);

        var secondPatch = JsonContent.Create(new { quantityWanted = 5 });
        var secondResponse = await client.PatchAsync($"/api/collection/{CollectionApiFactory.CardPrintingAlphaCommonId}", secondPatch);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, secondResponse.StatusCode);

        var afterSecond = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaCommonId);
        var updated = Assert.Single(afterSecond);
        Assert.Equal(0, updated.QuantityOwned);
        Assert.Equal(5, updated.QuantityWanted);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonProxy, updated.QuantityProxyOwned);
    }

    [Fact]
    public async Task Post_ApiCollectionDelta_AppliesChangesAndCreatesRows()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var response = await client.PostAsJsonAsync(
            "/api/collection/delta",
            new[]
            {
                new
                {
                    cardPrintingId = CollectionApiFactory.CardPrintingAlphaCommonId,
                    deltaOwned = -1,
                    deltaWanted = 2,
                    deltaProxyOwned = 0
                },
                new
                {
                    cardPrintingId = CollectionApiFactory.CardPrintingAlphaRareId,
                    deltaOwned = 3,
                    deltaWanted = 1,
                    deltaProxyOwned = 2
                }
            });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var existingItems = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaCommonId);
        var existing = Assert.Single(existingItems);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonOwned - 1, existing.QuantityOwned);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonWanted + 2, existing.QuantityWanted);
        Assert.Equal(CollectionApiFactory.SeededAlphaCommonProxy, existing.QuantityProxyOwned);

        var newItems = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaRareId);
        var created = Assert.Single(newItems);
        Assert.Equal(3, created.QuantityOwned);
        Assert.Equal(1, created.QuantityWanted);
        Assert.Equal(2, created.QuantityProxyOwned);
    }

    [Fact]
    public async Task Post_ApiCollectionDelta_WithInvalidCardPrinting_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var response = await client.PostAsJsonAsync(
            "/api/collection/delta",
            new[]
            {
                new
                {
                    cardPrintingId = 9999,
                    deltaOwned = 1,
                    deltaWanted = 0,
                    deltaProxyOwned = 0
                }
            });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ApiCollection_RemovesRow()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var deleteResponse = await client.DeleteAsync($"/api/collection/{CollectionApiFactory.CardPrintingAlphaCommonId}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var items = await GetCollectionAsync(client, CollectionApiFactory.CardPrintingAlphaCommonId);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Get_LegacyRoute_WithDifferentUser_ReturnsForbidden()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClientWithUser(CollectionApiFactory.UserAliceId);

        var response = await client.GetAsync($"/api/user/{CollectionApiFactory.UserBobId}/collection");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(HttpClient client, int cardPrintingId)
    {
        var response = await client.GetAsync($"/api/collection?cardPrintingId={cardPrintingId}");
        response.EnsureSuccessStatusCode();
        return await ReadCollectionAsync(response);
    }

    private static async Task<IReadOnlyList<CollectionItem>> ReadCollectionAsync(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        var items = await response.Content.ReadFromJsonAsync<List<CollectionItem>>(options);
        return items ?? new List<CollectionItem>();
    }

    private sealed record CollectionItem(
        int CardPrintingId,
        int QuantityOwned,
        int QuantityWanted,
        int QuantityProxyOwned,
        int CardId,
        string CardName,
        string Game,
        string Set,
        string Number,
        string Rarity,
        string Style,
        string? ImageUrl);
}

public sealed class CollectionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const int UserAliceId = 1;
    public const int UserBobId = 2;

    public const int CardAlphaId = 100;
    public const int CardMysticId = 200;
    public const int CardStarSaberId = 300;

    public const int CardPrintingAlphaCommonId = 1001;
    public const int CardPrintingAlphaRareId = 1002;
    public const int CardPrintingMysticShieldId = 1003;
    public const int CardPrintingStarSaberId = 1004;

    public const int SeededAlphaCommonOwned = 3;
    public const int SeededAlphaCommonWanted = 1;
    public const int SeededAlphaCommonProxy = 1;

    private SqliteConnection _connection = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
            SeedAsync(db).GetAwaiter().GetResult();
        });
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        _ = Server;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    public HttpClient CreateClientWithUser(int userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var cardAlpha = new Card
        {
            Id = CardAlphaId,
            Game = "Mythic Battles",
            Name = "Alpha Dragon",
            CardType = "Unit",
            Description = "Leader of the skies"
        };

        var cardMystic = new Card
        {
            Id = CardMysticId,
            Game = "Mythic Battles",
            Name = "Mystic Shield",
            CardType = "Spell",
            Description = "Protective incantation"
        };

        var cardStarSaber = new Card
        {
            Id = CardStarSaberId,
            Game = "Galaxy Clash",
            Name = "Star Saber",
            CardType = "Weapon",
            Description = "Cuts through the cosmos"
        };

        var alphaCommon = new CardPrinting
        {
            Id = CardPrintingAlphaCommonId,
            Card = cardAlpha,
            Set = "Alpha Rising",
            Number = "A-001",
            Rarity = "Common",
            Style = "Standard",
            ImageUrl = "https://example.com/a-001.png"
        };

        var alphaRare = new CardPrinting
        {
            Id = CardPrintingAlphaRareId,
            Card = cardAlpha,
            Set = "Alpha Rising",
            Number = "A-001R",
            Rarity = "Rare",
            Style = "Foil",
            ImageUrl = "https://example.com/a-001r.png"
        };

        var mysticShield = new CardPrinting
        {
            Id = CardPrintingMysticShieldId,
            Card = cardMystic,
            Set = "Mystic Storm",
            Number = "M-010",
            Rarity = "Uncommon",
            Style = "Standard",
            ImageUrl = "https://example.com/m-010.png"
        };

        var starSaber = new CardPrinting
        {
            Id = CardPrintingStarSaberId,
            Card = cardStarSaber,
            Set = "Starfall",
            Number = "S-099",
            Rarity = "Legendary",
            Style = "Standard",
            ImageUrl = "https://example.com/s-099.png"
        };

        db.Users.AddRange(
            new User { Id = UserAliceId, Username = "alice", DisplayName = "Alice" },
            new User { Id = UserBobId, Username = "bob", DisplayName = "Bob" }
        );

        db.Cards.AddRange(cardAlpha, cardMystic, cardStarSaber);
        db.CardPrintings.AddRange(alphaCommon, alphaRare, mysticShield, starSaber);

        db.UserCards.AddRange(
            new UserCard
            {
                UserId = UserAliceId,
                CardPrintingId = CardPrintingAlphaCommonId,
                QuantityOwned = SeededAlphaCommonOwned,
                QuantityWanted = SeededAlphaCommonWanted,
                QuantityProxyOwned = SeededAlphaCommonProxy
            },
            new UserCard
            {
                UserId = UserAliceId,
                CardPrintingId = CardPrintingMysticShieldId,
                QuantityOwned = 1,
                QuantityWanted = 2,
                QuantityProxyOwned = 0
            },
            new UserCard
            {
                UserId = UserBobId,
                CardPrintingId = CardPrintingAlphaRareId,
                QuantityOwned = 2,
                QuantityWanted = 1,
                QuantityProxyOwned = 0
            }
        );

        await db.SaveChangesAsync();
    }
}
