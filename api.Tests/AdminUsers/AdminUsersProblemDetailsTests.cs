using api.Common.Errors;
using api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests.AdminUsers;

public class AdminUsersProblemDetailsTests(TestingWebAppFactory factory) : IClassFixture<TestingWebAppFactory>
{
    private readonly TestingWebAppFactory _factory = factory;

    private async Task SeedDataAsync()
    {
        await _factory.ResetStateAsync();
        await _factory.ExecuteDbContextAsync(Seed.SeedAsync);
    }

    [Fact]
    public async Task DeletingLastAdmin_ReturnsConflictProblemWithCollisionDetail()
    {
        await SeedDataAsync();
        using var client = _factory.CreateClientForUser(Seed.AdminUserId);

        var response = await client.DeleteAsync($"/api/admin/users/{Seed.AdminUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().Be(ProblemTypes.Conflict.Type);
        problem.Title.Should().Be("Cannot remove last administrator");
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("At least one administrator must remain.");
        problem.Detail.Should().Contain("administrator");
        problem.Instance.Should().Be($"/api/admin/users/{Seed.AdminUserId}");
        problem.Extensions.Should().ContainKey("traceId");
    }
}
