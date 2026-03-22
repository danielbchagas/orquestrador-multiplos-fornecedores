using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Persistence;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Repositories;

public class InvalidInfringementRepository : IInvalidInfringementRepository
{
    private readonly IMongoCollection<InvalidInfringementDocument> _collection;
    private readonly IMongoCollection<SupplierState> _sagaCollection;

    public InvalidInfringementRepository(IMongoClient mongoClient, IConfiguration configuration)
    {
        var db = mongoClient.GetDatabase(configuration["MongoDb:DatabaseName"] ?? "IngestionRefineryDb");
        _collection = db.GetCollection<InvalidInfringementDocument>("InvalidInfringements");
        _sagaCollection = db.GetCollection<SupplierState>("InfringementSagas");
    }

    public Task SaveAsync(InvalidInfringementDocument document, CancellationToken ct = default)
        => _collection.InsertOneAsync(document, cancellationToken: ct);

    public async Task<IReadOnlyList<InvalidInfringementDocument>> GetAllAsync(int limit = 50, CancellationToken ct = default)
    {
        var result = await _collection
            .Find(FilterDefinition<InvalidInfringementDocument>.Empty)
            .SortByDescending(x => x.FailedAt)
            .Limit(limit)
            .ToListAsync(ct);
        return result;
    }

    public async Task<InvalidInfringementDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        return result;
    }

    public Task IncrementRetryAsync(Guid id, CancellationToken ct = default)
    {
        var update = Builders<InvalidInfringementDocument>.Update
            .Inc(x => x.RetryCount, 1)
            .Set(x => x.LastRetryAt, DateTime.UtcNow);
        return _collection.UpdateOneAsync(x => x.Id == id, update, cancellationToken: ct);
    }

    public Task DeleteSagaAsync(Guid correlationId, CancellationToken ct = default)
        => _sagaCollection.DeleteOneAsync(x => x.CorrelationId == correlationId, ct);
}
