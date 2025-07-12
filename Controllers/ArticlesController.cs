using articlessvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace articlessvc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly WikiService _wikiService;
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(WikiService wikiService, ILogger<ArticlesController> logger)
    {
        _wikiService = wikiService;
        _logger = logger;
    }

    [HttpGet("preload")]
    public async Task<IActionResult> Preload()
    {
        await _wikiService.PreloadArticlesAsync(200);

        var total = _wikiService.TotalArticles();
        var sample = _wikiService.GetArticles(1, 5);

        _logger.LogInformation("Preloaded {Count} articles from Wikipedia.", total);

        foreach (var article in sample)
        {
            _logger.LogInformation("🔸 {Title} - {Url}",
                article.Title,
                article.Content_Urls?.Desktop?.Page);
        }

        return Ok(new { message = $"Articles preloaded: {total}" });
    }


    [HttpGet]
    public IActionResult GetPaginated([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var total = _wikiService.TotalArticles();
        var articles = _wikiService.GetArticles(page, pageSize);

        var response = new
        {
            page,
            pageSize,
            total,
            articles = articles.Select(a => new
            {
                a.Title,
                a.Description,
                a.Extract,
                Image = a.Thumbnail?.Source,
                Url = a.Content_Urls?.Desktop?.Page
            })
        };

        // ✅ Log the serialized response to console
        _logger.LogInformation("Paginated Response: {ResponseJson}", JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return Ok(response);
    }

}
