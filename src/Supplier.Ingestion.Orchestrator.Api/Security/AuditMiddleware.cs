namespace Supplier.Ingestion.Orchestrator.Api.Security;

public class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Request.Path.StartsWithSegments("/dlq"))
        {
            logger.LogInformation(
                "AUDIT: {Method} {Path} — Status: {Status} — IP: {IP} — User: {User}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                context.Connection.RemoteIpAddress,
                context.User.Identity?.Name ?? "anonymous");
        }
    }
}
