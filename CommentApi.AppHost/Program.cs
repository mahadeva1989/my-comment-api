var builder = DistributedApplication.CreateBuilder(args);

// --- Infrastructure resources ---

// Aspire starts a Postgres container automatically in dev.
// It injects the connection string into any service that calls .WithReference(postgres).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()           // persist data between runs
    .AddDatabase("commentsdb"); // matches our DB name

// Aspire starts a Redis container automatically in dev.
var redis = builder.AddRedis("redis");

// --- Application services ---

// Aspire launches our API as a child process, sets ASPNETCORE_URLS,
// and injects ConnectionStrings__DefaultConnection + ConnectionStrings__Redis
// via environment variables so appsettings.json values are overridden automatically.
builder.AddProject<Projects.my_comment_api>("api")
    .WithReference(postgres)    // injects Postgres connection string
    .WithReference(redis)       // injects Redis connection string
    .WithExternalHttpEndpoints(); // makes the API URL visible in the dashboard

builder.Build().Run();
