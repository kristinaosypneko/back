namespace WeigthApiService;

public class HttpRequestMetricsLogger : BackgroundService
{
    private readonly ILogger<HttpRequestMetricsLogger> _logger;
    public static long HttpRequestsLastInterval = 0;
    private readonly TimeSpan _logInterval = TimeSpan.FromMinutes(1);

    public HttpRequestMetricsLogger(ILogger<HttpRequestMetricsLogger> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HttpRequestMetricsLogger is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_logInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Task.Delay был отменен, выходим из цикла
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            var requests = Interlocked.Exchange(ref HttpRequestsLastInterval, 0);
            _logger.LogInformation("API HTTP Requests in the last minute: {RequestCount}", requests);
        }

        _logger.LogInformation("HttpRequestMetricsLogger is stopping.");
    }
}