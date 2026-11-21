// gRPC 查询端点（HttpApi 层）。
// 走 Application 层的 IReferenceLocationAppService，不直接操作 DbContext。
using System.Globalization;
using Axiom.Contracts.ReferenceData;
using Axiom.Contracts.ReferenceData.V1;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Entities;
using Grpc.Core;

namespace Axiom.ReferenceData.HttpApi;

public sealed class ReferenceDataQueryGrpcService(
    IReferenceLocationAppService app,
    ILogger<ReferenceDataQueryGrpcService> logger)
    : ReferenceDataQuery.ReferenceDataQueryBase
{
    /// <summary>gRPC 分页上限：与 REST 侧查询参数边界对齐，防止无界查询拖垮内存与带宽。</summary>
    private const int MAX_PAGE_SIZE = 100;

    public override async Task<ListAirportsResponse> ListAirports(
        ListAirportsRequest request,
        ServerCallContext context)
    {
        // 边界钳制：page_size 上限 100，非法 page 兜底为 1（异常输入不值得 500）。
        var pageSize = Math.Clamp(request.PageSize == 0 ? 20 : request.PageSize, 1, MAX_PAGE_SIZE);
        var page = Math.Max(request.Page == 0 ? 1 : request.Page, 1);

        var query = new ReferenceLocationListQuery(
            request.Code,
            request.Name,
            page,
            pageSize);

        try
        {
            // gRPC 请求头同样经过 RequestLocalization，按调用方声明的语言合并译文。
            var culture = CultureInfo.CurrentUICulture.Name;
            var paged = await app.ListAsync(ReferenceLocationType.Airport, query, culture, context.CancellationToken);
            var response = new ListAirportsResponse
            {
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize
            };

            foreach (var location in paged.Items)
            {
                response.Airports.Add(ToAirport(location));
            }

            return response;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // 客户端断开：映射为 Cancelled，避免污染 Internal 错误监控。
            throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 与 REST 侧 GlobalExceptionHandler 口径一致：记完整日志、回传通用文案，不泄露内部异常消息。
            logger.LogError(exception, "gRPC ListAirports 处理失败（TraceId={TraceId}）。", context.GetHttpContext().TraceIdentifier);
            throw new RpcException(new Status(
                StatusCode.Internal,
                $"Internal error (traceId: {context.GetHttpContext().TraceIdentifier})."));
        }
    }

    private static Airport ToAirport(ReferenceLocationDto location)
    {
        var airport = new Airport
        {
            Id = location.Id.ToString(),
            Code = location.Code,
            Name = location.Name,
            CountryCode = location.CountryCode
        };
        if (location.City is not null) airport.City = location.City;
        if (location.Latitude is not null) airport.Latitude = location.Latitude.Value;
        if (location.Longitude is not null) airport.Longitude = location.Longitude.Value;
        return airport;
    }
}
