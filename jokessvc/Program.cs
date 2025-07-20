using jokessvc.BackgroundServices;
using jokessvc.Models;
using jokessvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure JokeConfig
builder.Services.Configure<JokeConfig>(options =>
{
    var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI") 
        ?? builder.Configuration.GetValue<string>("MONGO_URI") 
        ?? "mongodb://novell:novell123@mongodb.mongo.svc.cluster.local:27017/jokedb?authSource=admin";
    
    // Replace the database name in the connection string to use jokedb instead of newsdb
    if (mongoUri.Contains("/newsdb"))
    {
        mongoUri = mongoUri.Replace("/newsdb", "/jokedb");
    }
    options.MongoUri = mongoUri;
    
    options.NatsUrl = Environment.GetEnvironmentVariable("NATS_URL") 
        ?? builder.Configuration.GetValue<string>("NATS_URL") 
        ?? "nats://nats.nats-system.svc.cluster.local:4222";
        
    options.RefreshIntervalMinutes = builder.Configuration.GetValue<int?>("RefreshIntervalMinutes") ?? 60;
    options.BatchSize = builder.Configuration.GetValue<int?>("BatchSize") ?? 200;
    options.RateLimitDelayMs = builder.Configuration.GetValue<int?>("RateLimitDelayMs") ?? 100;
});

// Register services
builder.Services.AddHttpClient();
builder.Services.AddScoped<JokeService>();
builder.Services.AddHostedService<BackgroundRefreshService>();

// Add controllers and API documentation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow 
}));

// Add readiness check that includes database connectivity
app.MapGet("/ready", async (JokeService jokeService) => 
{
    try
    {
        // Try to get total count to verify database connectivity
        var total = await jokeService.TotalJokesAsync();
        return Results.Ok(new { 
            status = "ready", 
            totalJokes = total,
            timestamp = DateTime.UtcNow 
        });
    }
    catch
    {
        return Results.Problem("Service not ready");
    }
});

app.Run();
