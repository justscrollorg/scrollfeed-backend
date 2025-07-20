using jokessvc.BackgroundServices;
using jokessvc.Models;
using jokessvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure JokeConfig
builder.Services.Configure<JokeConfig>(options =>
{
    options.MongoUri = Environment.GetEnvironmentVariable("MONGO_URI") 
        ?? builder.Configuration.GetValue<string>("MONGO_URI") 
        ?? "mongodb://mongo-0.mongo,mongo-1.mongo,mongo-2.mongo:27017/?replicaSet=rs0";
    
    options.NatsUrl = Environment.GetEnvironmentVariable("NATS_URL") 
        ?? builder.Configuration.GetValue<string>("NATS_URL") 
        ?? "nats://nats:4222";
        
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

app.Run();
