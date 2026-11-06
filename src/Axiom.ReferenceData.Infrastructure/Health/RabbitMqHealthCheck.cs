// 通过轻量 TCP 探测判断 RabbitMQ 是否可达，避免每次健康检查都新建 AMQP 连接。
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Axiom.ReferenceData.Infrastructure.Health;

public sealed class RabbitMqHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var hostName = configuration["RabbitMq:HostName"] ?? "localhost";
        var port = configuration.GetValue("RabbitMq:Port", 5672);

        try
        {
            using var client = new TcpClient();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(Timeout);
            await client.ConnectAsync(hostName, port, timeoutSource.Token);
            return HealthCheckResult.Healthy("RabbitMQ 可达。");
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            // Broker 不可用时业务仍可读写（Outbox 会缓冲），因此降级而不是直接不健康。
            return HealthCheckResult.Degraded($"RabbitMQ 不可达：{hostName}:{port}。", exception);
        }
    }
}
