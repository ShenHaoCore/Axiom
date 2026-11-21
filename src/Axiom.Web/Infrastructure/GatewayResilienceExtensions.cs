// 网关类型化客户端的统一默认值：短超时 + 有界韧性策略，集中一处避免四个客户端配置漂移。
using System.Net;
using Microsoft.Extensions.Http.Resilience;

namespace Axiom.Web.Infrastructure;

public static class GatewayResilienceExtensions
{
    public static IHttpClientBuilder AddAxiomGatewayDefaults<TClient>(
        this IServiceCollection services,
        Uri baseAddress)
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>(client =>
        {
            client.BaseAddress = baseAddress;
            // 网关悬挂时 10 秒内失败（HttpClient 默认 100 秒会让用户整页卡死）。
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            options.Retry.MaxRetryAttempts = 2;
            // 仅对幂等安全的情况重试：连接类异常与 408/429/502/503/504。
            // 标准管道默认无 hedging（并发竞速），写操作不会重复提交。
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException
                || args.Outcome.Result?.StatusCode is HttpStatusCode.RequestTimeout
                    or HttpStatusCode.TooManyRequests
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout);
        });

        // 返回原始 IHttpClientBuilder，调用方仍可追加自定义 MessageHandler（如 MockGatewayHandler）。
        return builder;
    }
}
