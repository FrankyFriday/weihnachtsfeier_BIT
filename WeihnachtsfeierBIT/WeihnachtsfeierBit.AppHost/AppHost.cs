var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("weihnachtsfeierdb");

var apiService = builder.AddProject<Projects.WeihnachtsfeierBit_ApiService>("apiservice")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

var adminClient = builder.AddProject<Projects.WeihnachtsfeierBit_AdminClient>("adminclient")
    .WithReference(apiService)
    .WaitFor(apiService);

var participantClient = builder.AddProject<Projects.WeihnachtsfeierBit_ParticipantClient>("participantclient")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.WeihnachtsfeierBit_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(adminClient)
    .WithReference(participantClient)
    .WithEnvironment("AdminClientBaseUrl", adminClient.GetEndpoint("http"))
    .WithEnvironment("ParticipantClientBaseUrl", participantClient.GetEndpoint("http"))
    .WaitFor(apiService)
    .WaitFor(adminClient)
    .WaitFor(participantClient);

builder.Build().Run();
