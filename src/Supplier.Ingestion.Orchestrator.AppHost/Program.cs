var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("mongo-data")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("MongoDb", "IngestionRefineryDb");

var kafka = builder.AddKafka("Kafka")
    .WithDataVolume("kafka-data")
    .WithKafkaUI();

var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
