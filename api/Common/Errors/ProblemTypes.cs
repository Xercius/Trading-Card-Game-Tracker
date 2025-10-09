using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace api.Common.Errors;

public static class ProblemTypes
{
    private static readonly IReadOnlyDictionary<int, ProblemType> Types = new Dictionary<int, ProblemType>
    {
        [StatusCodes.Status400BadRequest] = new ProblemType(
            "https://api.tradingcardgame.tracker/errors/bad-request",
            "Bad Request",
            StatusCodes.Status400BadRequest,
            "The request parameters were invalid."),
        [StatusCodes.Status404NotFound] = new ProblemType(
            "https://api.tradingcardgame.tracker/errors/not-found",
            "Not Found",
            StatusCodes.Status404NotFound,
            "The requested resource could not be found."),
        [StatusCodes.Status409Conflict] = new ProblemType(
            "https://api.tradingcardgame.tracker/errors/conflict",
            "Conflict",
            StatusCodes.Status409Conflict,
            "A conflicting resource state was detected."),
        [StatusCodes.Status500InternalServerError] = new ProblemType(
            "https://api.tradingcardgame.tracker/errors/internal-server-error",
            "Internal Server Error",
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred while processing the request.")
    };

    public static ProblemType BadRequest => Types[StatusCodes.Status400BadRequest];
    public static ProblemType NotFound => Types[StatusCodes.Status404NotFound];
    public static ProblemType Conflict => Types[StatusCodes.Status409Conflict];
    public static ProblemType InternalServerError => Types[StatusCodes.Status500InternalServerError];

    public static bool TryGet(
        int statusCode,
        [NotNullWhen(true)] out ProblemType? problemType) =>
        Types.TryGetValue(statusCode, out problemType);
}

public sealed record ProblemType(string Type, string Title, int Status, string DefaultDetail)
{
    public void Apply(HttpContext httpContext, ProblemDetails problemDetails)
    {
        problemDetails.Type ??= Type;
        problemDetails.Title ??= Title;
        problemDetails.Status ??= Status;
        problemDetails.Detail ??= DefaultDetail;

        if (problemDetails.Instance is null)
        {
            problemDetails.Instance = httpContext.Request.Path.ToString();
        }
    }
}