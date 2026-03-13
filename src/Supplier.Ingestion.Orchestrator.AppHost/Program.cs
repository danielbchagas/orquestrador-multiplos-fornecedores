var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongodb")
    .WithDataVolume();

var mongoDb = mongo.AddDatabase("IngestionRefineryDb");

var kafka = builder.AddKafka("kafka")
    .WithKafkaUI();

builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongo)
    .WaitFor(kafka);

builder.Build().Run();
