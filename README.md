# Orquestrador de Múltiplos Fornecedores

## Introdução

Este projeto é uma API desenvolvida em .NET responsável por orquestrar a ingestão de dados de múltiplos fornecedores. O sistema visa centralizar e gerenciar o processo de recebimento e processamento de informações, garantindo consistência e confiabilidade.

## Estrutura do Projeto

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

## Como Executar

### Pré-requisitos

Antes de executar o projeto, configure as variáveis de ambiente:

1. Copie o arquivo `.env.example` para `.env`:
   ```bash
   cp .env.example .env
   ```

2. Edite o arquivo `.env` com suas credenciais do MongoDB (para desenvolvimento, os valores padrão já são suficientes).

### Executar com Docker

Para executar o projeto localmente utilizando o Docker:

```bash
docker-compose up -d
```

Ou via .NET CLI na pasta do projeto da API:

```bash
dotnet run --project src/Supplier.Ingestion.Orchestrator.Api
```
