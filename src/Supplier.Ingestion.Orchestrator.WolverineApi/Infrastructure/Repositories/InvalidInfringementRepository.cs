using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;

public class InvalidInfringementRepository : IInvalidInfringementRepository
{
    private readonly IMongoCollection<InvalidInfringementDocument> _collection;

    public InvalidInfringementRepository(IMongoClient mongoClient, IConfiguration configuration)
    {
        var db = mongoClient.GetDatabase(configuration["MongoDb:DatabaseName"] ?? "IngestionRefineryDb");
        _collection = db.GetCollection<InvalidInfringementDocument>(
            configuration["MongoDb:InvalidCollectionName"] ?? "WolverineInvalidInfringements");
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
}
