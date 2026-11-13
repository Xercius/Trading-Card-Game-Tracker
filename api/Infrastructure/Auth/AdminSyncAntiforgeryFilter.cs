using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace api.Infrastructure.Auth;

internal sealed class AdminSyncAntiforgeryFilter(
    IAntiforgery antiforgery,
    ProblemDetailsFactory problemDetailsFactory) : IAsyncAuthorizationFilter
{
    /// <summary>Protects admin browser mutations while preserving authenticated server token clients.</summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments("/api/admin") ||
            HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            context.HttpContext.User.Identities.Any(identity =>
                identity.IsAuthenticated && identity.AuthenticationType == AdminTokenAuthenticationHandler.SchemeName))
        {
            return;
        }

        try
        {
            var session = await context.HttpContext.AuthenticateAsync(AdminSession.Scheme);
            if (session.Succeeded)
            {
                context.HttpContext.User = session.Principal!;
            }
            else if (!request.Path.StartsWithSegments("/api/admin/sync/session"))
            {
                return;
            }

            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                title: "Invalid request verification token.");
            context.Result = new BadRequestObjectResult(problem);
        }
    }
}
