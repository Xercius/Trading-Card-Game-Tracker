using System.Security.Claims;

namespace api.Authentication;

public sealed record CurrentUser(int Id, string Username, bool IsAdmin);

public static class CurrentUserExtensions
{
    public static CurrentUser? GetCurrentUser(this HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var principal = context.User;
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        if (idClaim is null || !int.TryParse(idClaim.Value, out var id))
        {
            return null;
        }

        var username = principal.FindFirst("username")?.Value ?? string.Empty;
        var isAdmin = string.Equals(principal.FindFirst("is_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        return new CurrentUser(id, username, isAdmin);
    }
}
