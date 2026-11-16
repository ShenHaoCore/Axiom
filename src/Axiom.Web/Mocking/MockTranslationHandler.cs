// 翻译子资源（{kind}/{id}/translations/{culture}）共用处理：四类资源同构的 GET/PUT/DELETE 读写。
using System.Net;
using System.Text.Json;
using Axiom.Web.Models;
using static Axiom.Web.Mocking.MockHttp;

namespace Axiom.Web.Mocking;

internal static class MockTranslationHandler
{
    // 返回 null 表示方法不受支持，由入口回落真实网关（与原实现 base.SendAsync 一致）。
    internal static async Task<HttpResponseMessage?> HandleAsync(
        HttpRequestMessage request, string key, string culture, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            lock (MockDataStore.Gate)
            {
                return MockDataStore.Translations.TryGetValue(key, out var values)
                    ? Json(new TranslationView(culture, values), HttpStatusCode.OK)
                    : Empty(HttpStatusCode.NotFound);
            }
        }

        if (request.Method == HttpMethod.Put)
        {
            using var body = await ReadJsonAsync(request, cancellationToken);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (body.RootElement.TryGetProperty("values", out var element) && element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        values[property.Name] = property.Value.GetString() ?? string.Empty;
                    }
                }
            }

            lock (MockDataStore.Gate)
            {
                MockDataStore.Translations[key] = values;
            }

            return Empty(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Delete)
        {
            lock (MockDataStore.Gate)
            {
                MockDataStore.Translations.Remove(key);
            }

            return Empty(HttpStatusCode.NoContent);
        }

        return null;
    }
}
