using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    .AddDatabase("app");

var redis = builder.AddRedis("redis");

// M6: the sample owns its schema; the same project applies its migration and exits.
var migration = builder.AddProject<Trivium_Sample_Persistence>("persistence-migrate")
    .WithArgs("--migrate")
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<Trivium_Sample_Persistence>("persistence")
    .WithReference(db)
    .WithReference(redis)
    .WaitForCompletion(migration);

builder.Build().Run();
