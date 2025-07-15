using articlessvc.Models;
using MongoDB.Driver;
using System.Text.Json;

namespace articlessvc.Services;

public class WikiService
{
    private readonly HttpClient _httpClient;
    private readonly IMongoCollection<WikiArticle> _collection;

    public WikiService(IConfiguration config, IHttpClientFactory clientFactory)
    {
        var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI")
                      ?? config.GetValue<string>("MONGO_URI")
                      ?? "mongodb://mongo-0.mongo,mongo-1.mongo,mongo-2.mongo:27017/?replicaSet=rs0";

        var client = new MongoClient(mongoUri);
        var db = client.GetDatabase("wikidb");
        _collection = db.GetCollection<WikiArticle>("articles");

        _httpClient = clientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "articlessvc/1.0 (anurag@example.com)");
    }

    public async Task PreloadArticlesAsync(int total = 200)
    {
        Console.WriteLine($"Preloading {total} articles...");

        await _collection.DeleteManyAsync(FilterDefinition<WikiArticle>.Empty);

        for (int i = 0; i < total; i++)
        {
            var article = await GetOneArticleAsync();
            if (article != null)
            {
                await _collection.InsertOneAsync(article);
                await Task.Delay(10); // ~100 requests/sec to respect Wikimedia's rate limit
            }
        }

        Console.WriteLine("Preloading done.");
    }

    private async Task<WikiArticle?> GetOneArticleAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WikiArticle>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching article: {ex.Message}");
            return null;
        }
    }

    public List<WikiArticle> GetArticles(int page, int pageSize)
    {
        return _collection.Find(_ => true)
                          .Skip((page - 1) * pageSize)
                          .Limit(pageSize)
                          .ToList();
    }

    public int TotalArticles()
    {
        return (int)_collection.CountDocuments(_ => true);
    }
}
