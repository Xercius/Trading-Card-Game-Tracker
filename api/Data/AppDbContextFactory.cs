using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(AppDbContextFactory).Assembly.GetName().Name
        });
        return CreateDbContext(builder.Configuration);
    }

    internal static AppDbContext CreateDbContext(IConfiguration configuration)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db")
            .Options;
        return new AppDbContext(opts);
    }
}
