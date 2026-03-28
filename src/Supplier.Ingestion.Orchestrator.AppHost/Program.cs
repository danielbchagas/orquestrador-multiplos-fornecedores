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

// Observability stack (Loki → Tempo → OTel Collector → Prometheus → Grafana)
// Os configs em deploy/files usam os nomes dos recursos como hostnames na rede Docker do Aspire.

var loki = builder.AddContainer("loki", "grafana/loki")
    .WithImageTag("3.4.2")
    .WithBindMount("../../deploy/files/loki.yaml", "/etc/loki/local-config.yaml", isReadOnly: true)
    .WithHttpEndpoint(name: "http", targetPort: 3100);

var tempo = builder.AddContainer("tempo", "grafana/tempo")
    .WithImageTag("2.7.2")
    .WithBindMount("../../deploy/files/tempo.yaml", "/etc/tempo.yaml", isReadOnly: true)
    .WithArgs("-config.file=/etc/tempo.yaml")
    .WithHttpEndpoint(name: "http", targetPort: 3200);

var otelCollector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib")
    .WithImageTag("0.123.0")
    .WithBindMount("../../deploy/files/otel-collector-config.yaml", "/etc/otelcol-contrib/config.yaml", isReadOnly: true)
    .WithHttpEndpoint(name: "grpc", targetPort: 4317)
    .WithHttpEndpoint(name: "http-otlp", targetPort: 4318)
    .WithHttpEndpoint(name: "metrics", targetPort: 8889)
    .WaitFor(loki)
    .WaitFor(tempo);

var prometheus = builder.AddContainer("prometheus", "prom/prometheus")
    .WithBindMount("../../deploy/files/prometheus.yaml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithBindMount("../../deploy/files/alert.rules.yaml", "/etc/prometheus/alert.rules.yaml", isReadOnly: true)
    .WithHttpEndpoint(name: "http", targetPort: 9090)
    .WaitFor(otelCollector);

var grafana = builder.AddContainer("grafana", "grafana/grafana")
    .WithBindMount("../../deploy/files/datasources.yaml", "/etc/grafana/provisioning/datasources/datasources.yaml", isReadOnly: true)
    .WithBindMount("../../deploy/files/grafana/dashboards", "/etc/grafana/provisioning/dashboards", isReadOnly: true)
    .WithHttpEndpoint(name: "http", targetPort: 3000)
    .WaitFor(prometheus)
    .WaitFor(loki)
    .WaitFor(tempo);

// Os projetos enviam telemetria para o OTel Collector (que alimenta Grafana).
// O Aspire Dashboard permanece ativo para gerenciamento de serviços.
var api = builder.AddProject<Projects.Supplier_Ingestion_Orchestrator_Api>("api")
    .WithReference(mongoDb)
    .WithReference(kafka)
    .WaitFor(mongoDb)
    .WaitFor(kafka)
    .WaitFor(otelCollector)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("grpc"))
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Supplier_A_Producer_Api>("supplier-a-producer")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitFor(otelCollector)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("grpc"))
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Supplier_B_Producer_Api>("supplier-b-producer")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitFor(otelCollector)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("grpc"))
    .WithExternalHttpEndpoints();

builder.Build().Run();
