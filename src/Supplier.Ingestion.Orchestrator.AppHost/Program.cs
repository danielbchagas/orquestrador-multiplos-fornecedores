var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithImageTag("7.0")
    .WithDataVolume("mongo-data")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("MongoDb", "IngestionRefineryDb");

var kafka = builder.AddKafka("Kafka")
    .WithDataVolume("kafka-data")
    .WithKafkaUI();

builder.AddContainer("init-kafka", "confluentinc/cp-kafka", "7.9.0")
    .WaitFor(kafka)
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", """
        kafka-topics --create --if-not-exists --bootstrap-server "$ConnectionStrings__Kafka" --partitions 2 --replication-factor 1 --topic source.supplier-a.v1 &&
        kafka-topics --create --if-not-exists --bootstrap-server "$ConnectionStrings__Kafka" --partitions 2 --replication-factor 1 --topic source.supplier-b.v1 &&
        kafka-topics --create --if-not-exists --bootstrap-server "$ConnectionStrings__Kafka" --partitions 2 --replication-factor 1 --topic target.processed.data.v1 &&
        kafka-topics --create --if-not-exists --bootstrap-server "$ConnectionStrings__Kafka" --partitions 1 --replication-factor 1 --topic target.invalid.data.v1 --config retention.ms=2592000000 &&
        echo 'Tópicos criados com sucesso!'
        """)
    .WithReference(kafka);

var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WithExternalHttpEndpoints();

builder.Build().Run();
