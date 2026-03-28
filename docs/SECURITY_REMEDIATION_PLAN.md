# Plano de Remediação de Segurança — OWASP Top 10

**Branch:** `security/owasp-remediation`
**Data:** 2026-03-24
**Baseado em:** Análise OWASP Top 10 (2021)

---

## Visão Geral

Este documento descreve o plano de implementação para corrigir as vulnerabilidades identificadas na análise de segurança. As correções estão organizadas em três fases de acordo com severidade e impacto.

---

## Fase 1 — Crítico (implementar imediatamente)

### F1.1 — Autenticação por API Key em todos os endpoints HTTP

> **Fora de escopo neste POC — decisão intencional.** Em produção, todos os endpoints devem ser protegidos. A vulnerabilidade foi identificada e o plano de remediação está documentado abaixo para referência.

**Vulnerabilidade:** A01 (Broken Access Control) + A07 (Authentication Failures)
**Arquivos afetados:**
- `src/Supplier.Ingestion.Orchestrator.Api/Program.cs`
- `src/Supplier.Ingestion.Orchestrator.Api/Extensions/ApplicationExtensions.cs`
- `src/Supplier.A.Producer.Api/Program.cs`
- `src/Supplier.B.Producer.Api/Program.cs`
- `deploy/docker-compose.yml`
- `.env.example`

**Implementação:**

1. Criar `ApiKeyAuthenticationHandler` — middleware que lê o header `X-Api-Key` e valida contra um conjunto de chaves configuradas.

   ```csharp
   // src/Supplier.Ingestion.Orchestrator.Api/Security/ApiKeyAuthenticationHandler.cs
   public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
   {
       private const string ApiKeyHeaderName = "X-Api-Key";

       protected override Task<AuthenticateResult> HandleAuthenticateAsync()
       {
           if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
               return Task.FromResult(AuthenticateResult.Fail("Header X-Api-Key ausente."));

           var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
           var validKeys = Context.RequestServices
               .GetRequiredService<IConfiguration>()
               .GetSection("Security:ApiKeys")
               .Get<string[]>() ?? [];

           if (!validKeys.Contains(providedApiKey))
               return Task.FromResult(AuthenticateResult.Fail("API Key inválida."));

           var claims = new[] { new Claim(ClaimTypes.Name, "ApiClient") };
           var identity = new ClaimsIdentity(claims, Scheme.Name);
           var principal = new ClaimsPrincipal(identity);
           var ticket = new AuthenticationTicket(principal, Scheme.Name);
           return Task.FromResult(AuthenticateResult.Success(ticket));
       }
   }
   ```

2. Registrar o handler em `Program.cs` (Orchestrator e Producers):

   ```csharp
   builder.Services
       .AddAuthentication("ApiKey")
       .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
   builder.Services.AddAuthorization();
   ```

3. Proteger os endpoints com `[Authorize]` ou `.RequireAuthorization()`:

   ```csharp
   app.MapGet("/dlq", ...).RequireAuthorization();
   app.MapPost("/dlq/{id:guid}/retry", ...).RequireAuthorization();
   app.MapPost("/infringements", ...).RequireAuthorization();
   app.MapPost("/infringements/simulate", ...).RequireAuthorization();
   ```

4. Adicionar `UseAuthentication()` antes de `UseAuthorization()` em `ApplicationExtensions.cs`:

   ```csharp
   app.UseAuthentication();
   app.UseAuthorization();
   ```

5. Externalizar as chaves — adicionar no `appsettings.json`:
   ```json
   "Security": {
     "ApiKeys": []
   }
   ```

6. Adicionar ao `.env.example` e `docker-compose.yml`:
   ```env
   ORCHESTRATOR_API_KEY=<gerar com: openssl rand -hex 32>
   SUPPLIER_A_API_KEY=<gerar com: openssl rand -hex 32>
   SUPPLIER_B_API_KEY=<gerar com: openssl rand -hex 32>
   ```

7. Excluir endpoints públicos da autenticação: `/health`, `/health/ready`, `/health/live`, `/scalar`, `/openapi`.

---

### F1.2 — Remover Scalar UI automático dos Producer APIs
**Vulnerabilidade:** A01 (Broken Access Control), A05 (Security Misconfiguration)
**Arquivos afetados:**
- `src/Supplier.A.Producer.Api/Program.cs` (linhas 27-28)
- `src/Supplier.B.Producer.Api/Program.cs` (linhas similares)

**Implementação:**

Envolver o mapeamento da documentação em verificação de ambiente:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```

---

### F1.3 — Restringir bind de portas de infraestrutura no Docker Compose
**Vulnerabilidade:** A05 (Security Misconfiguration)
**Arquivo afetado:** `deploy/docker-compose.yml`

**Implementação:**

Alterar todos os mapeamentos de porta dos serviços de infraestrutura para bind exclusivo em `127.0.0.1`:

```yaml
# Antes:
ports:
  - "27017:27017"

# Depois (MongoDB, Kafka, Mongo Express, Kafka UI, Prometheus, Loki, Tempo, OTel):
ports:
  - "127.0.0.1:27017:27017"
```

Serviços e portas a atualizar:

| Serviço         | Porta(s)        |
|-----------------|-----------------|
| mongo           | 27017           |
| mongo-express   | 8181            |
| kafka           | 9092            |
| kafka-ui        | 8090            |
| prometheus      | 9090            |
| loki            | 3100            |
| tempo           | 3200            |
| otel-collector  | 4317, 4318      |

As APIs de negócio (`8080`, `8081`, `8082`, `8083`) mantêm bind público, pois precisam ser acessíveis externamente.

---

## Fase 2 — Alto (curto prazo)

### F2.1 — Rate Limiting nos endpoints HTTP
**Vulnerabilidade:** A04 (Insecure Design)
**Arquivos afetados:**
- `src/Supplier.Ingestion.Orchestrator.Api/Program.cs`
- `src/Supplier.A.Producer.Api/Program.cs`
- `src/Supplier.B.Producer.Api/Program.cs`

**Implementação:**

Usar `Microsoft.AspNetCore.RateLimiting` (nativo no .NET 7+):

```csharp
// Program.cs — registrar políticas
builder.Services.AddRateLimiter(options =>
{
    // Política padrão: 60 requests/minuto por IP
    options.AddFixedWindowLimiter("default", cfg =>
    {
        cfg.PermitLimit = 60;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 0;
    });

    // Política restrita: 10 requests/minuto para simulate e retry
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

// Ativar o middleware:
app.UseRateLimiter();
```

Aplicar por endpoint:

```csharp
app.MapPost("/infringements", ...).RequireRateLimiting("default");
app.MapPost("/infringements/simulate", ...).RequireRateLimiting("strict");
app.MapGet("/dlq", ...).RequireRateLimiting("default");
app.MapPost("/dlq/{id:guid}/retry", ...).RequireRateLimiting("strict");
```

---

### F2.2 — Sanitização contra Prompt Injection
**Vulnerabilidade:** A03 (Injection)
**Arquivo afetado:** `src/Supplier.Ingestion.Orchestrator.Api/Validators/AiInfringementValidator.cs`

**Implementação:**

Criar método de sanitização antes da interpolação no prompt:

```csharp
// Sanitiza qualquer string que vá para o prompt da IA
private static string SanitizeForPrompt(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return string.Empty;

    // Remove quebras de linha, tabulações e caracteres de controle
    var cleaned = Regex.Replace(input, @"[\r\n\t]", " ");

    // Remove padrões de injeção mais comuns
    cleaned = Regex.Replace(cleaned, @"(ignore|esquece?a?|desconsidere?|system\s*prompt|</?[a-z]+>)", " ", RegexOptions.IgnoreCase);

    // Limita o tamanho (placa tem max 8 chars, mas evita abuso)
    return cleaned.Length > 50 ? cleaned[..50] : cleaned;
}
```

Aplicar em `BuildPrompt`:

```csharp
public static string BuildPrompt(string plate, int infringementCode, decimal amount, string originSystem) =>
    $$"""
    Analise esta infração de trânsito brasileira:
    - Placa: {{SanitizeForPrompt(plate)}}
    - Código CTB: {{infringementCode}}
    - Valor: R$ {{amount:F2}}
    - Sistema de origem: {{SanitizeForPrompt(originSystem)}}
    ...
    """;
```

> **Nota:** O `infringementCode` é `int` e `amount` é `decimal` — já são type-safe e não precisam de sanitização.

---

### F2.3 — Corrigir race condition no retry da DLQ
**Vulnerabilidade:** A04 (Insecure Design)
**Arquivo afetado:** `src/Supplier.Ingestion.Orchestrator.Api/Program.cs` (linhas 44-65)

**Implementação:**

Inverter a ordem das operações — publicar no Kafka **antes** de deletar do banco:

```csharp
app.MapPost("/dlq/{id:guid}/retry", async (...) =>
{
    var item = await repo.GetByIdAsync(id, ct);
    if (item is null) return Results.NotFound();

    // 1. Incrementar retry count ANTES de qualquer operação destrutiva
    await repo.IncrementRetryAsync(id, ct);

    // 2. Publicar no Kafka
    if (item.OriginSystem == "SupplierA")
        await producerA.Produce(item.OriginId,
            new SupplierAInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);
    else
        await producerB.Produce(item.OriginId,
            new SupplierBInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);

    // 3. Só deletar após publicação bem-sucedida
    await repo.DeleteSagaAsync(item.CorrelationId, ct);

    return Results.Accepted();
})
```

> Para garantia transacional completa, avaliar o uso do padrão Outbox com MassTransit.

---

### F2.4 — Falha segura no fallback da validação IA
**Vulnerabilidade:** A04 (Insecure Design)
**Arquivo afetado:** `src/Supplier.Ingestion.Orchestrator.Api/Validators/AiInfringementValidator.cs` (linha 58)

**Implementação:**

Alterar o comportamento de falha para **rejeitar** ao invés de aceitar:

```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Validação IA falhou. Placa: {Plate}", plate);

    // Fail-closed: rejeita a infração se a IA estiver indisponível
    return new AiValidationResult(
        IsValid: false,
        IsSuspicious: true,
        Analysis: "Validação IA indisponível — infração retida para revisão manual.",
        Confidence: 0.0);
}
```

> **Alternativa menos restritiva:** criar um modo configurável via `appsettings.json`:
> ```json
> "Anthropic": { "FailurePolicy": "FailClosed" }
> ```

---

### F2.5 — Adicionar Security Headers HTTP
**Vulnerabilidade:** A05 (Security Misconfiguration)
**Arquivo afetado:** `src/Supplier.Ingestion.Orchestrator.Api/Extensions/ApplicationExtensions.cs`

**Implementação:**

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=()");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'");
    await next();
});
```

Aplicar também nos Producer APIs.

---

### F2.6 — Mascarar PII (placas) nos logs
**Vulnerabilidade:** A09 (Security Logging Failures)
**Arquivos afetados:**
- `src/Supplier.Ingestion.Orchestrator.Api/Validators/AiInfringementValidator.cs`
- `src/Supplier.Ingestion.Orchestrator.Api/Infrastructure/StateMachines/SupplierStateMachineBase.cs`

**Implementação:**

Criar utilitário de mascaramento:

```csharp
// src/Supplier.Ingestion.Orchestrator.Api/Security/PlateObfuscator.cs
public static class PlateObfuscator
{
    /// Retorna "ABC-***" ou "ABC9***" para logs
    public static string Mask(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate) || plate.Length < 4)
            return "***";
        return plate[..3] + "***";
    }
}
```

Substituir todas as ocorrências de `{Plate}` nos logs:

```csharp
// Antes:
_logger.LogInformation("Chamando IA para validação. Placa: {Plate}", plate);

// Depois:
_logger.LogInformation("Chamando IA para validação. Placa: {Plate}", PlateObfuscator.Mask(plate));
```

---

### F2.7 — Adicionar audit trail de segurança
**Vulnerabilidade:** A09 (Security Logging Failures)
**Arquivo afetado:** `src/Supplier.Ingestion.Orchestrator.Api/Program.cs`

**Implementação:**

Criar middleware de auditoria:

```csharp
// src/Supplier.Ingestion.Orchestrator.Api/Security/AuditMiddleware.cs
public class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Logar todas as chamadas a endpoints sensíveis
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
```

Registrar em `Program.cs`:
```csharp
app.UseMiddleware<AuditMiddleware>();
```

---

## Fase 3 — Médio prazo

### F3.1 — Atualizar MongoDB de 6.0 para 8.0
**Vulnerabilidade:** A06 (Vulnerable and Outdated Components)
**Arquivo afetado:** `deploy/docker-compose.yml`

**Implementação:**

```yaml
# Antes:
image: mongo:6.0

# Depois:
image: mongo:8.0
```

Testar compatibilidade do driver `MongoDB.Driver 3.5.1` com MongoDB 8.0 (compatível conforme release notes).
Validar schema de sagas no ambiente de testes antes de aplicar em produção.

---

### F3.2 — Fixar versões das imagens Docker
**Vulnerabilidade:** A06 (Vulnerable and Outdated Components)
**Arquivo afetado:** `deploy/docker-compose.yml`

**Implementação:**

Substituir todas as tags `latest` por versões fixas e verificadas:

```yaml
# Antes:
provectuslabs/kafka-ui:latest
grafana/grafana:latest
grafana/loki:latest
grafana/tempo:latest
prom/prometheus:latest
otel/opentelemetry-collector-contrib:latest
mongo-express              # sem tag

# Depois (versões mínimas recomendadas — verificar releases antes de aplicar):
provectuslabs/kafka-ui:v0.7.2
grafana/grafana:11.5.2
grafana/loki:3.4.2
grafana/tempo:2.7.2
prom/prometheus:v3.2.1
otel/opentelemetry-collector-contrib:0.123.0
mongo-express:1.0.2
```

Documentar o processo de atualização de versões (quem revisa, com que frequência).

---

### F3.3 — Habilitar TLS no Kafka
**Vulnerabilidade:** A02 (Cryptographic Failures) + A07 (Auth Failures)
**Arquivos afetados:** `deploy/docker-compose.yml`, `appsettings.json`

**Implementação:**

1. Gerar certificados TLS para o broker (ou usar certificados de uma CA interna):
   ```bash
   keytool -keystore kafka.server.keystore.jks -alias localhost -keyalg RSA -validity 365 -genkey
   keytool -keystore kafka.server.truststore.jks -alias CARoot -import -file ca-cert
   ```

2. Atualizar `docker-compose.yml` para configurar SASL_SSL:
   ```yaml
   KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,SASL_SSL:SASL_SSL,SASL_SSL_HOST:SASL_SSL
   KAFKA_LISTENERS: SASL_SSL://kafka:29092,CONTROLLER://kafka:29093,SASL_SSL_HOST://0.0.0.0:9092
   KAFKA_ADVERTISED_LISTENERS: SASL_SSL://kafka:29092,SASL_SSL_HOST://localhost:9092
   KAFKA_SSL_KEYSTORE_FILENAME: kafka.server.keystore.jks
   KAFKA_SSL_KEYSTORE_CREDENTIALS: keystore_creds
   KAFKA_SSL_KEY_CREDENTIALS: sslkey_creds
   KAFKA_SSL_TRUSTSTORE_FILENAME: kafka.server.truststore.jks
   KAFKA_SSL_TRUSTSTORE_CREDENTIALS: truststore_creds
   KAFKA_SASL_ENABLED_MECHANISMS: PLAIN
   KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL: PLAIN
   ```

3. Atualizar connection string no `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "Kafka": "kafka:9092",
     "KafkaSasl": "PLAIN",
     "KafkaUsername": "orchestrator",
     "KafkaPassword": ""
   }
   ```

4. Atualizar `MassTransitExtensions.cs` para incluir credenciais SASL no `KafkaFactoryConfigurator`.

---

### F3.4 — Criar usuário MongoDB com least privilege
**Vulnerabilidade:** A07 (Auth Failures)
**Arquivo afetado:** `deploy/docker-compose.yml`

**Implementação:**

1. Criar script de inicialização MongoDB:
   ```javascript
   // deploy/files/mongo-init.js
   db = db.getSiblingDB('IngestionRefineryDb');
   db.createUser({
     user: process.env.MONGO_APP_USERNAME,
     pwd: process.env.MONGO_APP_PASSWORD,
     roles: [{ role: 'readWrite', db: 'IngestionRefineryDb' }]
   });
   ```

2. Montar o script no container:
   ```yaml
   mongo:
     volumes:
       - ./files/mongo-init.js:/docker-entrypoint-initdb.d/mongo-init.js:ro
   ```

3. Atualizar a connection string para usar o usuário de aplicação:
   ```yaml
   - ConnectionStrings__MongoDb=mongodb://${MONGO_APP_USERNAME}:${MONGO_APP_PASSWORD}@mongo:27017/IngestionRefineryDb
   ```

4. Adicionar ao `.env.example`:
   ```env
   MONGO_APP_USERNAME=orchestrator_app
   MONGO_APP_PASSWORD=<gerar com: openssl rand -hex 24>
   ```

---

### F3.5 — Verificação de integridade das mensagens Kafka (HMAC)
**Vulnerabilidade:** A08 (Data Integrity Failures)
**Arquivos afetados:**
- `src/Supplier.A.Producer.Api/Program.cs`
- `src/Supplier.B.Producer.Api/Program.cs`
- `src/Supplier.Ingestion.Orchestrator.Api/Infrastructure/StateMachines/SupplierStateMachineBase.cs`

**Implementação:**

1. Criar utilitário HMAC:
   ```csharp
   // Compartilhado entre produtores e orquestrador
   public static class MessageSigner
   {
       public static string Sign(string payload, string secret)
       {
           var keyBytes = Encoding.UTF8.GetBytes(secret);
           var payloadBytes = Encoding.UTF8.GetBytes(payload);
           using var hmac = new HMACSHA256(keyBytes);
           return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
       }

       public static bool Verify(string payload, string signature, string secret)
           => Sign(payload, secret) == signature;
   }
   ```

2. Produtores: adicionar header Kafka com assinatura:
   ```csharp
   new Message<string, string>
   {
       Key = correlationId.ToString(),
       Value = json,
       Headers = new Headers
       {
           { "x-signature", Encoding.UTF8.GetBytes(MessageSigner.Sign(json, signingKey)) }
       }
   }
   ```

3. Orquestrador: verificar assinatura no consumer antes de processar a saga.

4. Adicionar `KAFKA_SIGNING_KEY` ao `.env.example`.

---

## Checklist de Validação

Antes de considerar cada fase concluída, verificar:

### Fase 1
- [ ] ~~Todos os endpoints retornam `401` sem header `X-Api-Key`~~ *(fora de escopo — POC)*
- [ ] ~~Endpoints `/health*`, `/scalar`, `/openapi` continuam acessíveis sem auth~~ *(fora de escopo — POC)*
- [ ] Scalar UI dos Producers não aparece em `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `docker-compose up` não expõe MongoDB/Kafka/Prometheus/Loki/Tempo no `0.0.0.0`

### Fase 2
- [ ] `POST /infringements/simulate?count=100` repetido 11x em 1 minuto retorna `429`
- [ ] Placa com caracteres de injeção (`\nIgnore...`) é sanitizada antes de chegar à IA
- [ ] Retry da DLQ com Kafka indisponível não deleta o registro do banco
- [ ] Com API Anthropic indisponível, a infração é rejeitada (não aceita)
- [ ] Logs não contêm placa completa — apenas os 3 primeiros caracteres + `***`
- [ ] Resposta HTTP contém `X-Content-Type-Options: nosniff`
- [ ] Todas as chamadas a `/dlq*` geram log de audit

### Fase 3
- [ ] Testes de integração passam com `mongo:8.0`
- [ ] Imagens Docker têm digest SHA256 fixado no repositório
- [ ] Kafka recusa conexão sem certificado TLS
- [ ] Usuário `orchestrator_app` tem acesso apenas à `IngestionRefineryDb`
- [ ] Mensagem sem header `x-signature` é rejeitada pelo consumidor

---

## Referências

- [OWASP Top 10 (2021)](https://owasp.org/www-project-top-ten/)
- [ASP.NET Core — Rate Limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
- [ASP.NET Core — Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
- [Confluent Kafka — Security](https://docs.confluent.io/platform/current/security/general-overview.html)
- [MongoDB — Security Checklist](https://www.mongodb.com/docs/manual/administration/security-checklist/)
- [LGPD — Lei Geral de Proteção de Dados](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm)
