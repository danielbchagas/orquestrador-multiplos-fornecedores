using Confluent.Kafka;
using Microsoft.AspNetCore.RateLimiting;
using Supplier.B.Producer.Api.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var kafkaBootstrapServers = builder.Configuration.GetConnectionString("Kafka") ?? "localhost:9092";

const string Topic = "source.supplier-b.v1";
const string OriginSystem = "Fornecedor_B";

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    }).Build());

builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", cfg =>
    {
        cfg.PermitLimit = 60;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("strict", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueLimit = 0;
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit excedido.", ct);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=()");

    var path = context.Request.Path;
    if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/openapi"))
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Supplier B Producer", topic = Topic }))
    .WithName("Health")
    .WithSummary("Health check");

app.MapPost("/infringements", async (InfringementRequest request, IProducer<string, string> producer) =>
{
    if (string.IsNullOrWhiteSpace(request.ExternalCode))
        return Results.BadRequest(new { error = "ExternalCode é obrigatório." });

    if (string.IsNullOrWhiteSpace(request.Plate))
        return Results.BadRequest(new { error = "Plate é obrigatório." });

    if (request.Infringement < 500 || request.Infringement > 999)
        return Results.BadRequest(new { error = "Infringement deve ser um código CTB entre 500 e 999." });

    if (request.TotalValue <= 0)
        return Results.BadRequest(new { error = "TotalValue deve ser maior que zero." });

    var correlationId = GenerateCorrelationId(request.ExternalCode);
    var envelope = BuildEnvelope(correlationId, request.ExternalCode, request.Plate, request.Infringement, request.TotalValue);
    var json = JsonSerializer.Serialize(envelope, JsonOptions.Default);
    var signingKey = builder.Configuration["Kafka:SigningKey"] ?? string.Empty;

    await producer.ProduceAsync(Topic, new Message<string, string>
    {
        Key = correlationId.ToString(),
        Value = json,
        Headers = new Headers { { "x-signature", Encoding.UTF8.GetBytes(MessageSigner.Sign(json, signingKey)) } }
    });

    return Results.Accepted($"/infringements", new { correlationId, status = "published", topic = Topic });
})
.WithName("PublishInfringement")
.WithSummary("Publica uma infração do Fornecedor B")
.WithDescription("Publica uma mensagem de infração de veículo no tópico Kafka do Fornecedor B para ser processada pelo orquestrador.")
.RequireRateLimiting("default");

app.MapPost("/infringements/simulate", async (IProducer<string, string> producer, int count = 1) =>
{
    if (count < 1 || count > 100)
        return Results.BadRequest(new { error = "count deve ser entre 1 e 100." });

    var published = new List<object>();

    for (var i = 0; i < count; i++)
    {
        var externalCode = $"B-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var plate = GenerateRandomPlate();
        var infringement = Random.Shared.Next(500, 1000);
        var totalValue = Math.Round((decimal)(Random.Shared.NextDouble() * 990 + 10), 2);

        var correlationId = GenerateCorrelationId(externalCode);
        var envelope = BuildEnvelope(correlationId, externalCode, plate, infringement, totalValue);
        var json = JsonSerializer.Serialize(envelope, JsonOptions.Default);
        var signingKey = builder.Configuration["Kafka:SigningKey"] ?? string.Empty;

        await producer.ProduceAsync(Topic, new Message<string, string>
        {
            Key = correlationId.ToString(),
            Value = json,
            Headers = new Headers { { "x-signature", Encoding.UTF8.GetBytes(MessageSigner.Sign(json, signingKey)) } }
        });

        published.Add(new { correlationId, externalCode, plate, infringement, totalValue });
    }

    return Results.Accepted($"/infringements", new { status = "published", topic = Topic, count = published.Count, messages = published });
})
.WithName("SimulateInfringement")
.WithSummary("Simula infrações aleatórias do Fornecedor B")
.WithDescription("Gera e publica mensagens de infração com dados aleatórios válidos. Use o parâmetro `count` para publicar múltiplas mensagens de uma vez (máx. 100).")
.RequireRateLimiting("strict");

app.Run();

static object BuildEnvelope(Guid correlationId, string externalCode, string plate, int infringement, decimal totalValue) => new
{
    correlationId,
    externalCode,
    plate,
    infringement,
    totalValue,
    originSystem = OriginSystem
};

static Guid GenerateCorrelationId(string businessKey)
{
    var dnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    Span<byte> namespaceBytes = stackalloc byte[16];
    dnsNamespace.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

    var nameBytes = Encoding.UTF8.GetBytes(businessKey);
    var buffer = new byte[16 + nameBytes.Length];
    namespaceBytes.CopyTo(buffer);
    nameBytes.CopyTo(buffer, 16);

    var hash = SHA1.HashData(buffer);
    hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
    hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122

    return new Guid(hash.AsSpan()[..16], bigEndian: true);
}

static string GenerateRandomPlate()
{
    var letters = string.Concat(Enumerable.Range(0, 3).Select(_ => (char)('A' + Random.Shared.Next(26))));
    if (Random.Shared.Next(2) == 0)
    {
        // Formato Mercosul: AAA9A99
        var d1 = Random.Shared.Next(10);
        var l = (char)('A' + Random.Shared.Next(26));
        var d2 = Random.Shared.Next(10);
        var d3 = Random.Shared.Next(10);
        return $"{letters}{d1}{l}{d2}{d3}";
    }
    else
    {
        // Formato antigo: AAA-9999
        return $"{letters}-{Random.Shared.Next(10000):D4}";
    }
}

record InfringementRequest(
    string ExternalCode,
    string Plate,
    int Infringement,
    decimal TotalValue);

file static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
