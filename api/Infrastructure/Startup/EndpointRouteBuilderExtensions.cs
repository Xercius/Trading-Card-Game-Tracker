using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace api.Infrastructure.Startup;

internal static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }
}
