// 声明持久化交换机、消费队列及对应的死信拓扑。
using RabbitMQ.Client;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

internal static class RabbitMqTopology
{
    public const string EVENTS_EXCHANGE = "axiom.referencedata.events";
    public const string LOCATION_QUEUE = "axiom.referencedata.location-events";
    public const string DEAD_LETTER_EXCHANGE = "axiom.referencedata.dead-letter";
    public const string DEAD_LETTER_QUEUE = "axiom.referencedata.location-events.dead-letter";

    public static ConnectionFactory CreateConnectionFactory(IConfiguration configuration, string clientName)
    {
        var password = configuration["RabbitMq:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("缺少 RabbitMq:Password 配置。");
        }

        return new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"] ?? "localhost",
            Port = configuration.GetValue("RabbitMq:Port", 5672),
            VirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/",
            UserName = configuration["RabbitMq:UserName"] ?? "axiom",
            Password = password,
            ClientProvidedName = clientName,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
    }

    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(EVENTS_EXCHANGE, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(DEAD_LETTER_EXCHANGE, ExchangeType.Fanout, durable: true);
        channel.QueueDeclare(
            DEAD_LETTER_QUEUE,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(DEAD_LETTER_QUEUE, DEAD_LETTER_EXCHANGE, routingKey: string.Empty);
        channel.QueueDeclare(
            LOCATION_QUEUE,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = DEAD_LETTER_EXCHANGE
            });
        channel.QueueBind(LOCATION_QUEUE, EVENTS_EXCHANGE, routingKey: "referencedata.location.#");
    }
}
