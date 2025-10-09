namespace api.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "TradingCardGameTracker";
    public string Audience { get; set; } = "TradingCardGameTracker";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 120;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes <= 0 ? 120 : AccessTokenLifetimeMinutes);
}
