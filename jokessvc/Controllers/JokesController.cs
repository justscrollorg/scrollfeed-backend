using jokessvc.Models;
using jokessvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace jokessvc.Controllers;

[ApiController]
[Route("jokes-api")]
public class JokesController : ControllerBase
{
    private readonly JokeService _jokeService;
    private readonly ILogger<JokesController> _logger;
    private readonly JokeConfig _config;

    public JokesController(
        JokeService jokeService, 
        ILogger<JokesController> logger,
        IOptions<JokeConfig> config)
    {
        _jokeService = jokeService;
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
            // Add cache-busting headers to prevent frontend caching
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Invalid page or pageSize parameters");
            }

            List<Joke> jokes;
            long total;

            if (!string.IsNullOrWhiteSpace(search))
            {
                jokes = await _jokeService.SearchJokesAsync(search, page, pageSize);
                // For simplicity, we'll use the same total count. In production, you'd want a separate count query for search results.
                total = await _jokeService.TotalJokesAsync();
            }
            else
            {
                jokes = await _jokeService.GetJokesAsync(page, pageSize);
                total = await _jokeService.TotalJokesAsync();
            }

            var response = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling((double)total / pageSize),
                jokes = jokes.Select(j => new { j.Type, j.Setup, j.Punchline, j.JokeId }),
                search
            };

            _logger.LogInformation("Retrieved {Count} jokes for page {Page} (search: {Search})", 
                jokes.Count, page, search ?? "none");

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving jokes");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var joke = await _jokeService.GetJokeByIdAsync(id);
            if (joke == null)
            {
                return NotFound(new { error = "Joke not found" });
            }

            return Ok(joke);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving joke {JokeId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> TriggerRefresh([FromQuery] int batchSize = 200)
    {
        try
        {
            if (batchSize < 1 || batchSize > 1000)
            {
                return BadRequest("Batch size must be between 1 and 1000");
            }

            // Perform direct refresh
            await _jokeService.RefreshJokesAsync(batchSize);
            var total = await _jokeService.TotalJokesAsync();
            
            return Ok(new { 
                message = "Refresh completed", 
                totalJokes = total,
                batchSize = batchSize
            });
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
            var total = await _jokeService.TotalJokesAsync();
            
            return Ok(new
            {
                totalJokes = total,
                refreshIntervalMinutes = _config.RefreshIntervalMinutes,
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
