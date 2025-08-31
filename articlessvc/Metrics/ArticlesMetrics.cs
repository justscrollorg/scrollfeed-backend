using Prometheus;

namespace articlessvc.Metrics
{
    public static class ArticlesMetrics
    {
        // HTTP request metrics
        public static readonly Counter HttpRequestsTotal = Prometheus.Metrics
            .CreateCounter("http_requests_total", "Total number of HTTP requests", 
                new[] { "method", "path", "status_code", "service" });

        public static readonly Histogram HttpRequestDuration = Prometheus.Metrics
            .CreateHistogram("http_request_duration_seconds", "HTTP request duration in seconds",
                new[] { "method", "path", "service" });

        // Business metrics for articles service
        public static readonly Counter WikiArticlesFetched = Prometheus.Metrics
            .CreateCounter("wiki_articles_fetched_total", "Total number of Wikipedia articles fetched",
                new[] { "category", "status" });

        public static readonly Counter ArticlesServed = Prometheus.Metrics
            .CreateCounter("articles_served_total", "Total number of articles served to clients",
                new[] { "category", "source" });

        public static readonly Gauge ArticlesCacheSize = Prometheus.Metrics
            .CreateGauge("articles_cache_size", "Number of articles currently in cache");

        public static readonly Counter BackgroundRefreshRuns = Prometheus.Metrics
            .CreateCounter("background_refresh_runs_total", "Total number of background refresh runs",
                new[] { "status" });

        public static readonly Histogram BackgroundRefreshDuration = Prometheus.Metrics
            .CreateHistogram("background_refresh_duration_seconds", "Background refresh duration in seconds");

        // Application health metrics
        public static readonly Gauge ApplicationInfo = Prometheus.Metrics
            .CreateGauge("application_info", "Application information",
                new[] { "service", "version", "environment" });

        // Initialize metrics with default values
        public static void Initialize(string serviceName, string version, string environment)
        {
            ApplicationInfo.WithLabels(serviceName, version, environment).Set(1);
        }
    }
}
