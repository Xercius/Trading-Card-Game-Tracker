using api.Data;
using api.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace api.Tests.Infrastructure.Startup;

public sealed class DatabaseConfigurationTests
{
    [Theory]
    [InlineData(null, "Data Source=app.db")]
    [InlineData("Data Source=:memory:", "Data Source=:memory:")]
    [InlineData("Data Source=disposable.db;Pooling=False", "Data Source=disposable.db;Pooling=False")]
    public void AddAppServices_WithDatabaseConfiguration_SelectsExpectedConnection(
        string? connectionString, string expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddAppServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(expected, db.Database.GetConnectionString());
    }

    [Fact]
    public void CreateDbContext_WithoutOverride_UsesExistingDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var db = AppDbContextFactory.CreateDbContext(configuration);

        Assert.Equal("Data Source=app.db", db.Database.GetConnectionString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrate_WithOverride_InitializesSelectedDatabase(bool useDesignTimeFactory)
    {
        var directory = Directory.CreateTempSubdirectory("tcg-database-tests-");
        var databasePath = Path.Combine(directory.FullName, "disposable.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddAppServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        try
        {
            using var db = useDesignTimeFactory
                ? new AppDbContextFactory().CreateDbContext(
                    ["--ConnectionStrings:DefaultConnection", connectionString])
                : scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal(databasePath, db.Database.GetDbConnection().DataSource);
            Assert.False(File.Exists(databasePath));

            await db.Database.MigrateAsync();

            Assert.True(File.Exists(databasePath));
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Empty(await db.Users.ToListAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
