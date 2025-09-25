using api.Data;
using api.Middleware;
using api.Models;
using api.Importing;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

builder.Services.AddHttpClient();
builder.Services.AddScoped<api.Importing.ISourceImporter, api.Importing.ScryfallImporter>();
builder.Services.AddScoped<api.Importing.ImporterRegistry>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        b => b.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}


app.UseHttpsRedirection();

app.UseCors("AllowReact");

app.UseAuthorization();
app.UseMiddleware<UserContextMiddleware>();

app.MapControllers();

app.Run();




