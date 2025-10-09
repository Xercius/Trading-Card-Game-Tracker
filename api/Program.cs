using api.Data;
using api.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var isSeedCommand = args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase);
var filteredArgs = isSeedCommand ? args[1..] : args;

var builder = WebApplication.CreateBuilder(filteredArgs);

builder.Services.AddApiBasics();
var usingDevFallbackKey = builder.Services.AddAppServices(builder.Configuration, builder.Environment);

var app = builder.Build();

if (usingDevFallbackKey)
{
    app.Logger.LogWarning("Using built-in development JWT signing key. Provide JWT__KEY before deploying to Production.");
}

if (isSeedCommand)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    MinimalDbSeeder.Seed(db);
    return;
}

// DB migrate + seed (skip in dedicated testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperApi();
}

app.UseApiPipeline();

app.MapApiEndpoints();

app.Run();

public partial class Program { }
