// 使用持久化消息和 Publisher Confirm 发布 Outbox 集成事件。
using System.Diagnostics;
using System.Text;
using Axiom.ReferenceData.Infrastructure.Messaging;
using RabbitMQ.Client;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(5);
    private readonly ConnectionFactory _connectionFactory;
    private readonly object _sync = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _connectionFactory = RabbitMqTopology.CreateConnectionFactory(configuration, "axiom-reference-data-publisher");
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = ReferenceDataTelemetry.Source.StartActivity("RabbitMQ publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", RabbitMqTopology.EVENTS_EXCHANGE);
        activity?.SetTag("messaging.message_id", message.Id);

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var channel = EnsureChannel();
                var properties = channel.CreateBasicProperties();
                properties.ContentType = "application/json";
                properties.DeliveryMode = 2;
                properties.MessageId = message.Id.ToString();
                properties.Type = message.EventType;
                properties.Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds());
                properties.Headers = new Dictionary<string, object>();
                if (Activity.Current?.Id is { } traceParent)
                {
                    properties.Headers["traceparent"] = traceParent;
                }

                channel.BasicPublish(
                    RabbitMqTopology.EVENTS_EXCHANGE,
                    message.EventType,
                    mandatory: false,
                    properties,
                    Encoding.UTF8.GetBytes(message.Payload));
                channel.WaitForConfirmsOrDie(ConfirmTimeout);
            }
            catch
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                ResetChannel();
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            ResetChannel();
        }
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        ResetChannel();
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        RabbitMqTopology.Declare(_channel);
        _channel.ConfirmSelect();
        return _channel;
    }

    private void ResetChannel()
    {
        _channel?.Dispose();
        _channel = null;
        _connection?.Dispose();
        _connection = null;
    }
}
