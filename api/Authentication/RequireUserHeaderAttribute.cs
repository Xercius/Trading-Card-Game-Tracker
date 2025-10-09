using Microsoft.AspNetCore.Authorization;

namespace api.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireUserHeaderAttribute : AuthorizeAttribute
{
    public RequireUserHeaderAttribute()
    {
        AuthenticationSchemes = HeaderUserAuthenticationHandler.SchemeName;
    }
}
