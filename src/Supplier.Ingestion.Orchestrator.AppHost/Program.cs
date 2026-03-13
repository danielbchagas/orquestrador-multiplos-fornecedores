var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var mongoDb = mongo.AddDatabase("MongoDb");

var kafka = builder.AddKafka("Kafka")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithKafkaUI();

builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongo)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
