using System.Net;
using api.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api.Filters;

/// <summary>Allows only loopback requests or authenticated administrators.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminGuardAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;
        var remoteIp = http.Connection.RemoteIpAddress;
        var isLoopback = remoteIp is null || IPAddress.IsLoopback(remoteIp);

        if (isLoopback)
        {
            base.OnActionExecuting(context);
            return;
        }

        var currentUser = http.GetCurrentUser();
        if (currentUser?.IsAdmin == true)
        {
            base.OnActionExecuting(context);
            return;
        }

        context.Result = new ForbidResult();
    }
}
