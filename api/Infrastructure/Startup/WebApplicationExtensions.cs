using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace api.Infrastructure.Startup;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

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

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsPolicyOptions>>().Value;
        app.UseCors(string.IsNullOrWhiteSpace(corsOptions.PolicyName) ? "AllowReact" : corsOptions.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication UseDeveloperApi(this WebApplication app)
    {
        // Enable developer exception page for detailed error information
        app.UseDeveloperExceptionPage();
        // Enable Swagger UI for API documentation (if Swagger is registered)
        return app;
    }
}
