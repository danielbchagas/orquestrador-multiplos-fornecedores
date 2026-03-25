using Scalar.AspNetCore;

namespace Supplier.Ingestion.Orchestrator.Api.Extensions;

public static class ApplicationExtensions
{
    public static WebApplication UseApplicationExtensions(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Supplier Ingestion Orchestrator API")
                    .WithTheme(ScalarTheme.Mars)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");
            context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=()");

            // CSP restritivo apenas para endpoints de API — Scalar/OpenAPI precisam carregar scripts e estilos
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/openapi"))
                context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

            await next();
        });

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
