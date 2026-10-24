using api.Data;
using api.Infrastructure.Auth;
using api.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;

var isSeedCommand = args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase);
var isProvisionAdminCommand = args.Length > 0 && string.Equals(args[0], "provision-admin", StringComparison.OrdinalIgnoreCase);
var filteredArgs = isSeedCommand ? args[1..] : isProvisionAdminCommand ? [] : args;

var builder = WebApplication.CreateBuilder(filteredArgs);

builder.Services.AddApiBasics(builder.Configuration);
builder.Services.AddAppServices();

var app = builder.Build();

if (isProvisionAdminCommand)
{
    var password = Environment.GetEnvironmentVariable("TCG_ADMIN_PASSWORD");
    if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]) || args[1].Length > 200 ||
        string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 1024)
    {
        Console.Error.WriteLine("Usage: provision-admin <username>; set TCG_ADMIN_PASSWORD to a password of 12–1024 characters.");
        Environment.ExitCode = 1;
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AdminAccountProvisioner>().ProvisionAsync(args[1], password);
    Console.WriteLine("Administrator credentials provisioned.");
    return;
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
