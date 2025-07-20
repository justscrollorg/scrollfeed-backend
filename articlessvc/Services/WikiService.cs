using articlessvc.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text.Json;

namespace articlessvc.Services;

public class WikiService
{
    private readonly HttpClient _httpClient;
    private readonly IMongoCollection<WikiArticle> _collection;
    private readonly WikiConfig _config;
    private readonly ILogger<WikiService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    public WikiService(
        IOptions<WikiConfig> config, 
        IHttpClientFactory clientFactory,
        ILogger<WikiService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _rateLimitSemaphore = new SemaphoreSlim(1, 1); // Ensure sequential requests

        var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI") ?? _config.MongoUri;
        var client = new MongoClient(mongoUri);
        var db = client.GetDatabase("wikidb");
        _collection = db.GetCollection<WikiArticle>("articles");

        // Create indexes for better performance
        CreateIndexesAsync().ConfigureAwait(false);

        _httpClient = clientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private async Task CreateIndexesAsync()
    {
        try
        {
            // Create index on title for faster searches
            var titleIndex = Builders<WikiArticle>.IndexKeys.Ascending(x => x.Title);
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<WikiArticle>(titleIndex));

            // Create index on creation timestamp for cleanup operations
            var timestampIndex = Builders<WikiArticle>.IndexKeys.Descending("_createdAt");
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<WikiArticle>(timestampIndex));

            _logger.LogInformation("MongoDB indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create indexes (they may already exist)");
        }
    }

    public async Task RefreshArticlesAsync(int batchSize = 200)
    {
        _logger.LogInformation("Starting article refresh with batch size: {BatchSize}", batchSize);

        try
        {
            // Clear all existing articles to ensure fresh content
            var totalCount = await _collection.CountDocumentsAsync(FilterDefinition<WikiArticle>.Empty);
            
            if (totalCount > 0)
            {
                var deleteResult = await _collection.DeleteManyAsync(FilterDefinition<WikiArticle>.Empty);
                _logger.LogInformation("Cleared all {DeletedCount} existing articles for fresh refresh", 
                    deleteResult.DeletedCount);
            }

            // Fetch new articles
            var successCount = 0;
            var failCount = 0;

            for (int i = 0; i < batchSize; i++)
            {
                await _rateLimitSemaphore.WaitAsync();
                try
                {
                    var article = await GetOneArticleWithRetryAsync();
                    if (article != null)
                    {
                        await _collection.InsertOneAsync(article);
                        successCount++;
                        _logger.LogDebug("Fetched article: {Title}", article.Title);
                    }
                    else
                    {
                        failCount++;
                    }

                    // Rate limiting - respect Wikipedia's guidelines
                    await Task.Delay(_config.RateLimitDelayMs);
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }
            }

            _logger.LogInformation("Article refresh completed. Success: {SuccessCount}, Failed: {FailCount}", 
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during article refresh");
            throw;
        }
    }

    private async Task<WikiArticle?> GetOneArticleWithRetryAsync()
    {
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var article = JsonSerializer.Deserialize<WikiArticle>(json);
                    
                    if (article != null && !string.IsNullOrEmpty(article.Title))
                    {
                        return article;
                    }
                }
                else
                {
                    _logger.LogWarning("Wikipedia API returned status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed to fetch article", attempt);
            }

            if (attempt < _config.MaxRetries)
            {
                await Task.Delay(_config.RetryDelayMs * attempt); // Exponential backoff
            }
        }

        _logger.LogError("Failed to fetch article after {MaxRetries} attempts", _config.MaxRetries);
        return null;
    }

    // Legacy method for backward compatibility
    public async Task PreloadArticlesAsync(int total = 200)
    {
        _logger.LogInformation("Preloading {Total} articles...", total);
        await RefreshArticlesAsync(total);
        _logger.LogInformation("Preloading completed");
    }

    public async Task<List<WikiArticle>> GetArticlesAsync(int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        return await _collection.Find(FilterDefinition<WikiArticle>.Empty)
                                .Sort(Builders<WikiArticle>.Sort.Descending("_createdAt"))
                                .Skip(skip)
                                .Limit(pageSize)
                                .ToListAsync();
    }

    public List<WikiArticle> GetArticles(int page, int pageSize)
    {
        return GetArticlesAsync(page, pageSize).GetAwaiter().GetResult();
    }

    public async Task<long> TotalArticlesAsync()
    {
        return await _collection.CountDocumentsAsync(FilterDefinition<WikiArticle>.Empty);
    }

    public int TotalArticles()
    {
        return (int)TotalArticlesAsync().GetAwaiter().GetResult();
    }

    public async Task<List<WikiArticle>> SearchArticlesAsync(string searchTerm, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetArticlesAsync(page, pageSize);
        }

        var filter = Builders<WikiArticle>.Filter.Or(
            Builders<WikiArticle>.Filter.Regex(x => x.Title, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
            Builders<WikiArticle>.Filter.Regex(x => x.Description, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"))
        );

        var skip = (page - 1) * pageSize;
        return await _collection.Find(filter)
                                .Sort(Builders<WikiArticle>.Sort.Descending("_createdAt"))
                                .Skip(skip)
                                .Limit(pageSize)
                                .ToListAsync();
    }

    public async Task<WikiArticle?> GetArticleByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
        _httpClient?.Dispose();
    }
}
