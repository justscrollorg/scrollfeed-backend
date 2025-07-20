namespace jokessvc.Models;

public class JokeConfig
{
    public string MongoUri { get; set; } = "mongodb://mongo-0.mongo,mongo-1.mongo,mongo-2.mongo:27017/?replicaSet=rs0";
    public string DatabaseName { get; set; } = "jokedb";
    public string CollectionName { get; set; } = "jokes";
    public string NatsUrl { get; set; } = "nats://nats:4222";
    public string JokeApiUrl { get; set; } = "https://official-joke-api.appspot.com/jokes/random";
    public int RefreshIntervalMinutes { get; set; } = 60; // 1 hour refresh
    public int BatchSize { get; set; } = 200;
    public int RateLimitDelayMs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
}
