// 后台轮询 Outbox，Broker 故障时延迟重试且不影响业务请求。
using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Api.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxPublishProcessor>();
                await processor.PublishPendingAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox 发布失败，将在下次轮询时重试。");
                await DelayBeforeRetryAsync(stoppingToken);
            }
        }
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
