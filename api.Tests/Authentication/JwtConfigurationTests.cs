using api.Authentication;
using api.Data;
using api.Models;
using api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace api.Tests.Authentication;

public sealed class JwtConfigurationTests
{
    [Fact]
    public void ProductionEnvironment_WithShortKey_ThrowsDuringStartup()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = "short"
                    });
                });
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("256 bits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevelopmentEnvironment_WithEmptyKey_UsesDevFallbackAsync()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = string.Empty
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                    services.AddSingleton(connection);
                    services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
                });
            });

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.CreateToken(new User
        {
            Id = 42,
            Username = "dev-user",
            DisplayName = "Dev User",
            IsAdmin = false
        });

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }

    [Fact]
    public void TestingFactory_ProvidesUsableTokenService()
    {
        using var factory = new TestingWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var token = tokenService.CreateToken(new User
        {
            Id = 7,
            Username = "test-user",
            DisplayName = "Test User",
            IsAdmin = false
        });

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }
}
