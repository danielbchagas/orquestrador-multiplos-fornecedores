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

---

## 🔗 Links Úteis para Testes

Após executar o projeto com `docker-compose up -d`, todos os serviços ficam disponíveis localmente. Use os links abaixo para explorar e validar cada funcionalidade do sistema.

---

### 🌐 API

| Recurso | URL | Descrição |
|---|---|---|
| Raiz | [http://localhost:8080](http://localhost:8080) | Endpoint raiz da API |
| Scalar Docs | [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1) | Documentação interativa (substitui Swagger UI) |
| OpenAPI JSON | [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json) | Especificação OpenAPI em JSON |
| Health Geral | [http://localhost:8080/health](http://localhost:8080/health) | Status completo da aplicação |
| Health Readiness | [http://localhost:8080/health/ready](http://localhost:8080/health/ready) | Verifica conectividade com Kafka e MongoDB |
| Health Liveness | [http://localhost:8080/health/live](http://localhost:8080/health/live) | Verifica se o processo está respondendo |
| Grafana Probe | [http://localhost:8080/grafana](http://localhost:8080/grafana) | Endpoint de teste de observabilidade |

---

### 📨 Kafka UI

> Cluster: **Local-Cluster**

| Recurso | URL | Descrição |
|---|---|---|
| Painel Principal | [http://localhost:8090](http://localhost:8090) | Visão geral do cluster Kafka |
| Todos os Tópicos | [http://localhost:8090/ui/clusters/Local-Cluster/all-topics](http://localhost:8090/ui/clusters/Local-Cluster/all-topics) | Lista todos os tópicos criados |
| `source.supplier-a.v1` | [http://localhost:8090/ui/clusters/Local-Cluster/topics/source.supplier-a.v1/messages](http://localhost:8090/ui/clusters/Local-Cluster/topics/source.supplier-a.v1/messages) | Mensagens de entrada do Fornecedor A |
| `source.supplier-b.v1` | [http://localhost:8090/ui/clusters/Local-Cluster/topics/source.supplier-b.v1/messages](http://localhost:8090/ui/clusters/Local-Cluster/topics/source.supplier-b.v1/messages) | Mensagens de entrada do Fornecedor B |
| `target.processed.data.v1` | [http://localhost:8090/ui/clusters/Local-Cluster/topics/target.processed.data.v1/messages](http://localhost:8090/ui/clusters/Local-Cluster/topics/target.processed.data.v1/messages) | Eventos processados com sucesso |
| `target.invalid.data.v1` | [http://localhost:8090/ui/clusters/Local-Cluster/topics/target.invalid.data.v1/messages](http://localhost:8090/ui/clusters/Local-Cluster/topics/target.invalid.data.v1/messages) | Eventos com falha de validação |
| Consumer Groups | [http://localhost:8090/ui/clusters/Local-Cluster/consumer-groups](http://localhost:8090/ui/clusters/Local-Cluster/consumer-groups) | Lag e status dos grupos consumidores |

---

### 🗄️ MongoDB — Mongo Express

> Credenciais: `admin` / `password`

| Recurso | URL | Descrição |
|---|---|---|
| Painel Principal | [http://localhost:8181](http://localhost:8181) | Explorador visual do MongoDB |
| Banco `IngestionRefineryDb` | [http://localhost:8181/db/IngestionRefineryDb/](http://localhost:8181/db/IngestionRefineryDb/) | Banco de dados das sagas |
| Coleção `InfringementSagas` | [http://localhost:8181/db/IngestionRefineryDb/InfringementSagas](http://localhost:8181/db/InfringementSagas) | Estado persistido de cada saga |

---

### 📊 Observabilidade — Grafana

> Grafana está configurado com **acesso anônimo** habilitado. Não é necessário login.

#### Logs — Loki

As queries abaixo usam a fonte de dados **Loki** no Grafana Explore.

| Consulta | Link Direto | LogQL |
|---|---|---|
| Todos os logs da aplicação | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22loki%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22%7Bexporter%3D%5C%22OTLP%5C%22%7D%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{exporter="OTLP"}` |
| Apenas erros | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22loki%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22%7Bexporter%3D%5C%22OTLP%5C%22%7D%20%7C%3D%20%5C%22error%5C%22%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{exporter="OTLP"} \|= "error"` |
| Falhas de validação | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22loki%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22%7Bexporter%3D%5C%22OTLP%5C%22%7D%20%7C%3D%20%5C%22Validation%5C%22%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{exporter="OTLP"} \|= "Validation"` |
| Saga — Fornecedor A | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22loki%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22%7Bexporter%3D%5C%22OTLP%5C%22%7D%20%7C%3D%20%5C%22SupplierA%5C%22%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{exporter="OTLP"} \|= "SupplierA"` |
| Saga — Fornecedor B | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22loki%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22%7Bexporter%3D%5C%22OTLP%5C%22%7D%20%7C%3D%20%5C%22SupplierB%5C%22%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{exporter="OTLP"} \|= "SupplierB"` |

#### Traces — Tempo

As queries abaixo usam a fonte de dados **Tempo** no Grafana Explore.

| Consulta | Link Direto | TraceQL |
|---|---|---|
| Todos os traces (última hora) | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22tempo%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22queryType%22%3A%22traceqlSearch%22%2C%22filters%22%3A%5B%5D%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | *(busca visual por atributos)* |
| Traces com erro | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22tempo%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22queryType%22%3A%22traceql%22%2C%22query%22%3A%22%7B%20status%3Derror%20%7D%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `{ status=error }` |

#### Métricas — Prometheus (via Grafana)

As queries abaixo usam a fonte de dados **Prometheus** no Grafana Explore.

| Consulta | Link Direto | PromQL |
|---|---|---|
| Taxa de requisições HTTP | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22prometheus%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22rate(http_server_request_duration_seconds_count%5B5m%5D)%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `rate(http_server_request_duration_seconds_count[5m])` |
| Latência p95 das requisições | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22prometheus%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22histogram_quantile(0.95%2C%20sum(rate(http_server_request_duration_seconds_bucket%5B5m%5D))%20by%20(le))%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))` |
| Uso de memória do processo | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22prometheus%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22process_working_set_bytes%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `process_working_set_bytes` |
| Coletas de GC do .NET | [Explorar →](http://localhost:3000/explore?orgId=1&left=%7B%22datasource%22%3A%22prometheus%22%2C%22queries%22%3A%5B%7B%22refId%22%3A%22A%22%2C%22expr%22%3A%22rate(process_runtime_dotnet_gc_collections_count_total%5B5m%5D)%22%7D%5D%2C%22range%22%3A%7B%22from%22%3A%22now-1h%22%2C%22to%22%3A%22now%22%7D%7D) | `rate(process_runtime_dotnet_gc_collections_count_total[5m])` |

---

### 📈 Prometheus — Acesso Direto

| Recurso | URL | Descrição |
|---|---|---|
| UI de Queries | [http://localhost:9090/graph](http://localhost:9090/graph) | Interface nativa do Prometheus |
| Targets Ativos | [http://localhost:9090/targets](http://localhost:9090/targets) | Status dos scrapers configurados |
| Todas as Métricas | [http://localhost:9090/api/v1/label/__name__/values](http://localhost:9090/api/v1/label/__name__/values) | Lista de métricas disponíveis (JSON) |

---

### 🔭 Loki e Tempo — Acesso Direto

| Serviço | URL | Descrição |
|---|---|---|
| Loki Health | [http://localhost:3100/ready](http://localhost:3100/ready) | Verifica se o Loki está pronto |
| Loki Labels | [http://localhost:3100/loki/api/v1/labels](http://localhost:3100/loki/api/v1/labels) | Labels indexados no Loki |
| Tempo Health | [http://localhost:3200/ready](http://localhost:3200/ready) | Verifica se o Tempo está pronto |
| Tempo Métricas | [http://localhost:3200/metrics](http://localhost:3200/metrics) | Métricas internas do Tempo |