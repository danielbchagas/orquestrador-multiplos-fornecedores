var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithImageTag("7.0")
    .WithDataVolume("mongo-data")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("MongoDb", "IngestionRefineryDb");

var kafka = builder.AddKafka("Kafka")
    .WithDataVolume("kafka-data")
    .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
    .WithEnvironment("KAFKA_NUM_PARTITIONS", "2")
    .WithKafkaUI();

var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
