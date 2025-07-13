using articlessvc.Models;
using System.Text.Json;

namespace articlessvc.Services;

public class WikiService
{
    private readonly HttpClient _httpClient;
    private readonly List<WikiArticle> _cache = new();
    private readonly object _lock = new();

    public WikiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "articlessvc/1.0 (anurag@example.com)");
    }

    public async Task PreloadArticlesAsync(int total = 200)
    {
        var tasks = new List<Task<WikiArticle>>();
        Console.WriteLine($"Preloading {total} articles...");
        for (int i = 0; i < total; i++)
        {
            tasks.Add(GetOneArticleAsync());
        }

        var articles = await Task.WhenAll(tasks);
        lock (_lock)
        {
            _cache.Clear();
            _cache.AddRange(articles.Where(a => a != null));
        }
        Console.WriteLine($"Done. Loaded {_cache.Count} articles.");
    }

    private async Task<WikiArticle> GetOneArticleAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            //Console.WriteLine("RAW JSON:");
            //Console.WriteLine(json);  

            return JsonSerializer.Deserialize<WikiArticle>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("⚠️ Deserialization error: " + ex.Message);
            return null;
        }
    }


    public List<WikiArticle> GetArticles(int page, int pageSize)
    {
        lock (_lock)
        {
            return _cache.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
    }

    public int TotalArticles()
    {
        lock (_lock) return _cache.Count;
    }
}
