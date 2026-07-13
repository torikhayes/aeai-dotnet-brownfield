var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("fake-redis");
var postgres = builder.AddPostgres("fake-postgres")
    .WithImage("example/custom-postgres")
    .WithImageTag("16.2");

builder.Build().Run();
