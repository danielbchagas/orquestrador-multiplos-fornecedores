# Orquestrador de Múltiplos Fornecedores

## 📋 Introdução

Este projeto é uma API desenvolvida em .NET responsável por orquestrar a ingestão de dados de múltiplos fornecedores. O sistema centraliza e gerencia o processo de recebimento e processamento de informações, garantindo consistência e confiabilidade.

---

## 🗂️ Estrutura do Projeto

A estrutura de diretórios do projeto está organizada da seguinte forma:

- **src/**: Contém o código fonte da aplicação.
  - **Supplier.Ingestion.Orchestrator.Api**: Projeto principal da API (ASP.NET Core).
    - `Controllers/`: Endpoints da API.
    - `Domain/`: Entidades e regras de negócio.
    - `Infrastructure/`: Implementação de acesso a dados e serviços externos.
    - `Shared/`: Recursos compartilhados.
- **tests/**: Contém os testes automatizados do projeto.
- **docs/**: Documentação complementar e diagramas de arquitetura.
- **docker-compose.yml**: Arquivo para orquestração de containers Docker, facilitando a execução do ambiente local.

---

## 🛠️ Tecnologias Utilizadas

- **.NET 10**: Plataforma principal da API
- **MassTransit**: Comunicação assíncrona via mensagens
- **MongoDB**: Banco de dados NoSQL

---

## 🧪 Bibliotecas de Teste

- **xUnit**: Execução de testes unitários
- **Moq**: Criação de objetos simulados (mocks)
- **AutoFixture**: Geração de dados de teste anônimos
- **MassTransit.TestHarness**: Testes de sagas e consumidores MassTransit em memória

---

## ▶️ Como Executar

**Via Docker:**
```bash
docker-compose up -d
```

**Via .NET CLI:**
```bash
dotnet run --project src/Supplier.Ingestion.Orchestrator.Api
```

---

## 🕹️ Exemplos de Eventos

### Fornecedor A

**Evento válido**
```
{
  "ExternalId": "TESTE-FIXO-HASH",
  "Plate": "ABC-1234",
  "Infringement": 7455,
  "TotalValue": 100.00
}
```
Destino: `target.dados.processados.v1`

**Evento inválido**
```
{
  "ExternalId": "TESTE-FIXO-HASH",
  "Plate": "ABC-1234",
  "Infringement": 7455,
  "TotalValue": -100.00
}
```
Destino: `target.dados.invalidos.v1`

---

### Fornecedor B

**Evento válido**
```
{
  "ExternalCode": "PEDIDO-B-FINAL-900",
  "Plate": "BBB-8888",
  "Infringement": 6050,
  "TotalValue": 355.50,
  "OriginSystem": "LEGADO_B"
}
```
Destino: `target.dados.processados.v1`

**Evento inválido**
```
{
  "ExternalCode": "PEDIDO-B-FINAL-900",
  "Plate": "BBB-8888",
  "Infringement": 6050,
  "TotalValue": -355.50,
  "OriginSystem": "LEGADO_B"
}
```
Destino: `target.dados.invalidos.v1`