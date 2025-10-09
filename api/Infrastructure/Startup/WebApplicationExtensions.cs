using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace api.Infrastructure.Startup;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
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

        return app;
    }

    public static WebApplication UseDeveloperApi(this WebApplication app)
    {
        // Enable developer exception page for detailed error information
        app.UseDeveloperExceptionPage();
        // Enable Swagger UI for API documentation (if Swagger is registered)
        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }
}
