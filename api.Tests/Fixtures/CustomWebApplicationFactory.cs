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
    private bool _isInitialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Ensure connection is opened before configuring services
        if (!_isInitialized)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            _isInitialized = true;
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        // Ensure connection is opened (may have already been opened in ConfigureWebHost)
        if (!_isInitialized)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
            _isInitialized = true;
        }
        
        // Seed the database after the service provider is fully configured
        _ = Server; // ensure host is built
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await TestDataSeeder.SeedAsync(db);
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
