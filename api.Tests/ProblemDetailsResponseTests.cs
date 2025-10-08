using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Common.Errors;
using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        problem.Detail.Should().NotBeNullOrWhiteSpace();
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
