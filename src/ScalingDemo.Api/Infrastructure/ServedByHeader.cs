namespace ScalingDemo.Api.Infrastructure;

public static class ServedByHeader
{
    /// <summary>Adds X-Served-By so the UI (and curl -i) can show which replica answered.</summary>
    public static Task Add(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Served-By"] = Environment.MachineName;
            return Task.CompletedTask;
        });

        return next(context);
    }
}
