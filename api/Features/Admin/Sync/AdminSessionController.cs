using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using api.Data;
using api.Infrastructure.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin.Sync;

[ApiController]
[Route("api/admin/sync/session")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSessionController : ControllerBase
{
    /// <summary>Returns browser session state and a request verification token, never an API credential.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<AdminSessionResponse>> GetSession(IAntiforgery antiforgery)
    {
        var session = await HttpContext.AuthenticateAsync(AdminSession.Scheme);
        HttpContext.User = session.Succeeded ? session.Principal! : new ClaimsPrincipal(new ClaimsIdentity());
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new AdminSessionResponse(session.Succeeded, tokens.RequestToken!);
    }

    /// <summary>Creates a short-lived sync-only administrator session from provisioned credentials.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("admin-login")]
    public async Task<IActionResult> Login(
        AdminLoginRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Username == request.Username, cancellationToken);
        var verifier = HttpContext.RequestServices.GetRequiredService<AdminCredentialVerifier>();
        if (!verifier.Verify(user, request.Password))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid administrator credentials.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user!.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(AdminSession.PasswordStamp, AdminSession.Stamp(user.PasswordHash!))
        ], AdminSession.Scheme);
        await HttpContext.SignInAsync(AdminSession.Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
        return NoContent();
    }

    /// <summary>Removes the browser's administrator session.</summary>
    [HttpDelete]
    [Authorize(AuthenticationSchemes = AdminSession.Scheme, Roles = "Admin")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AdminSession.Scheme);
        return NoContent();
    }
}

public sealed record AdminLoginRequest(
    [Required, StringLength(200)] string Username,
    [Required, StringLength(1024)] string Password);

public sealed record AdminSessionResponse(bool Authenticated, string CsrfToken);
