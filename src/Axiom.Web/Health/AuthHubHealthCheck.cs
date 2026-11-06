// 通过 AuthHub 的就绪端点判断认证后端是否可用，认证后端不可用时登录流程必然失败。
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Axiom.Web.Health;

public sealed class AuthHubHealthCheck(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var baseAddress = configuration["Oidc:BackchannelAuthority"]
            ?? configuration["Oidc:Authority"]
            ?? "http://localhost:5101";
        var requestUri = $"{baseAddress.TrimEnd('/')}/health/ready";

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(Timeout);
            using var response = await client.GetAsync(requestUri, timeoutSource.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("AuthHub 可达。")
                : HealthCheckResult.Unhealthy($"AuthHub 返回 {(int)response.StatusCode}。");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"AuthHub 不可达：{requestUri}。", exception);
        }
    }
}
