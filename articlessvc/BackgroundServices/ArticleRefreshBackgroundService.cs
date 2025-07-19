using articlessvc.Services;

namespace articlessvc.BackgroundServices;

public class ArticleRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArticleRefreshBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public ArticleRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ArticleRefreshBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get refresh interval from configuration (default: 6 hours)
        var refreshIntervalHours = _configuration.GetValue<int>("ArticleRefresh:IntervalHours", 6);
        var refreshInterval = TimeSpan.FromHours(refreshIntervalHours);

        // Get batch size for incremental refresh (default: 50)
        var batchSize = _configuration.GetValue<int>("ArticleRefresh:BatchSize", 50);

        // Initial delay before starting refresh cycle (default: 30 minutes after startup)
        var initialDelayMinutes = _configuration.GetValue<int>("ArticleRefresh:InitialDelayMinutes", 30);
        
        _logger.LogInformation("Article refresh background service started. " +
            "Refresh interval: {RefreshInterval}, Batch size: {BatchSize}, Initial delay: {InitialDelay} minutes",
            refreshInterval, batchSize, initialDelayMinutes);

        // Wait for initial delay before starting refresh cycle
        await Task.Delay(TimeSpan.FromMinutes(initialDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();

                _logger.LogInformation("Starting periodic article refresh...");

                // Perform incremental refresh
                await wikiService.RefreshArticlesIncrementallyAsync(batchSize);

                // Optionally add some new articles too
                var addNewCount = _configuration.GetValue<int>("ArticleRefresh:AddNewCount", 5);
                if (addNewCount > 0)
                {
                    await wikiService.AddNewArticlesAsync(addNewCount);
                }

                _logger.LogInformation("Periodic article refresh completed. Next refresh in {RefreshInterval}",
                    refreshInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic article refresh");
            }

            // Wait for the next refresh cycle
            await Task.Delay(refreshInterval, stoppingToken);
        }

        _logger.LogInformation("Article refresh background service stopped");
    }
}