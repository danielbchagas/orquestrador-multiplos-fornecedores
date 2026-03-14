using Confluent.Kafka;
using Confluent.Kafka.Admin;

var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithImageTag("7.0")
    .WithDataVolume("mongo-data")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("MongoDb", "IngestionRefineryDb");

var kafka = builder.AddKafka("Kafka")
    .WithDataVolume("kafka-data")
    .WithKafkaUI();

builder.Eventing.Subscribe<ResourceReadyEvent>(kafka.Resource, async (@event, ct) =>
{
    var connectionString = await ((IResourceWithConnectionString)kafka.Resource).GetConnectionStringAsync(ct);

    using var adminClient = new AdminClientBuilder(new AdminClientConfig
    {
        BootstrapServers = connectionString
    }).Build();

    var topics = new[]
    {
        new TopicSpecification { Name = "source.supplier-a.v1",      NumPartitions = 2, ReplicationFactor = 1 },
        new TopicSpecification { Name = "source.supplier-b.v1",      NumPartitions = 2, ReplicationFactor = 1 },
        new TopicSpecification { Name = "target.processed.data.v1",  NumPartitions = 2, ReplicationFactor = 1 },
        new TopicSpecification
        {
            Name = "target.invalid.data.v1",
            NumPartitions = 1,
            ReplicationFactor = 1,
            Configs = new Dictionary<string, string> { ["retention.ms"] = "2592000000" }
        }
    };

    try
    {
        await adminClient.CreateTopicsAsync(topics);
    }
    catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
    {
        // tópicos já existem, sem ação necessária
    }
});

var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
