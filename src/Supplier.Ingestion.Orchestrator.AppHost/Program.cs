var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("mongo-data")
    .WithMongoExpress(configureContainer: me =>
    {
        me.WithLifetime(ContainerLifetime.Persistent);
    });

var mongoDb = mongo.AddDatabase("MongoDb", "IngestionRefineryDb");

var kafka = builder.AddKafka("Kafka")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("kafka-data")
    .WithKafkaUI(configureContainer: kui =>
    {
        kui.WithLifetime(ContainerLifetime.Persistent);
    });

var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
