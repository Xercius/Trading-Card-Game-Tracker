using api.Infrastructure.Startup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace api.Infrastructure.Auth;

internal sealed class AdminSyncAntiforgeryFilter(
    IOptions<CorsPolicyOptions> corsOptions,
    ProblemDetailsFactory problemDetailsFactory) : IAsyncAuthorizationFilter
{
    private const string RequestedWithHeader = "X-Requested-With";

    /// <summary>Blocks cross-site browser mutations to unauthenticated sync endpoints unless accompanied by an allowed CORS origin, custom request header, or admin bearer token.</summary>
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments("/api/admin/sync") ||
            HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            context.HttpContext.User.Identities.Any(identity =>
                identity.IsAuthenticated && identity.AuthenticationType == AdminTokenAuthenticationHandler.SchemeName))
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(request.Headers[RequestedWithHeader]))
        {
            return Task.CompletedTask;
        }

        var origin = request.Headers.Origin.ToString().TrimEnd('/');
        var allowedOrigins = corsOptions.Value.Origins ?? Array.Empty<string>();
        if (!string.IsNullOrEmpty(origin) &&
            allowedOrigins.Any(allowed => string.Equals(allowed.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        var problem = problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            title: "Cross-site sync requests are not allowed.");
        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
        return Task.CompletedTask;
    }
}
