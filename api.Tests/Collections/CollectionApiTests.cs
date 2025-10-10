using api.Common.Errors;
using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.Collections;

public class CollectionApiTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task GetCollection_ForCurrentUser_ReturnsOnlyOwnedEntries()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/collection");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CollectionItemContract>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().NotBeEmpty();
        payload.Items.Should().Contain(i => i.CardPrintingId == Seed.LightningAlphaPrintingId);
        payload.Items.Should().Contain(i => i.CardPrintingId == Seed.LightningBetaPrintingId);
        payload.Items.Should().Contain(i => i.CardPrintingId == Seed.PhoenixPrintingId);
        payload.Items.Should().Contain(i => i.CardPrintingId == Seed.GoblinPrintingId);
        payload.Items.Should().NotContain(i => i.CardPrintingId == Seed.DragonPrintingId);
    }

    [Fact]
    public async Task QuickAdd_IncrementsQuantityOwned()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/collection/items", new
        {
            printingId = Seed.GoblinPrintingId,
            quantity = 2
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickAddResponseContract>();
        result.Should().NotBeNull();
        result!.PrintingId.Should().Be(Seed.GoblinPrintingId);
        result.QuantityOwned.Should().Be(3);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var card = await db.UserCards.FindAsync(Seed.AdminUserId, Seed.GoblinPrintingId);
            card.Should().NotBeNull();
            card!.QuantityOwned.Should().Be(3);
        });
    }

    [Fact]
    public async Task PostCollection_WithInvalidQuantities_ReturnsValidationProblem()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync("/api/collection", new
        {
            cardPrintingId = Seed.GoblinPrintingId,
            quantityOwned = -1,
            quantityWanted = 0,
            quantityProxyOwned = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Title.Should().Be(ProblemTypes.BadRequest.Title);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be(ProblemTypes.BadRequest.DefaultDetail);
        problem.Instance.Should().Be("/api/collection");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Errors.Should().ContainKey("QuantityOwned");
        problem.Errors["QuantityOwned"].Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCollection_WithoutUserHeader_ReturnsMissingHeaderProblem()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/collection");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCollectionItem_RemovesRow()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.DeleteAsync($"/api/collection/{Seed.LightningAlphaPrintingId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var card = await db.UserCards.FindAsync(Seed.AdminUserId, Seed.LightningAlphaPrintingId);
            card.Should().BeNull();
        });
    }

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    private sealed record CollectionItemContract(int CardPrintingId, int QuantityOwned, int QuantityWanted, int QuantityProxyOwned, int Availability, int AvailabilityWithProxies, int CardId, string CardName, string Game, string Set, string Number, string Rarity, string Style, string? ImageUrl);

    private sealed record QuickAddResponseContract(int PrintingId, int QuantityOwned);
}
