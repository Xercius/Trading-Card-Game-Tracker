using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace api.Common.Errors;

public static class ProblemDetailsFactoryExtensions
{
    private const string ProblemContentType = "application/problem+json";

    public static ObjectResult CreateProblem(
        this ProblemDetailsFactory factory,
        HttpContext httpContext,
        int statusCode,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null)
    {
        var problem = factory.CreateProblemDetails(httpContext, statusCode, title, type, detail, instance);
        return CreateResult(statusCode, problem);
    }

    public static ObjectResult CreateValidationProblem(
        this ProblemDetailsFactory factory,
        HttpContext httpContext,
        IDictionary<string, string[]> errors,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null)
    {
        var modelState = new ModelStateDictionary();

        foreach (var (key, messages) in errors)
        {
            var propertyName = key ?? string.Empty;
            if (messages is { Length: > 0 })
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(propertyName, message);
                }
            }
            else
            {
                modelState.AddModelError(propertyName, string.Empty);
            }
        }

        var problem = factory.CreateValidationProblemDetails(httpContext, modelState, statusCode, title, type, detail, instance);
        return CreateResult(statusCode, problem);
    }

    public static ObjectResult CreateProblem(
        this ControllerBase controller,
        int statusCode,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        return factory.CreateProblem(controller.HttpContext, statusCode, title, detail, type, instance);
    }

    public static ObjectResult CreateValidationProblem(
        this ControllerBase controller,
        IDictionary<string, string[]> errors,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        return factory.CreateValidationProblem(controller.HttpContext, errors, statusCode, title, detail, type, instance);
    }

    public static ObjectResult CreateValidationProblem(
        this ControllerBase controller,
        string field,
        params string[] errors)
    {
        return controller.CreateValidationProblem(new Dictionary<string, string[]> { [field] = errors });
    }

    private static ObjectResult CreateResult(int statusCode, ProblemDetails problem)
    {
        var result = new ObjectResult(problem)
        {
            StatusCode = statusCode
        };

        result.ContentTypes.Clear();
        result.ContentTypes.Add(ProblemContentType);

        return result;
    }
}
