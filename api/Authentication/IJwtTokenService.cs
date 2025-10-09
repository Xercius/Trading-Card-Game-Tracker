using api.Models;

namespace api.Authentication;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(User user);
}

public sealed record JwtTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
