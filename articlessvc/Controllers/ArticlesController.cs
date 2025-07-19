using articlessvc.Models;
using articlessvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NATS.Client;
using System.Text.Json;

namespace articlessvc.Controllers;

[ApiController]
[Route("articles-api")]
public class ArticlesController : ControllerBase
{
    private readonly WikiService _wikiService;
    private readonly ILogger<ArticlesController> _logger;
    private readonly WikiConfig _config;

    public ArticlesController(
        WikiService wikiService, 
        ILogger<ArticlesController> logger,
        IOptions<WikiConfig> config)
    {
        _wikiService = wikiService;
        _logger = logger;
        _config = config.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        try
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Invalid page or pageSize parameters");
            }

            List<WikiArticle> articles;
            long total;

            if (!string.IsNullOrWhiteSpace(search))
            {
                articles = await _wikiService.SearchArticlesAsync(search, page, pageSize);
                // For simplicity, we'll use the same total count. In production, you'd want a separate count query for search results.
                total = await _wikiService.TotalArticlesAsync();
            }
            else
            {
                articles = await _wikiService.GetArticlesAsync(page, pageSize);
                total = await _wikiService.TotalArticlesAsync();
            }

            var response = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling((double)total / pageSize),
                articles,
                search
            };

            _logger.LogInformation("Retrieved {Count} articles for page {Page} (search: {Search})", 
                articles.Count, page, search ?? "none");

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving articles");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var article = await _wikiService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound(new { error = "Article not found" });
            }

            return Ok(article);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving article {ArticleId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> TriggerRefresh([FromQuery] int batchSize = 100)
    {
        try
        {
            if (batchSize < 1 || batchSize > 1000)
            {
                return BadRequest("Batch size must be between 1 and 1000");
            }

            // Try to use NATS for refresh, fall back to direct call
            try
            {
                var factory = new ConnectionFactory();
                using var connection = factory.CreateConnection(_config.NatsUrl);
                
                var refreshRequest = new RefreshRequest
                {
                    BatchSize = batchSize,
                    RequestId = Guid.NewGuid().ToString(),
                    Priority = "manual"
                };

                var message = JsonSerializer.Serialize(refreshRequest);
                connection.Publish("articles.refresh", System.Text.Encoding.UTF8.GetBytes(message));
                
                _logger.LogInformation("Triggered refresh via NATS: {RequestId}", refreshRequest.RequestId);
                
                return Accepted(new { 
                    message = "Refresh triggered", 
                    requestId = refreshRequest.RequestId,
                    batchSize 
                });
            }
            catch (Exception natsEx)
            {
                _logger.LogWarning(natsEx, "NATS not available, performing direct refresh");
                
                // Direct refresh as fallback
                await _wikiService.RefreshArticlesAsync(batchSize);
                var total = await _wikiService.TotalArticlesAsync();
                
                return Ok(new { 
                    message = "Refresh completed directly", 
                    totalArticles = total 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering refresh");
            return StatusCode(500, new { error = "Failed to trigger refresh" });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var total = await _wikiService.TotalArticlesAsync();
            
            return Ok(new
            {
                totalArticles = total,
                refreshIntervalHours = _config.RefreshIntervalHours,
                rateLimitMs = _config.RateLimitDelayMs,
                batchSize = _config.BatchSize,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

}
