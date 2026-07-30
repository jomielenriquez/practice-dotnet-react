using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RiskRegister.Tests.Fakes;

/// <summary>
/// Stands in for the <see cref="ProblemDetailsFactory"/> MVC injects at runtime.
/// </summary>
/// <remarks>
/// ASP.NET Core's own implementation is internal, and <c>ControllerBase.ValidationProblem</c>
/// dereferences the factory, so a controller constructed with a bare <c>new</c> needs one supplied.
/// This produces the same shape without the framework's `type`/`traceId` decoration, which the
/// tests do not assert on.
/// </remarks>
public class TestProblemDetailsFactory : ProblemDetailsFactory
{
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null) => new()
        {
            Status = statusCode ?? StatusCodes.Status500InternalServerError,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null) => new(modelStateDictionary)
        {
            Status = statusCode ?? StatusCodes.Status400BadRequest,
            Title = title ?? "One or more validation errors occurred.",
            Type = type,
            Detail = detail,
            Instance = instance
        };
}
