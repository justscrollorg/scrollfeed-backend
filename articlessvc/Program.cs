using articlessvc.Models;
using articlessvc.Services;
using articlessvc.Metrics;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configure WikiConfig
builder.Services.Configure<WikiConfig>(builder.Configuration.GetSection("Wiki"));

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<WikiService>();

// Register background service
builder.Services.AddHostedService<BackgroundRefreshService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for frontend access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize metrics
ArticlesMetrics.Initialize("articles-service", "1.0.0", app.Environment.EnvironmentName);

// Add Prometheus metrics middleware
app.UseMetricServer(); // This exposes /metrics endpoint
app.UseHttpMetrics(); // This captures HTTP metrics automatically

// Middleware
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Health endpoints
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow }));

// Initial preload (optional - the background service will handle regular refreshes)
using (var scope = app.Services.CreateScope())
{
    var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var totalArticles = wikiService.TotalArticles();
        if (totalArticles == 0)
        {
            logger.LogInformation("No articles found, performing initial preload...");
            await wikiService.PreloadArticlesAsync(50); // Smaller initial batch
            logger.LogInformation("Initial preload completed with {Count} articles", wikiService.TotalArticles());
        }
        else
        {
            logger.LogInformation("Found {Count} existing articles", totalArticles);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during initial setup");
    }
}

app.Run();
