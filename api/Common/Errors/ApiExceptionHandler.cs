using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace api.Common.Errors;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/> responses via <see cref="IProblemDetailsService"/>.
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly EventId UnhandledExceptionEventId = new(5000, "UnhandledException");

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var sanitizedMethod = SanitizeForLog(httpContext.Request.Method);
        var sanitizedPath = SanitizeForLog(httpContext.Request.Path.Value);

        logger.LogError(UnhandledExceptionEventId, exception, "Unhandled exception processing {Method} {Path}", sanitizedMethod, sanitizedPath);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = ProblemTypes.InternalServerError.Title,
            Type = ProblemTypes.InternalServerError.Type,
            Instance = httpContext.Request.Path
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    internal static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var withoutNewlines = value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        return string.Concat(withoutNewlines.Where(c => !char.IsControl(c)));
    }
}
