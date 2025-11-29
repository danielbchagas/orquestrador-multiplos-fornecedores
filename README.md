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

## Tecnologias Utilizadas
O projeto utiliza as seguintes tecnologias e bibliotecas principais:
- **.NET 10**: Plataforma de desenvolvimento utilizada para construir a API.
- **MassTransit**: Biblioteca para comunicação assíncrona via mensagens.
- **MongoDB**: Banco de dados NoSQL utilizado para armazenamento de dados.

## Bibliotecas de Teste

Para garantir a qualidade e o correto funcionamento do código, o projeto utiliza as seguintes bibliotecas de teste:

- **xUnit**: Framework de teste para execução de testes unitários.
- **Moq**: Biblioteca para criação de objetos de simulação (mocks).
- **AutoFixture**: Ferramenta para geração de dados de teste anônimos.
- **MassTransit.TestHarness**: Utilitário para testar sagas e consumidores do MassTransit em memória.

## Como Executar

Para executar o projeto localmente utilizando o Docker:

```bash
docker-compose up -d
```

Ou via .NET CLI na pasta do projeto da API:

```bash
dotnet run --project src/Supplier.Ingestion.Orchestrator.Api
```
