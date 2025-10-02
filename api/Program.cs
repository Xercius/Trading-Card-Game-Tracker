using api.Common;
using api.Data;
using api.Middleware;
using api.Models;
using api.Importing;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

builder.Services.AddHttpClient();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = DummyAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = DummyAuthenticationHandler.SchemeName;
        options.DefaultForbidScheme = DummyAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, DummyAuthenticationHandler>(
        DummyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.ScryfallImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.SwccgdbImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.LorcanaJsonImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.SwuDbImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.PokemonTcgImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.FabDbImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.GuardiansLocalImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.DiceMastersDbImporter>();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.TransformersFmImporter>();
builder.Services.AddSingleton<api.Importing.ImporterRegistry>();

// Explicit HTTPS port for redirects
builder.Services.AddHttpsRedirection(o => o.HttpsPort = 7226);

// CORS: Vite only
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", p => p
        .WithOrigins("http://localhost:5173")
        .WithHeaders("X-User-Id", "Content-Type")
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH"));
});

var app = builder.Build();

// DB migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Pipeline
app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserContextMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
