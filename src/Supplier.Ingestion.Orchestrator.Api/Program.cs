using Scalar.AspNetCore;
using Supplier.Ingestion.Orchestrator.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMassTransitExtensions(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Scalar Example API")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapGet("/", () => "Supplier Ingestion Orchestrator API is running...");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
