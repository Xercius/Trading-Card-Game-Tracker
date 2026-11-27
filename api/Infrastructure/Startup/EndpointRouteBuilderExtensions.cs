

using api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace api.Infrastructure.Startup;

internal static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.Map("/api/{**slug}", async (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = ProblemTypes.NotFound.Title,
                Type = ProblemTypes.NotFound.Type,
                Instance = context.Request.Path
            };

            var problemDetailsService = context.RequestServices.GetService<IProblemDetailsService>();
            if (problemDetailsService is not null)
            {
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = problem
                });
                return;
            }

            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        });
        endpoints.MapFallbackToFile("index.html");
        return endpoints;
    }
}
