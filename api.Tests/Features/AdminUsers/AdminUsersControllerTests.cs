using System.Net;
using System.Net.Http.Json;
using api.Features.Admin.Users;
using api.Tests.Fixtures;
using api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace api.Tests.Features.AdminUsers;

public sealed class AdminUsersControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task CreateRenameToggle_FlowsSuccessfully()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var createResponse = await client.PostAsJsonAsync("/api/admin/users", new { name = "Charlie" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal("Charlie", created.Name);
        Assert.False(created.IsAdmin);

        var renameResponse = await client.PutAsJsonAsync($"/api/admin/users/{created.Id}", new { name = "Charles" });
        renameResponse.EnsureSuccessStatusCode();

        var renamed = await renameResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(renamed);
        Assert.Equal("Charles", renamed!.Name);
        Assert.Equal("Charles", renamed.DisplayName);

        var promoteResponse = await client.PutAsJsonAsync($"/api/admin/users/{created.Id}", new { isAdmin = true });
        promoteResponse.EnsureSuccessStatusCode();

        var promoted = await promoteResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(promoted);
        Assert.True(promoted!.IsAdmin);
    }

    [Fact]
    public async Task Delete_NonLastAdmin_Succeeds()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var createResponse = await client.PostAsJsonAsync("/api/admin/users", new { name = "Daisy" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        Assert.NotNull(created);

        var promote = await client.PutAsJsonAsync($"/api/admin/users/{created!.Id}", new { isAdmin = true });
        promote.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/admin/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetFromJsonAsync<AdminUserResponse[]>("/api/admin/users");
        Assert.NotNull(listResponse);
        Assert.DoesNotContain(listResponse!, u => u.Id == created.Id);
    }

    [Fact]
    public async Task Delete_LastAdmin_ReturnsConflict()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient().WithUser(TestDataSeeder.AdminUserId);

        var response = await client.DeleteAsync($"/api/admin/users/{TestDataSeeder.AdminUserId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Cannot remove last administrator", problem!.Title);
    }
}
