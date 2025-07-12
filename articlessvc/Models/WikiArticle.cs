using System.Text.Json.Serialization;

namespace articlessvc.Models;

public class WikiArticle
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("extract")]
    public string? Extract { get; set; }

    [JsonPropertyName("thumbnail")]
    public WikiImage? Thumbnail { get; set; }

    [JsonPropertyName("content_urls")]
    public ContentUrls? Content_Urls { get; set; }
}

public class WikiImage
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class ContentUrls
{
    [JsonPropertyName("desktop")]
    public WikiDesktop? Desktop { get; set; }
}

public class WikiDesktop
{
    [JsonPropertyName("page")]
    public string? Page { get; set; }
}
