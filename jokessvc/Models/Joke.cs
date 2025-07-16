using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace jokessvc.Models
{
    public class Joke
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonIgnore]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("setup")]
        public string? Setup { get; set; }

        [JsonPropertyName("punchline")]
        public string? Punchline { get; set; }

        [JsonPropertyName("id")]
        public int JokeId { get; set; }
    }
}
