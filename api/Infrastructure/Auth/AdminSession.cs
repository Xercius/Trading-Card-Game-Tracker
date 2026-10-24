using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using api.Data;
using api.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure.Auth;

internal static class AdminSession
{
    internal const string Scheme = "AdminSession";
    internal const string Path = "/api/admin/sync";
    internal const string CsrfHeader = "X-Admin-CSRF";
    internal const string PasswordStamp = "password-stamp";

    internal static string Stamp(string hash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));

    internal static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (!context.Request.Path.StartsWithSegments(Path) ||
            !int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            context.RejectPrincipal();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, context.HttpContext.RequestAborted);
        if (user is not { IsAdmin: true, PasswordHash: not null } ||
            Stamp(user.PasswordHash) != context.Principal?.FindFirstValue(PasswordStamp))
        {
            context.RejectPrincipal();
        }
    }
}

internal sealed class AdminCredentialVerifier(IPasswordHasher<User> hasher)
{
    // Unknown accounts still perform a password hash check to reduce account enumeration.
    private readonly string _dummyHash = hasher.HashPassword(new User(), Guid.NewGuid().ToString());

    internal bool Verify(User? user, string password)
    {
        try
        {
            var result = hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? _dummyHash, password);
            return user is { IsAdmin: true, PasswordHash: not null } &&
                result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
