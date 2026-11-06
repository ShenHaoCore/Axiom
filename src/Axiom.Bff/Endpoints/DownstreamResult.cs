// 将下游响应原样写回客户端，仅重写创建成功时的 Location 指向 BFF 路径。
namespace Axiom.Bff.Endpoints;

internal sealed class DownstreamResult(HttpResponseMessage response, bool rewriteLocation) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = (int)response.StatusCode;

        if (response.Content.Headers.ContentType?.ToString() is { } contentType)
        {
            httpContext.Response.ContentType = contentType;
        }

        if (response.Headers.ETag?.ToString() is { } etag)
        {
            httpContext.Response.Headers.ETag = etag;
        }

        if (response.Headers.Location?.ToString() is { } location)
        {
            httpContext.Response.Headers.Location = rewriteLocation ? RewriteLocation(location) : location;
        }

        await response.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
        response.Dispose();
    }

    private static string RewriteLocation(string location)
    {
        var id = location.TrimEnd('/').Split('/').LastOrDefault();
        return id is null ? location : $"/bff/news/{id}";
    }
}
