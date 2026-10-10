using ScalingDemo.Api.Data;

namespace ScalingDemo.Api.Infrastructure;

/// <summary>Endpoints used by the scaling lab itself: probes, "who answered", and a CPU burner for HPA.</summary>
public static class LabEndpoints
{
    public static IEndpointRouteBuilder MapLabEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => new
        {
            Application = "ScalingDemo API",
            Status = "Running"
        });

        // Liveness: the process is up. Deliberately does not touch the database.
        app.MapGet("/health", () => Results.Ok(new
        {
            Status = "Healthy"
        }));

        // Readiness: the database is reachable and the schema exists.
        app.MapGet("/health/ready", ReadyAsync);

        app.MapGet("/instance", () => new
        {
            Hostname = Environment.MachineName,
            Time = DateTimeOffset.UtcNow
        });

        // /health is far too light to move CPU usage, so HPA demos hit this instead.
        app.MapGet("/cpu", BurnCpu);

        return app;
    }

    private static async Task<IResult> ReadyAsync(IBmiRepository repository, CancellationToken ct)
    {
        try
        {
            return await repository.IsReadyAsync(ct)
                ? Results.Ok(new { Status = "Ready" })
                : NotReady();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return NotReady();
        }
    }

    private static IResult NotReady() =>
        Results.Json(new { Status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    private static object BurnCpu(int? ms)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Clamp(ms ?? 200, 1, 2000));
        var end = DateTime.UtcNow + duration;

        double sum = 0;
        while (DateTime.UtcNow < end)
        {
            sum += Math.Sqrt(Random.Shared.NextDouble());
        }

        return new { Hostname = Environment.MachineName, BusyMs = duration.TotalMilliseconds, Result = sum };
    }
}
