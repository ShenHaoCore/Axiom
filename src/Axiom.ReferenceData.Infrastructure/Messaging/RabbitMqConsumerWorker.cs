// 消费地点事件，持久化去重后 ACK，失败消息通过 NACK 进入死信队列。
using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

public sealed class RabbitMqConsumerWorker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumerWorker> logger) : BackgroundService
{
    private const string CONSUMER_NAME = "reference-data-location-events";
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ 消费连接失败，将延迟重连。");
                await DelayBeforeRetryAsync(stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var connectionFactory = RabbitMqTopology.CreateConnectionFactory(configuration, "axiom-reference-data-consumer");
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitMqTopology.Declare(channel);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) =>
            await HandleDeliveryAsync(channel, eventArgs, cancellationToken);
        channel.BasicConsume(RabbitMqTopology.LOCATION_QUEUE, autoAck: false, consumer);
        logger.LogInformation("RabbitMQ 地点事件消费者已启动。");
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task HandleDeliveryAsync(
        IModel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var parentContext = GetParentContext(eventArgs);
        using var activity = ReferenceDataTelemetry.Source.StartActivity(
            "RabbitMQ consume",
            ActivityKind.Consumer,
            parentContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", RabbitMqTopology.LOCATION_QUEUE);
        activity?.SetTag("messaging.message_id", eventArgs.BasicProperties.MessageId);

        try
        {
            if (!Guid.TryParse(eventArgs.BasicProperties.MessageId, out var eventId))
            {
                throw new InvalidDataException("RabbitMQ 消息缺少有效的 MessageId。");
            }

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<InboxProcessor>();
            var processed = await processor.ProcessAsync(eventId, CONSUMER_NAME, cancellationToken);
            activity?.SetTag("messaging.axiom.processed", processed);
            channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (channel.IsOpen)
            {
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            var requeue = IsTransient(exception) && !eventArgs.Redelivered;
            if (requeue)
            {
                // 瞬时故障（数据库抖动等）且首次投递：放回队列再投一次，避免把可恢复错误变成永久死信。
                logger.LogWarning(exception, "消费消息 {MessageId} 遇到瞬时故障，将重新入队。", eventArgs.BasicProperties.MessageId);
            }
            else
            {
                logger.LogError(exception, "消费消息 {MessageId} 失败，将拒绝且不再入队。", eventArgs.BasicProperties.MessageId);
            }

            if (channel.IsOpen)
            {
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: requeue);
            }
        }
    }

    // 确定性失败（消息本身坏）直接死信；基础设施类瞬时故障允许一次重投（结合 Redelivered 标记有界重试）。
    private static bool IsTransient(Exception exception) =>
        exception is System.Data.Common.DbException
            or TimeoutException
            or System.Net.Sockets.SocketException
            or IOException;

    private static ActivityContext GetParentContext(BasicDeliverEventArgs eventArgs)
    {
        if (eventArgs.BasicProperties.Headers?.TryGetValue("traceparent", out var value) != true)
        {
            return default;
        }

        var traceParent = value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null
        };
        return ActivityContext.TryParse(traceParent, null, out var parentContext) ? parentContext : default;
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RetryInterval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
