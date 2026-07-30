using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace RiskRegister.Api.ErrorHandling;

/// <summary>
/// Turns a database outage into a 503, rather than letting it surface as a generic 500.
/// </summary>
/// <remarks>
/// A stopped SQL Server container is by far the most likely failure in this app. 503 tells the
/// caller "retry later"; 500 tells them "this is broken", and only one of those is true. Anything
/// that is not a database failure is left alone and falls through to the default 500 handler.
/// </remarks>
public class DatabaseExceptionHandler(ILogger<DatabaseExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<DatabaseExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not (SqlException or DbUpdateException or TimeoutException))
        {
            return false;
        }

        // Logged in full here; the response deliberately carries none of it. Connection strings and
        // server names show up in SqlException messages.
        _logger.LogError(
            exception,
            "Database unavailable handling {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The register is temporarily unavailable.",
                Detail = "The database could not be reached. Please try again shortly.",
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
