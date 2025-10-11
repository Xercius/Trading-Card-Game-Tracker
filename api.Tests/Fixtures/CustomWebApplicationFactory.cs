// System.Net.Http implicit using provided by SDK; explicit directive removed.
using api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace api.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString = $"DataSource=file:tests_{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection _connection = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
            TestDataSeeder.SeedAsync(db).GetAwaiter().GetResult();
        });
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        _ = Server; // ensure host is created
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await TestDataSeeder.SeedAsync(db);
    }

    public HttpClient CreateClientForUser(int userId)
        => CreateClient().WithUser(userId);
}
