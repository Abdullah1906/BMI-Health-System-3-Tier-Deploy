using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using ScalingDemo.Api.Data;

namespace ScalingDemo.Api.Infrastructure;

/// <summary>Turns "database not reachable / not ready yet" into a clean 503 instead of a 500 stack trace.</summary>
public sealed class DatabaseExceptionHandler(
    ILogger<DatabaseExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not (DatabaseNotConfiguredException or SqlException))
        {
            return false;
        }

        logger.LogError(exception, "Database request failed.");
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Database unavailable",
                Detail = "The database is not reachable or is still starting. Please try again shortly."
            }
        });
    }
}
