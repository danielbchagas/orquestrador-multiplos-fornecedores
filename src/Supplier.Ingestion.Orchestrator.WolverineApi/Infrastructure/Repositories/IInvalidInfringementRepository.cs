using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;

public interface IInvalidInfringementRepository
{
    Task SaveAsync(InvalidInfringementDocument document, CancellationToken ct = default);
    Task<IReadOnlyList<InvalidInfringementDocument>> GetAllAsync(int limit = 50, CancellationToken ct = default);
    Task<InvalidInfringementDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task IncrementRetryAsync(Guid id, CancellationToken ct = default);
}
