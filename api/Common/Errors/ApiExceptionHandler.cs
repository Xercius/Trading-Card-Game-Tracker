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
        var sanitizedPath = (httpContext.Request.Path.Value ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        logger.LogError(UnhandledExceptionEventId, exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, sanitizedPath);

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
}
