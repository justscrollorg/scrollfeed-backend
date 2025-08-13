using articlessvc.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace articlessvc.Services;

public class BackgroundRefreshService : BackgroundService
{
    private readonly ILogger<BackgroundRefreshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WikiConfig _config;


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



    private async Task ScheduleRefreshAsync()
    {
        _logger.LogInformation("ScheduleRefreshAsync called (direct mode, no NATS)");
        await HandleRefreshRequestAsync(new RefreshRequest 
        { 
            BatchSize = _config.BatchSize,
            RequestId = Guid.NewGuid().ToString(),
            Priority = "normal"
        });
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
            


            _logger.LogInformation("Completed refresh request: {RequestId}", request.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process refresh request: {RequestId}", request.RequestId);
            

        }
    }


}
