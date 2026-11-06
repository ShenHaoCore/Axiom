// 提供行政区层级查询的 BFF 门面。
using System.Net;
using Axiom.Bff.Clients;

namespace Axiom.Bff.Endpoints;

public static class RegionEndpoints
{
    public static void MapRegionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff/regions").RequireAuthorization();

        group.MapGet("/", async (
                int? page,
                int? pageSize,
                int? level,
                string? countryCode,
                string? admin1Code,
                string? q,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var normalizedPage = page is > 0 ? page.Value : 1;
            var normalizedPageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;
            var response = await client.ListAsync(
                normalizedPage, normalizedPageSize, level, countryCode, admin1Code, q, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        });

        group.MapGet("/tree", async (
                string countryCode,
                int? depth,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var normalizedDepth = depth is >= 1 and <= 3 ? depth.Value : 2;
            var response = await client.GetTreeAsync(countryCode, normalizedDepth, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        });

        group.MapGet("/{id:guid}", async (
                Guid id,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var response = await client.GetAsync(id, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        });

        // 管理员修正（RegionManage）。
        group.MapPost("/", async (
                System.Text.Json.JsonElement? input,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            if (!input.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
            }
            var response = await client.CreateAsync(input.Value, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: response.StatusCode == HttpStatusCode.Created);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();

        group.MapPut("/{id:guid}", async (
                Guid id,
                System.Text.Json.JsonElement? input,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            if (!input.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
            }
            var response = await client.UpdateAsync(id, input.Value, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                RegionGatewayClient client,
                CancellationToken cancellationToken) =>
        {
            var response = await client.DeleteAsync(id, cancellationToken);
            return new DownstreamResult(response, rewriteLocation: false);
        })
        .AddEndpointFilter<RequireAntiforgeryFilter>();
    }
}
