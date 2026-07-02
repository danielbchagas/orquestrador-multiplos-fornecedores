using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;

public class SupplierIngestionStateRepository : ISupplierIngestionStateRepository
{
    private readonly IMongoCollection<SupplierIngestionState> _collection;

    public SupplierIngestionStateRepository(IMongoClient mongoClient, IConfiguration configuration)
    {
        var db = mongoClient.GetDatabase(configuration["MongoDb:DatabaseName"] ?? "IngestionRefineryDb");
        _collection = db.GetCollection<SupplierIngestionState>(
            configuration["MongoDb:StateCollectionName"] ?? "WolverineInfringementStates");
    }

    public async Task<bool> TryBeginProcessingAsync(SupplierIngestionState state, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(state, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task SaveAsync(SupplierIngestionState state, CancellationToken ct = default)
        => _collection.ReplaceOneAsync(
            x => x.CorrelationId == state.CorrelationId,
            state,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public Task DeleteAsync(Guid correlationId, CancellationToken ct = default)
        => _collection.DeleteOneAsync(x => x.CorrelationId == correlationId, ct);
}
