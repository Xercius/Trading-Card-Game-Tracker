using api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api.Tests.Infrastructure;

public sealed class TestingWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private bool _isDatabaseCreated;

    public TestingWebAppFactory()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    
    public new HttpClient CreateClient()
    {
        EnsureDatabaseCreated();
        return base.CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
        
        // Ensure database is created after all services are configured
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // This runs after all services are registered
        });
    }
    
    private void EnsureDatabaseCreated()
    {
        if (_isDatabaseCreated) return;
        
        _ = Server; // ensure host is built
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        
        _isDatabaseCreated = true;
    }

    public HttpClient CreateClientForUser(int userId)
    {
        EnsureDatabaseCreated();
        return CreateClient().WithUser(userId);
    }

    public async Task ResetStateAsync()
    {
        _ = Server; // ensure host is built
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        _isDatabaseCreated = true;
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        _ = Server;
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        _ = Server;
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    public Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
