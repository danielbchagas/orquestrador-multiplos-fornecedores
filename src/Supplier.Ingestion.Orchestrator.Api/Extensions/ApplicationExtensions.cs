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

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
