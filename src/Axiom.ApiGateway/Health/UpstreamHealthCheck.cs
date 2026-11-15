// 依据 YARP 集群配置探测各上游服务的就绪端点，网关自身无状态，上游故障时降级。
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Axiom.ApiGateway.Health;

public sealed class UpstreamHealthCheck(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var addresses = configuration.GetSection("ReverseProxy:Clusters")
            .GetChildren()
            .SelectMany(cluster => cluster.GetSection("Destinations").GetChildren())
            .Select(destination => destination["Address"])
            .Where(address => !string.IsNullOrWhiteSpace(address) && Uri.TryCreate(address, UriKind.Absolute, out _))
            .Select(address => address!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (addresses.Count == 0)
        {
            return HealthCheckResult.Healthy("未配置上游集群。");
        }

        var failures = new List<string>();
        foreach (var address in addresses)
        {
            var requestUri = $"{address.TrimEnd('/')}/health/ready";
            try
            {
                using var client = httpClientFactory.CreateClient();
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(Timeout);
                using var response = await client.GetAsync(requestUri, timeoutSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{requestUri} 返回 {(int)response.StatusCode}");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                failures.Add($"{requestUri} 不可达");
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("全部上游就绪。")
            : HealthCheckResult.Degraded($"部分上游未就绪：{string.Join("；", failures)}。");
    }
}
