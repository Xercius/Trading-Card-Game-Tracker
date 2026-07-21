using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace api.Infrastructure.Auth;

internal sealed class AdminTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    IConfiguration configuration,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "AdminToken";
    private const string ConfigurationKey = "Authentication:AdminApiToken";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredToken = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("Admin API token is not configured."));
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token),
                System.Text.Encoding.UTF8.GetBytes(configuredToken)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API token."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin-sync"), new Claim(ClaimTypes.Role, "Admin")],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
