using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using api.Middleware;

namespace api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAdminAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUser = context.HttpContext.GetCurrentUser();
        if (currentUser?.IsAdmin == true)
        {
            await next();
            return;
        }

        var problem = new ProblemDetails
        {
            Title = "Forbidden",
            Detail = "Administrator access required.",
            Status = StatusCodes.Status403Forbidden,
        };

        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
