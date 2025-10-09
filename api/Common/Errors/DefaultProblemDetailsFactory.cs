using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace api.Common.Errors;

public sealed class DefaultProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly ApiBehaviorOptions _apiBehavior;

    public DefaultProblemDetailsFactory(IOptions<ApiBehaviorOptions> apiBehavior)
    {
        _apiBehavior = apiBehavior?.Value ?? throw new ArgumentNullException(nameof(apiBehavior));
    }

    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

        statusCode ??= StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

        ApplyDefaults(httpContext, problemDetails, statusCode.Value);
        return problemDetails;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));
        if (modelStateDictionary is null) throw new ArgumentNullException(nameof(modelStateDictionary));

        statusCode ??= StatusCodes.Status400BadRequest;

        var problemDetails = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode,
            Type = type,
            Detail = detail,
            Instance = instance,
            Title = title
        };

        ApplyDefaults(httpContext, problemDetails, statusCode.Value);
        return problemDetails;
    }

    private void ApplyDefaults(HttpContext httpContext, ProblemDetails problemDetails, int statusCode)
    {
        if (_apiBehavior.ClientErrorMapping.TryGetValue(statusCode, out var clientErrorData))
        {
            problemDetails.Title ??= clientErrorData.Title;
            problemDetails.Type  ??= clientErrorData.Link;
        }

        if (ProblemTypes.TryGet(statusCode, out var problemType))
        {
            problemType.Apply(httpContext, problemDetails);
        }
        else if (problemDetails.Instance is null)
        {
            problemDetails.Instance = httpContext.Request.Path;
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
    }
}
