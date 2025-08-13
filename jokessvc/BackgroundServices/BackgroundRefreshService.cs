using jokessvc.Models;
using jokessvc.Services;
using Microsoft.Extensions.Options;
// using NATS.Client;
using System.Text.Json;

namespace jokessvc.BackgroundServices;

public class BackgroundRefreshService : BackgroundService
{
    private readonly JokeService _jokeService;
    private readonly ILogger<BackgroundRefreshService> _logger;
    private readonly JokeConfig _config;
    private readonly IServiceProvider _serviceProvider;
    // NATS removed
    private Timer? _refreshTimer;

    public BackgroundRefreshService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundRefreshService> logger,
        IOptions<JokeConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;

        // Get JokeService from DI container
        using var scope = serviceProvider.CreateScope();
        _jokeService = scope.ServiceProvider.GetRequiredService<JokeService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundRefreshService starting...");



        // Set up timer for periodic refresh
        var interval = TimeSpan.FromMinutes(_config.RefreshIntervalMinutes);
        _refreshTimer = new Timer(async _ => await PerformScheduledRefreshAsync(), 
            null, interval, interval);

        // Perform initial refresh
        await PerformInitialRefreshAsync();

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BackgroundRefreshService stopping...");
        }
    }



    private async Task PerformInitialRefreshAsync()
    {
        try
        {
            _logger.LogInformation("Performing initial jokes refresh...");
            
            using var scope = _serviceProvider.CreateScope();
            var jokeService = scope.ServiceProvider.GetRequiredService<JokeService>();
            
            var totalJokes = await jokeService.TotalJokesAsync();
            if (totalJokes == 0)
            {
                _logger.LogInformation("No jokes found, performing initial load...");
                await jokeService.RefreshJokesAsync(_config.BatchSize);
            }
            else
            {
                _logger.LogInformation("Found {TotalJokes} existing jokes, skipping initial refresh", totalJokes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial refresh");
        }
    }

    private async Task PerformScheduledRefreshAsync()
    {
        try
        {
            _logger.LogInformation("Performing scheduled jokes refresh...");
            
            using var scope = _serviceProvider.CreateScope();
            var jokeService = scope.ServiceProvider.GetRequiredService<JokeService>();
            await jokeService.RefreshJokesAsync(_config.BatchSize);
            
            _logger.LogInformation("Scheduled refresh completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled refresh");
        }
    }

    public override void Dispose()
    {
        _refreshTimer?.Dispose();
        base.Dispose();
    }
}
