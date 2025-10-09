using api.Common;
using api.Common.Errors;
using api.Data;
using api.Authentication;
using api.Models;
using api.Importing;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

var isSeedCommand = args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase);
var filteredArgs = isSeedCommand ? args[1..] : args;

var builder = WebApplication.CreateBuilder(filteredArgs);

// Services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();

            var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState);

            return new ObjectResult(problemDetails)
            {
                StatusCode = problemDetails.Status ?? StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.Configure<ProblemDetailsOptions>(options =>
{
    options.ClientErrorMapping[StatusCodes.Status400BadRequest] = new ClientErrorData
    {
        Link = ProblemTypes.BadRequest.Type,
        Title = ProblemTypes.BadRequest.Title
    };

    options.ClientErrorMapping[StatusCodes.Status404NotFound] = new ClientErrorData
    {
        Link = ProblemTypes.NotFound.Type,
        Title = ProblemTypes.NotFound.Title
    };

    options.ClientErrorMapping[StatusCodes.Status409Conflict] = new ClientErrorData
    {
        Link = ProblemTypes.Conflict.Type,
        Title = ProblemTypes.Conflict.Title
    };
});

builder.Services.AddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

builder.Services.AddHttpClient();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = HeaderUserAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = HeaderUserAuthenticationHandler.SchemeName;
        options.DefaultForbidScheme = HeaderUserAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, HeaderUserAuthenticationHandler>(
        HeaderUserAuthenticationHandler.SchemeName,
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
builder.Services.AddScoped<api.Importing.ImporterRegistry>();
builder.Services.AddScoped<api.Shared.Importing.FileParser>();

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

// Pipeline
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problemDetailsFactory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problemDetails = problemDetailsFactory.CreateProblemDetails(
            context,
            context.Response.StatusCode);

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
