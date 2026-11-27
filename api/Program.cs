using api.Data;
using api.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiBasics(builder.Configuration);
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

// DB migrate (skip in dedicated testing environment; never seed production data on startup)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (args.Any(a => string.Equals(a, "seed", StringComparison.OrdinalIgnoreCase)))
{
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperApi();
}

app.UseApiPipeline();

app.MapApiEndpoints();

app.Run();

public partial class Program { }
