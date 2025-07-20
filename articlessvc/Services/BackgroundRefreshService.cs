using articlessvc.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NATS.Client;
using System.Text.Json;

namespace articlessvc.Services;

public class BackgroundRefreshService : BackgroundService
{
    private readonly ILogger<BackgroundRefreshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WikiConfig _config;
    private IConnection? _natsConnection;
    private IAsyncSubscription? _subscription;

    public BackgroundRefreshService(
        ILogger<BackgroundRefreshService> logger,
        IServiceProvider serviceProvider,
        IOptions<WikiConfig> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Background Refresh Service with {IntervalMinutes} minute intervals...", _config.RefreshIntervalMinutes);

        // Initialize NATS connection (don't let this fail the service)
        try
        {
            await InitializeNatsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NATS initialization failed, will continue with direct refresh mode");
        }

        // Start periodic refresh timer
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_config.RefreshIntervalMinutes));

        // Initial refresh (don't let this fail the timer)
        _logger.LogInformation("Performing initial refresh...");
        try
        {
            await ScheduleRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial refresh failed, but timer will continue");
        }

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("Timer triggered - performing scheduled refresh...");
                try
                {
                    await ScheduleRefreshAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled refresh failed, but timer will continue");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background Refresh Service is stopping.");
        }
    }

    private async Task InitializeNatsAsync()
    {
        try
        {
            var factory = new ConnectionFactory();
            
            // Try multiple NATS URLs for better connectivity
            var natsUrls = new[]
            {
                _config.NatsUrl, // Original URL from config
                "nats://10.2.0.172:4222", // Direct pod IP we found earlier
                "nats://nats-0.nats.nats-system.svc.cluster.local:4222" // Full pod DNS
            };

            Exception? lastException = null;
            foreach (var url in natsUrls)
            {
                try
                {
                    _logger.LogInformation("Attempting NATS connection to: {Url}", url);
                    _natsConnection = await Task.Run(() => factory.CreateConnection(url));
                    
                    // Subscribe to refresh requests
                    _subscription = await Task.Run(() => _natsConnection.SubscribeAsync("articles.refresh", async (sender, args) =>
                    {
                        try
                        {
                            var refreshRequest = JsonSerializer.Deserialize<RefreshRequest>(args.Message.Data);
                            if (refreshRequest != null)
                            {
                                await HandleRefreshRequestAsync(refreshRequest);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing refresh request");
                        }
                    }));

                    _logger.LogInformation("Connected to NATS at {NatsUrl}", url);
                    return; // Success, exit the method
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Failed to connect to NATS at {Url}", url);
                }
            }
            
            throw lastException ?? new Exception("All NATS connection attempts failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to NATS");
        }
    }

    private async Task ScheduleRefreshAsync()
    {
        _logger.LogInformation("ScheduleRefreshAsync called");
        
        if (_natsConnection == null)
        {
            _logger.LogInformation("NATS connection not available, performing direct refresh");
            await HandleRefreshRequestAsync(new RefreshRequest 
            { 
                BatchSize = _config.BatchSize,
                RequestId = Guid.NewGuid().ToString(),
                Priority = "normal"
            });
            return;
        }

        try
        {
            var refreshRequest = new RefreshRequest
            {
                BatchSize = _config.BatchSize,
                RequestId = Guid.NewGuid().ToString(),
                Priority = "scheduled",
                Timestamp = DateTime.UtcNow
            };

            var message = JsonSerializer.Serialize(refreshRequest);
            _natsConnection.Publish("articles.refresh", System.Text.Encoding.UTF8.GetBytes(message));
            
            _logger.LogInformation("Scheduled article refresh: {RequestId}", refreshRequest.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule refresh via NATS");
        }
    }

    private async Task HandleRefreshRequestAsync(RefreshRequest request)
    {
        _logger.LogInformation("Processing refresh request: {RequestId}, BatchSize: {BatchSize}", 
            request.RequestId, request.BatchSize);

        using var scope = _serviceProvider.CreateScope();
        var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();
        
        try
        {
            await wikiService.RefreshArticlesAsync(request.BatchSize);
            
            // Publish completion message
            if (_natsConnection != null)
            {
                var result = new RefreshResult
                {
                    RequestId = request.RequestId,
                    Success = true,
                    ProcessedCount = request.BatchSize,
                    CompletedAt = DateTime.UtcNow
                };

                var resultMessage = JsonSerializer.Serialize(result);
                _natsConnection.Publish("articles.refresh.result", System.Text.Encoding.UTF8.GetBytes(resultMessage));
            }

            _logger.LogInformation("Completed refresh request: {RequestId}", request.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process refresh request: {RequestId}", request.RequestId);
            
            // Publish error message
            if (_natsConnection != null)
            {
                var result = new RefreshResult
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = ex.Message,
                    CompletedAt = DateTime.UtcNow
                };

                var resultMessage = JsonSerializer.Serialize(result);
                _natsConnection.Publish("articles.refresh.result", System.Text.Encoding.UTF8.GetBytes(resultMessage));
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Background Refresh Service...");
        
        _subscription?.Dispose();
        _natsConnection?.Close();
        
        await base.StopAsync(cancellationToken);
    }
}
