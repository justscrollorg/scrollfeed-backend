using jokessvc.Models;
using MongoDB.Driver;
using System.Text.Json;

namespace jokessvc.Services
{
    public class JokeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMongoCollection<Joke> _collection;

        public JokeService(IConfiguration config, IHttpClientFactory clientFactory)
        {
            var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI")
                         ?? config.GetValue<string>("MONGO_URI")
                         ?? "mongodb://mongo-0.mongo,mongo-1.mongo,mongo-2.mongo:27017/?replicaSet=rs0";

            var client = new MongoClient(mongoUri);
            var db = client.GetDatabase("jokedb");
            _collection = db.GetCollection<Joke>("jokes");

            _httpClient = clientFactory.CreateClient();
        }

        public async Task PreloadJokesAsync(int total = 10000)
        {
            Console.WriteLine($"Preloading {total} jokes...");

            await _collection.DeleteManyAsync(_ => true);

            var tasks = new List<Task>();

            for (int i = 0; i < total; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var joke = await GetOneJokeAsync();
                    if (joke != null)
                    {
                        await _collection.InsertOneAsync(joke);
                    }
                }));

                if (i % 100 == 0) await Task.Delay(100); // throttle ~100/s
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("Preloading done.");
        }

        private async Task<Joke?> GetOneJokeAsync()
        {
            try
            {
                var res = await _httpClient.GetAsync("https://official-joke-api.appspot.com/jokes/random");
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Joke>(json);
            }
            catch
            {
                return null;
            }
        }

        public List<Joke> GetJokes(int page, int pageSize)
        {
            return _collection.Find(_ => true)
                              .Skip((page - 1) * pageSize)
                              .Limit(pageSize)
                              .ToList();
        }

        public int TotalJokes()
        {
            return (int)_collection.CountDocuments(_ => true);
        }
    }
}
