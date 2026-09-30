using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderSubmission.Application.Notifications;

namespace OrderSubmission.Infrastructure.Notifications;

internal sealed partial class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationDispatchOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(settings.PollingInterval);

        do
        {
            await DrainAsync(settings.BatchSize, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DrainAsync(int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            int processed;
            do
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                processed = await dispatcher.DispatchDueAsync(stoppingToken);
            }
            while (processed == batchSize && !stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogCycleFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification worker is disabled by configuration")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification dispatch cycle failed; it will be retried on the next tick")]
    private partial void LogCycleFailed(Exception exception);
}
