using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Supplier.Ingestion.Orchestrator.Api.Extensions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCustomMassTransit(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Scalar Example API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapPost("/api/suppliera/input-received", async (
    [FromBody] SupplierAInputReceived message,
    IPublishEndpoint publishEndpoint) =>
{
    if (message == null)
    {
        return Results.BadRequest();
    }

    await publishEndpoint.Publish(message);
    return Results.Accepted();
});

app.MapPost("/api/supplierb/input-received", async (
    [FromBody] SupplierBInputReceived message,
    IPublishEndpoint publishEndpoint) =>
{
    if (message == null)
    {
        return Results.BadRequest();
    }

    await publishEndpoint.Publish(message);
    return Results.Accepted();
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
