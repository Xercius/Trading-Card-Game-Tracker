

namespace api.Infrastructure.Startup;

internal sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";

    public string PolicyName { get; set; } = "AllowReact";

    public string[] Origins { get; set; } = new[]
    {
        "http://localhost:5173"
    };

    public string[] Headers { get; set; } = new[]
    {
        "Authorization",
        "Content-Type"
    };

    public string[] Methods { get; set; } = new[]
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "OPTIONS",
        "PATCH"
    };

    public bool AllowCredentials { get; set; }
        = false;

    public TimeSpan? PreflightMaxAge { get; set; }
        = null;
}
