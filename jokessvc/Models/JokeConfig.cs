namespace jokessvc.Models;

public class JokeConfig
{
    public string MongoUri { get; set; } = Environment.GetEnvironmentVariable("MONGO_URI") ?? "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "jokesdb";
    public string CollectionName { get; set; } = "jokes";
    public string NatsUrl { get; set; } = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
    public string JokeApiUrl { get; set; } = "https://official-joke-api.appspot.com/jokes/random";
    public int RefreshIntervalMinutes { get; set; } = 10; // 10 minute refresh
    public int BatchSize { get; set; } = 200;
    public int RateLimitDelayMs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
}
