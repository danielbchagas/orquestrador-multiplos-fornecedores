# Orquestrador de Múltiplos Fornecedores

## 📋 Introdução

Este projeto é uma API desenvolvida em .NET responsável por orquestrar a ingestão de dados de múltiplos fornecedores. O sistema consome eventos de infrações a partir de tópicos Kafka específicos por fornecedor, valida os dados recebidos através de State Machines (Saga Pattern via MassTransit), e publica o resultado em tópicos de saída — separando eventos válidos de inválidos. O estado das sagas é persistido no MongoDB.

---

## 📐 Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) e Docker Compose

---

## 🗂️ Estrutura do Projeto

```
├── src/
│   └── Supplier.Ingestion.Orchestrator.Api/   # API principal (ASP.NET Core)
│       ├── Extensions/                         # Configuração de serviços (MassTransit, Kafka)
│       ├── Infrastructure/
│       │   ├── Events/                         # Eventos de integração (entrada, saída, falha)
│       │   └── StateMachines/                  # State Machines das sagas por fornecedor
│       └── Validators/                         # Regras de validação de infrações
├── tests/
│   └── Supplier.Ingestion.Orchestrator.Tests/  # Testes automatizados
│       ├── UnitTests/                          # Testes unitários das state machines
│       ├── IntegrationTests/                   # Testes de integração com Testcontainers
│       └── LoadTests/                          # Testes de carga com NBomber
├── files/                                      # Configs de infra (Grafana, Prometheus, OTel, etc.)
├── docker-compose.yml                          # Orquestração da API
└── docker-compose.override.yml                 # Overrides para ambiente local
```

---

## 🛠️ Tecnologias Utilizadas

| Tecnologia | Finalidade |
|---|---|
| **.NET 10** | Plataforma principal da API |
| **MassTransit** | Orquestração de sagas (Saga Pattern) |
| **Apache Kafka** | Broker de mensageria (entrada e saída de eventos) |
| **MongoDB** | Persistência do estado das sagas |
| **OpenTelemetry** | Coleta de métricas, traces e logs |
| **Grafana / Loki / Tempo / Prometheus** | Observabilidade (dashboards, logs, traces, métricas) |
| **Scalar** | Documentação interativa da API (substitui Swagger UI) |
| **Docker Compose** | Orquestração do ambiente local |

---

## 🔀 Fluxo de Dados

```
Kafka (source.supplier-a.v1) ──┐
                                ├──▶ MassTransit Saga ──▶ Validação ──┬──▶ Kafka (target.processed.data.v1)
Kafka (source.supplier-b.v1) ──┘                                     └──▶ Kafka (target.invalid.data.v1)
```

### Tópicos Kafka

| Tópico | Direção | Descrição |
|---|---|---|
| `source.supplier-a.v1` | Entrada | Eventos do Fornecedor A |
| `source.supplier-b.v1` | Entrada | Eventos do Fornecedor B |
| `target.processed.data.v1` | Saída | Eventos validados com sucesso |
| `target.invalid.data.v1` | Saída | Eventos com falha de validação |

---

## 🧪 Bibliotecas de Teste

- **xUnit**: Framework de testes
- **AutoFixture / AutoFixture.AutoMoq**: Geração de dados de teste e mocks automáticos
- **FluentAssertions**: Asserções legíveis e expressivas
- **Testcontainers.Kafka**: Testes de integração com Kafka real via container
- **NBomber**: Testes de carga e performance

---

## ▶️ Como Executar

### Via Docker (recomendado)

Sobe toda a infraestrutura (Kafka, MongoDB, Grafana, Prometheus, etc.) junto com a API:

```bash
docker-compose up -d
```

### Via .NET CLI

> ⚠️ Requer que os serviços de infraestrutura (Kafka, MongoDB, OTel Collector) já estejam em execução.

```bash
docker-compose -f files/docker-compose.yml up -d
dotnet run --project src/Supplier.Ingestion.Orchestrator.Api
```

### Executar Testes

```bash
dotnet test
```

---

## 🌐 Portas dos Serviços

| Serviço | URL |
|---|---|
| API | http://localhost:8080 |
| Scalar (API Docs) | http://localhost:8080/scalar/v1 |
| Kafka UI | http://localhost:8090 |
| Mongo Express | http://localhost:8181 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |

---

## 🕹️ Exemplos de Eventos

### Fornecedor A

**Evento válido**
```json
{
  "ExternalCode": "TESTE-FIXO-HASH",
  "Plate": "ABC-1234",
  "Infringement": 7455,
  "TotalValue": 100.00,
  "OriginSystem": "Fornecedor_A"
}
```
Destino: `target.processed.data.v1`

**Evento inválido**
```json
{
  "ExternalCode": "TESTE-FIXO-HASH",
  "Plate": "ABC-1234",
  "Infringement": 7455,
  "TotalValue": -100.00,
  "OriginSystem": "Fornecedor_A"
}
```
Destino: `target.invalid.data.v1`

---

### Fornecedor B

**Evento válido**
```json
{
  "ExternalCode": "PEDIDO-B-FINAL-900",
  "Plate": "BBB-8888",
  "Infringement": 6050,
  "TotalValue": 355.50,
  "OriginSystem": "Fornecedor_B"
}
```
Destino: `target.processed.data.v1`

**Evento inválido**
```json
{
  "ExternalCode": "PEDIDO-B-FINAL-900",
  "Plate": "BBB-8888",
  "Infringement": 6050,
  "TotalValue": -355.50,
  "OriginSystem": "Fornecedor_B"
}
```
Destino: `target.invalid.data.v1`