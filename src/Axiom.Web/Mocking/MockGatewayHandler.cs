// 仅开发 Mock 模式启用：拦截常用网关请求并按路径前缀分发到各资源 Mock 处理器，
// 数据由 MockDataStore 进程内共享维护，使机场、新闻、行政区增删改与翻译读写在 Mock 下形成完整交互闭环。
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;

namespace Axiom.Web.Mocking;

public sealed class MockGatewayHandler : DelegatingHandler
{
    // 与真实网关一致的语言协商规则，供各资源处理器列表/详情套用已保存的译文。
    private readonly MockEnumHandler enums;
    private readonly MockAirportHandler airports;
    private readonly MockNewsHandler news;
    private readonly MockGeoDivisionHandler geoDivisions;

    public MockGatewayHandler(AxiomLocalizationOptions localization)
    {
        var localizer = new MockLocalizer(localization);
        enums = new MockEnumHandler(localizer);
        airports = new MockAirportHandler(localizer);
        news = new MockNewsHandler(localizer);
        geoDivisions = new MockGeoDivisionHandler(localizer);
    }

    // 路径与各网关客户端共享同一常量，避免双份维护。
    private static readonly string AIRPORTS_PATH = ReferenceDataClient.AIRPORTS_PATH;
    private static readonly string ENUMS_PATH = ReferenceEnumClient.ENUMS_PATH;
    private static readonly string ARTICLES_PATH = NewsClient.ARTICLES_PATH;
    private static readonly string GEO_DIVISIONS_PATH = GeoDivisionClient.GEO_DIVISIONS_PATH;

    // 处理器返回 null 表示该请求不由 Mock 接管（如翻译子资源的不支持方法），回落到真实网关。
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.TrimEnd('/');
        if (path is not null)
        {
            if (path.StartsWith(ENUMS_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await enums.HandleAsync(request, path, cancellationToken)
                    ?? await base.SendAsync(request, cancellationToken);
            }

            if (path.StartsWith(AIRPORTS_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await airports.HandleAsync(request, path, cancellationToken)
                    ?? await base.SendAsync(request, cancellationToken);
            }

            if (path.StartsWith(ARTICLES_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await news.HandleAsync(request, path, cancellationToken)
                    ?? await base.SendAsync(request, cancellationToken);
            }

            if (path.StartsWith(GEO_DIVISIONS_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await geoDivisions.HandleAsync(request, path, cancellationToken)
                    ?? await base.SendAsync(request, cancellationToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
