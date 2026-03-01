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
│       ├── FunctionalTests/                    # Testes funcionais BDD (Reqnroll/Gherkin)
│       │   ├── Features/                       # Cenários em linguagem Gherkin (.feature)
│       │   └── StepDefinitions/               # Implementação dos passos (Given/When/Then)
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
- **Reqnroll**: BDD (Behaviour-Driven Development) com sintaxe Gherkin (Given/When/Then)
- **AutoFixture / AutoFixture.AutoMoq**: Geração de dados de teste e mocks automáticos
- **Moq**: Mocking de dependências nos testes unitários e funcionais
- **FluentAssertions**: Asserções legíveis e expressivas
- **Testcontainers.Kafka**: Testes de integração com Kafka real via container
- **NBomber**: Testes de carga e performance

---

## 🧬 Testes Funcionais (BDD)

Os testes funcionais utilizam **Reqnroll** (sucessor do SpecFlow para .NET) com cenários escritos em **Gherkin** (Given/When/Then). Eles validam o comportamento end-to-end das state machines sem depender de infraestrutura externa — as dependências de Kafka são substituídas por mocks via **Moq** e o barramento pelo **MassTransit Test Harness**.

### Cenários Cobertos

#### `SupplierAStateMachine.feature` — State Machine do Fornecedor A

| Cenário | Entrada | Resultado esperado |
|---|---|---|
| Infração válida processada com sucesso | Placa `ABC1234`, valor `R$ 150,00` | Saga finalizada + evento `UnifiedInfringementProcessed` produzido |
| Valor negativo rejeitado | Placa `ABC1234`, valor `-R$ 10,00` | Saga finalizada + evento `InfringementValidationFailed` produzido |
| Placa vazia rejeitada | Placa `""`, valor `R$ 100,00` | Saga finalizada + evento `InfringementValidationFailed` produzido |

#### `SupplierBStateMachine.feature` — State Machine do Fornecedor B

| Cenário | Entrada | Resultado esperado |
|---|---|---|
| Infração válida processada com sucesso | Placa `XYZ9876`, valor `R$ 200,00` | Saga finalizada + evento `UnifiedInfringementProcessed` produzido |
| Valor negativo rejeitado | Placa `XYZ9876`, valor `-R$ 5,00` | Saga finalizada + evento `InfringementValidationFailed` produzido |
| Placa vazia rejeitada | Placa `""`, valor `R$ 50,00` | Saga finalizada + evento `InfringementValidationFailed` produzido |

#### `InfringementValidation.feature` — Validação de Infrações

| Cenário | Condição | Resultado esperado |
|---|---|---|
| Todos os campos válidos | Placa, valor e ID preenchidos corretamente | Resultado válido, sem erros |
| Placa vazia | Placa `""` | Inválido — `"Placa obrigatória"` |
| Valor negativo | Valor `-50,00` | Inválido — `"Valor inválido"` |
| ID de origem vazio | ExternalId `""` | Inválido — `"ID de origem não informado"` |
| Múltiplos erros simultâneos | Placa, valor e ID inválidos ao mesmo tempo | Inválido — todos os erros acima retornados |

### Arquitetura dos Testes Funcionais

```
FunctionalTests/
├── Features/
│   ├── InfringementValidation.feature   # Validação de regras de negócio
│   ├── SupplierAStateMachine.feature    # Comportamento da saga do Fornecedor A
│   └── SupplierBStateMachine.feature    # Comportamento da saga do Fornecedor B
└── StepDefinitions/
    ├── SupplierStateMachineStepDefinitionsBase.cs  # Passos reutilizáveis (When/Then)
    ├── SupplierAStateMachineStepDefinitions.cs     # Passos Given do Fornecedor A
    └── SupplierBStateMachineStepDefinitions.cs     # Passos Given do Fornecedor B
```

Os produtores Kafka (`ITopicProducer<string, UnifiedInfringementProcessed>` e `ITopicProducer<string, InfringementValidationFailed>`) são substituídos por mocks Moq, permitindo verificar quais eventos foram produzidos sem iniciar um broker real.

### Executar apenas os Testes Funcionais

```bash
dotnet test --filter "Category=Functional"
```

Ou pelo nome do namespace:

```bash
dotnet test --filter "FullyQualifiedName~FunctionalTests"
```

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