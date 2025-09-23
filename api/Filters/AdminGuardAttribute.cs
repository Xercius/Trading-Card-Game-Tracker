using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using api.Middleware; // for CurrentUser extension

namespace api.Filters
{
    /// <summary>Allows when (IsLocalhost || IsAdmin). Otherwise 403.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AdminGuardAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;

            // 1) Localhost check
            var remoteIp = http.Connection.RemoteIpAddress;
            var isLocal =
                remoteIp is null ||
                IPAddress.IsLoopback(remoteIp) ||
                // handle IPv6 mapped loopback and dev proxies
                (http.Request.Host.Host?.Equals("localhost", StringComparison.OrdinalIgnoreCase) ?? false);

            // 2) User.IsAdmin check (set by your UserContextMiddleware)
            var currentUser = http.GetCurrentUser();
            var isAdmin = currentUser?.IsAdmin == true;

            if (isLocal || isAdmin)
            {
                base.OnActionExecuting(context);
                return;
            }

            // Return 403 directly (do NOT call Forbid() since no auth scheme is configured)
            context.Result = new ObjectResult(new { error = "Forbidden: admin or localhost required." }) { StatusCode = 403 };
        }
    }
}