// 新闻 Mock：/api/news/articles 的列表、CRUD 与翻译读写，未知路径回落真实网关。
using System.Net;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using static Axiom.Web.Mocking.MockHttp;

namespace Axiom.Web.Mocking;

internal sealed class MockNewsHandler(MockLocalizer localizer)
{
    // 返回 null 表示该路径/方法不由 Mock 接管，由入口回落真实网关（与原实现 base.SendAsync 一致）。
    internal async Task<HttpResponseMessage?> HandleAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[NewsClient.ARTICLES_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = localizer.ResolveCulture(request);
                List<NewsArticleView> snapshot;
                lock (MockDataStore.Gate) { snapshot = [.. MockDataStore.Articles]; }
                return Json(Paged(snapshot.Select(article => localizer.Localize(article, requestCulture)).ToList()), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var now = DateTimeOffset.UtcNow;
                var status = Body(body, GatewayFields.STATUS) ?? "Draft";
                var created = new NewsArticleView(
                    Guid.NewGuid(),
                    Body(body, GatewayFields.TITLE) ?? "（无标题）",
                    Body(body, GatewayFields.SUMMARY),
                    Body(body, GatewayFields.CONTENT) ?? string.Empty,
                    Body(body, GatewayFields.CATEGORY) ?? string.Empty,
                    status,
                    Body(body, GatewayFields.AUTHOR) ?? string.Empty,
                    string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase) ? now : null,
                    now,
                    now);
                lock (MockDataStore.Gate) { MockDataStore.Articles.Add(created); }
                return Json(created, HttpStatusCode.Created);
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await MockTranslationHandler.HandleAsync(request, $"news/{id}/{culture}", culture, cancellationToken);
            }

            // PUT 请求体需在 lock 外读取，避免在 lock 语句体内 await。
            using var putBody = request.Method == HttpMethod.Put
                ? await ReadJsonAsync(request, cancellationToken)
                : null;

            lock (MockDataStore.Gate)
            {
                var index = MockDataStore.Articles.FindIndex(a => a.Id == id);
                if (request.Method == HttpMethod.Get)
                {
                    // 未知 id 返回 404：与真实网关一致，避免页面渲染出“看似存在的幽灵记录”。
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定新闻。");
                    }

                    var requestCulture = localizer.ResolveCulture(request);
                    return Json(localizer.Localize(MockDataStore.Articles[index], requestCulture), HttpStatusCode.OK, etag: $"mock-{id.ToString("N")[..8]}");
                }

                if (request.Method == HttpMethod.Put)
                {
                    var body = putBody!;
                    var original = index >= 0 ? MockDataStore.Articles[index] : SampleArticle(id);
                    var status = Body(body, GatewayFields.STATUS) ?? original.Status;
                    var updated = new NewsArticleView(
                        id,
                        Body(body, GatewayFields.TITLE) ?? original.Title,
                        Body(body, GatewayFields.SUMMARY),
                        Body(body, GatewayFields.CONTENT) ?? original.Content,
                        Body(body, GatewayFields.CATEGORY) ?? original.Category,
                        status,
                        Body(body, GatewayFields.AUTHOR) ?? original.Author,
                        string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
                            ? (original.PublishedAt ?? DateTimeOffset.UtcNow)
                            : original.PublishedAt,
                        original.CreatedAt,
                        DateTimeOffset.UtcNow);

                    if (index < 0)
                    {
                        MockDataStore.Articles.Add(updated);
                    }
                    else
                    {
                        MockDataStore.Articles[index] = updated;
                    }

                    return Empty(HttpStatusCode.NoContent);
                }

                if (request.Method == HttpMethod.Delete)
                {
                    if (index >= 0)
                    {
                        MockDataStore.Articles.RemoveAt(index);
                    }

                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        return null;
    }

    private static NewsArticleView SampleArticle(Guid id) => new(
        id,
        "示例新闻标题",
        "Mock 模式返回的示例摘要。",
        "这是开发 Mock 模式生成的示例正文，用于本地预览新闻详情与编辑页面。",
        "平台动态",
        "Draft",
        "开发用户",
        null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
