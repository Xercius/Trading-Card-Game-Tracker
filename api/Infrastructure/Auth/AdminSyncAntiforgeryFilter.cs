using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api.Infrastructure.Auth;

internal sealed class AdminSyncAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    /// <summary>Protects sync browser mutations while preserving authenticated server token clients.</summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments(AdminSession.Path) ||
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
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request verification token."
            });
        }
    }
}
