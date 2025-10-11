using api.Common.Errors;
using api.Shared.Importing;
using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace api.Tests;

public class ProblemDetailsResponseTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task InvalidQuery_ReturnsValidationProblemDetailsWithTraceId()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/cards?skip=foo");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Errors.Should().ContainKey("skip");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Instance.Should().Be("/api/cards");
    }

    [Fact]
    public async Task ValueRefresh_MissingGame_ReturnsValidationProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var payload = new[]
        {
            new
            {
                cardPrintingId = Seed.LightningAlphaPrintingId,
                priceCents = 1000L,
                source = "test"
            }
        };

        var response = await client.PostAsJsonAsync("/api/value/refresh", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Title.Should().Be(ProblemTypes.BadRequest.Title);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be("The refresh request must specify a game.");
        problem.Instance.Should().Be("/api/value/refresh");
        problem.Errors.Should().ContainKey("game")
            .WhoseValue.Should().Contain("The 'game' query parameter is required.");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task CardsPrinting_InvalidPayload_ReturnsValidationProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.PostAsJsonAsync(
            "/api/cards/printing",
            new
            {
                cardId = 0,
                set = "Alpha",
                number = "A1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Title.Should().Be(ProblemTypes.BadRequest.Title);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be(ProblemTypes.BadRequest.DefaultDetail);
        problem.Instance.Should().Be("/api/cards/printing");
        problem.Errors.Should().ContainKey("CardId")
            .WhoseValue.Should().Contain("CardId must be provided.");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task GetCard_MissingResource_ReturnsNotFoundProblemDetailsWithDetail()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/cards/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.NotFound.Type);
        problem.Title.Should().Be(ProblemTypes.NotFound.Title);
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Detail.Should().Be("Card 999999 was not found.");
        problem.Instance.Should().Be("/api/cards/999999");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task AdminImportDryRun_InvalidJson_ReturnsBadRequestProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        using var content = new StringContent("{\"source\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/import/dry-run", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Title.Should().Be("Invalid request.");
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be(ProblemTypes.BadRequest.DefaultDetail);
        problem.Instance.Should().Be("/api/admin/import/dry-run");
        problem.Errors.Should().ContainKey("request")
            .WhoseValue.Should().Contain("The request body could not be parsed as JSON.");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task AdminImportDryRun_InvalidLimit_ReturnsBadRequestProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/admin/import/dry-run?limit=invalid", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.BadRequest.Type);
        problem.Title.Should().Be("Invalid limit");
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be(ProblemTypes.BadRequest.DefaultDetail);
        problem.Instance.Should().Be("/api/admin/import/dry-run");
        problem.Errors.Should().ContainKey("limit")
            .WhoseValue.Should().Contain($"limit must be between {ImportingOptions.MinPreviewLimit} and {ImportingOptions.MaxPreviewLimit}");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task MissingResource_ReturnsNotFoundProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.GetAsync("/api/user/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Type.Should().Be(ProblemTypes.NotFound.Type);
        problem.Title.Should().Be(ProblemTypes.NotFound.Title);
        problem.Detail.Should().Be("User 99999 was not found.");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Instance.Should().Be("/api/user/99999");
    }

    [Fact]
    public async Task DeletingLastAdmin_ReturnsConflictProblemDetails()
    {
        await SeedAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.DeleteAsync($"/api/admin/users/{Seed.AdminUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Type.Should().Be(ProblemTypes.Conflict.Type);
        problem.Title.Should().Be("Cannot remove last administrator");
        problem.Detail.Should().Be("At least one administrator must remain.");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Instance.Should().Be($"/api/admin/users/{Seed.AdminUserId}");
    }
}
