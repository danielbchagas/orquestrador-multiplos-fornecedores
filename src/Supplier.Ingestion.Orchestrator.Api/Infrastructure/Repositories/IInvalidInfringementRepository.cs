using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Persistence;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Repositories;

public interface IInvalidInfringementRepository
{
    Task SaveAsync(InvalidInfringementDocument document, CancellationToken ct = default);
    Task<IReadOnlyList<InvalidInfringementDocument>> GetAllAsync(int limit = 50, CancellationToken ct = default);
    Task<InvalidInfringementDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task IncrementRetryAsync(Guid id, CancellationToken ct = default);
    Task DeleteSagaAsync(Guid correlationId, CancellationToken ct = default);
}
