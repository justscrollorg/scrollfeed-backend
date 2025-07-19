namespace articlessvc.Models;

public class WikiConfig
{
    public string NatsUrl { get; set; } = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
    public string MongoUri { get; set; } = Environment.GetEnvironmentVariable("MONGO_URI") ?? "mongodb://localhost:27017";
    public int RefreshIntervalMinutes { get; set; } = 1; // Temporarily set to 1 minute for testing
    public int BatchSize { get; set; } = 200;
    public int RateLimitDelayMs { get; set; } = 10; // ~100 requests/sec to respect Wikimedia's rate limit
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public string UserAgent { get; set; } = "articlessvc/2.0 (contact@example.com)";
}

public class RefreshRequest
{
    public string RequestId { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public string Priority { get; set; } = "normal";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class RefreshResult
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; }
}
