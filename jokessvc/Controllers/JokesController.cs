using jokessvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace jokessvc.Controllers;

[ApiController]
[Route("jokes-api")]
public class JokesController : ControllerBase
{
    private readonly JokeService _jokeService;
    private readonly ILogger<JokesController> _logger;

    public JokesController(JokeService jokeService, ILogger<JokesController> logger)
    {
        _jokeService = jokeService;
        _logger = logger;
    }

    [HttpGet("preload")]
    public async Task<IActionResult> Preload()
    {
        await _jokeService.PreloadJokesAsync(10000);

        var total = _jokeService.TotalJokes();
        var sample = _jokeService.GetJokes(1, 5);

        _logger.LogInformation("Preloaded {Count} jokes.", total);
        foreach (var joke in sample)
        {
            _logger.LogInformation("{Setup} - {Punchline}", joke.Setup, joke.Punchline);
        }

        return Ok(new { message = $"Jokes preloaded: {total}" });
    }

    [HttpGet]
    public IActionResult GetPaginated([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var total = _jokeService.TotalJokes();
        var jokes = _jokeService.GetJokes(page, pageSize);

        var response = new
        {
            page,
            pageSize,
            total,
            jokes = jokes.Select(j => new { j.Type, j.Setup, j.Punchline })
        };

        _logger.LogInformation("Paginated Response: {ResponseJson}", JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return Ok(response);
    }
}
