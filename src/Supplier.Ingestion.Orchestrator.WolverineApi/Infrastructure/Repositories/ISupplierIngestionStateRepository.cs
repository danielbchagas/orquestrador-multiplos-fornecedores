using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;

public interface ISupplierIngestionStateRepository
{
    /// <summary>
    /// Insere o estado inicial usando o CorrelationId como _id.
    /// Retorna false quando já existe um documento com o mesmo id (mensagem duplicada).
    /// </summary>
    Task<bool> TryBeginProcessingAsync(SupplierIngestionState state, CancellationToken ct = default);

    Task SaveAsync(SupplierIngestionState state, CancellationToken ct = default);
    Task DeleteAsync(Guid correlationId, CancellationToken ct = default);
}
