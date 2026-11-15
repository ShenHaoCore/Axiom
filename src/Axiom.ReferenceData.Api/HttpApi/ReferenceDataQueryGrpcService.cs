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

public sealed class ReferenceDataQueryGrpcService(IReferenceLocationAppService app)
    : ReferenceDataQuery.ReferenceDataQueryBase
{
    public override async Task<ListAirportsResponse> ListAirports(
        ListAirportsRequest request,
        ServerCallContext context)
    {
        var pageSize = request.PageSize == 0 ? 20 : request.PageSize;
        var page = request.Page == 0 ? 1 : request.Page;

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
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
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
