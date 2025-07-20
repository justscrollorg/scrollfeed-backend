using jokessvc.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text.Json;

namespace jokessvc.Services;

public class JokeService
{
    private readonly HttpClient _httpClient;
    private readonly IMongoCollection<Joke> _collection;
    private readonly ILogger<JokeService> _logger;
    private readonly JokeConfig _config;

    public JokeService(
        IHttpClientFactory clientFactory, 
        ILogger<JokeService> logger,
        IOptions<JokeConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        
        var client = new MongoClient(_config.MongoUri);
        var db = client.GetDatabase(_config.DatabaseName);
        _collection = db.GetCollection<Joke>(_config.CollectionName);

        _httpClient = clientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task RefreshJokesAsync(int batchSize = 200)
    {
        _logger.LogInformation("Starting jokes refresh with batch size {BatchSize}", batchSize);
        
        try
        {
            // Clear all existing jokes for a complete refresh
            var deleteResult = await _collection.DeleteManyAsync(FilterDefinition<Joke>.Empty);
            _logger.LogInformation("Cleared {DeletedCount} existing jokes", deleteResult.DeletedCount);

            var jokes = new List<Joke>();
            var successCount = 0;
            var failCount = 0;

            for (int i = 0; i < batchSize; i++)
            {
                try
                {
                    var joke = await FetchJokeWithRetryAsync();
                    if (joke != null)
                    {
                        jokes.Add(joke);
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }

                    // Rate limiting
                    if (_config.RateLimitDelayMs > 0)
                    {
                        await Task.Delay(_config.RateLimitDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch joke {Index}", i + 1);
                    failCount++;
                }
            }

            // Batch insert all jokes
            if (jokes.Count > 0)
            {
                await _collection.InsertManyAsync(jokes);
                _logger.LogInformation("Successfully inserted {Count} jokes", jokes.Count);
            }

            _logger.LogInformation("Jokes refresh completed. Success: {Success}, Failed: {Failed}", 
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during jokes refresh");
            throw;
        }
    }

    private async Task<Joke?> FetchJokeWithRetryAsync()
    {
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(_config.JokeApiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var joke = JsonSerializer.Deserialize<Joke>(json);
                    
                    if (joke != null && !string.IsNullOrWhiteSpace(joke.Setup) && !string.IsNullOrWhiteSpace(joke.Punchline))
                    {
                        return joke;
                    }
                }
                
                _logger.LogWarning("Invalid response from joke API on attempt {Attempt}: {StatusCode}", 
                    attempt, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed to fetch joke", attempt);
            }

            if (attempt < _config.MaxRetries)
            {
                await Task.Delay(1000 * attempt); // Exponential backoff
            }
        }

        return null;
    }

    public async Task<List<Joke>> GetJokesAsync(int page, int pageSize)
    {
        try
        {
            var skip = (page - 1) * pageSize;
            var jokes = await _collection
                .Find(FilterDefinition<Joke>.Empty)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();

            return jokes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving jokes for page {Page}, pageSize {PageSize}", page, pageSize);
            throw;
        }
    }

    public async Task<List<Joke>> SearchJokesAsync(string searchTerm, int page, int pageSize)
    {
        try
        {
            var filter = Builders<Joke>.Filter.Or(
                Builders<Joke>.Filter.Regex(j => j.Setup, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                Builders<Joke>.Filter.Regex(j => j.Punchline, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                Builders<Joke>.Filter.Regex(j => j.Type, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"))
            );

            var skip = (page - 1) * pageSize;
            var jokes = await _collection
                .Find(filter)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();

            return jokes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching jokes with term '{SearchTerm}'", searchTerm);
            throw;
        }
    }

    public async Task<Joke?> GetJokeByIdAsync(string id)
    {
        try
        {
            var filter = Builders<Joke>.Filter.Eq(j => j.Id, id);
            var joke = await _collection.Find(filter).FirstOrDefaultAsync();
            return joke;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving joke with id {JokeId}", id);
            throw;
        }
    }

    public async Task<long> TotalJokesAsync()
    {
        try
        {
            return await _collection.CountDocumentsAsync(FilterDefinition<Joke>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting total jokes");
            throw;
        }
    }
}
