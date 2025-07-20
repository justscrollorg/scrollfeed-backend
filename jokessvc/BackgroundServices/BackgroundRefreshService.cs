using jokessvc.Models;
using jokessvc.Services;
using Microsoft.Extensions.Options;
using NATS.Client;
using System.Text.Json;

namespace jokessvc.BackgroundServices;

public class BackgroundRefreshService : BackgroundService
{
    private readonly JokeService _jokeService;
    private readonly ILogger<BackgroundRefreshService> _logger;
    private readonly JokeConfig _config;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _natsConnection;
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

        // Initialize NATS connection
        await InitializeNatsAsync();

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

    private Task InitializeNatsAsync()
    {
        var natsUrls = new[]
        {
            _config.NatsUrl,
            "nats://nats.nats-system.svc.cluster.local:4222", // Full DNS name
            "nats://nats:4222", // Short name fallback
            "nats://localhost:4222" // Local fallback
        };

        foreach (var natsUrl in natsUrls)
        {
            try
            {
                var factory = new ConnectionFactory();
                _natsConnection = factory.CreateConnection(natsUrl);
                
                // Subscribe to refresh requests
                _natsConnection.SubscribeAsync("jokes.refresh", OnRefreshRequested!);
                
                _logger.LogInformation("Connected to NATS at {NatsUrl}", natsUrl);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to NATS at {NatsUrl}", natsUrl);
            }
        }

        _logger.LogWarning("Could not connect to NATS. Manual refresh will still work.");
        return Task.CompletedTask;
    }

    private async void OnRefreshRequested(object? sender, MsgHandlerEventArgs e)
    {
        try
        {
            var message = System.Text.Encoding.UTF8.GetString(e.Message.Data);
            var request = JsonSerializer.Deserialize<RefreshRequest>(message);
            
            if (request != null)
            {
                _logger.LogInformation("Received NATS refresh request: {RequestId} with batch size {BatchSize}", 
                    request.RequestId, request.BatchSize);
                
                using var scope = _serviceProvider.CreateScope();
                var jokeService = scope.ServiceProvider.GetRequiredService<JokeService>();
                await jokeService.RefreshJokesAsync(request.BatchSize);
                
                _logger.LogInformation("Completed NATS refresh request: {RequestId}", request.RequestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NATS refresh request");
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
        _natsConnection?.Dispose();
        base.Dispose();
    }
}
