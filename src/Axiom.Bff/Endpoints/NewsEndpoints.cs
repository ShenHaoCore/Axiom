// 提供新闻 CRUD 的 BFF 门面，原样透传下游状态码、错误体与 ETag。
using System.Net;
using Axiom.Bff.Clients;
using Axiom.Bff.Contracts;

namespace Axiom.Bff.Endpoints;

public static class NewsEndpoints
{
    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff/news").RequireAuthorization();

        group.MapGet("/", async (
                int? page,
                int? pageSize,
                string? status,
                NewsGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var normalizedPage = page is > 0 ? page.Value : 1;
            var normalizedPageSize = pageSize is > 0 and <= 100 ? pageSize.Value : 20;
            var response = await client.ListAsync(normalizedPage, normalizedPageSize, status, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        });

        group.MapGet("/{id:guid}", async (
                Guid id,
                NewsGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var response = await client.GetAsync(id, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        });

        group.MapPost("/", async (
                NewsInput? input,
                NewsGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
            }

            var response = await client.CreateAsync(input, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: response.StatusCode == HttpStatusCode.Created);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();

        group.MapPut("/{id:guid}", async (
                Guid id,
                NewsInput? input,
                HttpContext httpContext,
                NewsGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
            }

            var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
            var response = await client.UpdateAsync(
                id,
                string.IsNullOrWhiteSpace(ifMatch) ? null : ifMatch,
                input,
                cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                NewsGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
            var response = await client.DeleteAsync(
                id,
                string.IsNullOrWhiteSpace(ifMatch) ? null : ifMatch,
                cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();
    }
}
