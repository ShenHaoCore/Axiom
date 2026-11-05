// 将外部机场列表 REST 查询转换为带 Deadline 的内部 gRPC 调用。
using Axiom.Contracts.ReferenceData;
using Axiom.Contracts.ReferenceData.V1;
using Axiom.SharedKernel.Models;
using Grpc.Core;

namespace Axiom.ApiGateway;

public static class ReferenceDataGrpcEndpoints
{
    public static void MapReferenceDataGrpcEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reference-data/airports", ListAirportsAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAirportsAsync(
        [AsParameters] LocationQueryParameters query,
        HttpContext httpContext,
        ReferenceDataQuery.ReferenceDataQueryClient client,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var metadata = new Metadata
        {
            { "Authorization", httpContext.Request.Headers.Authorization.ToString() }
        };
        var request = new ListAirportsRequest
        {
            Code = query.Code ?? string.Empty,
            Name = query.Name ?? string.Empty,
            Page = query.Page,
            PageSize = query.PageSize
        };

        try
        {
            var response = await client.ListAirportsAsync(
                request,
                metadata,
                DateTime.UtcNow.AddSeconds(3),
                cancellationToken);
            return Results.Ok(new PagedResponse<AirportResponse>(
                response.Airports.Select(AirportResponse.FromMessage).ToList(),
                response.TotalCount,
                response.Page,
                response.PageSize));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Status.Detail);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.PermissionDenied)
        {
            return Results.Forbid();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Unauthenticated)
        {
            return Results.Unauthorized();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.DeadlineExceeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "基础数据查询超时。");
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "基础数据服务暂不可用。");
        }
    }
}
