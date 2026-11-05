// 通过版本化 gRPC 契约向内部服务提供机场查询。
using Axiom.Contracts.ReferenceData;
using Axiom.Contracts.ReferenceData.V1;
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Models;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api;

public sealed class ReferenceDataQueryGrpcService(ReferenceDataDbContext dbContext)
    : ReferenceDataQuery.ReferenceDataQueryBase
{
    public override async Task<ListAirportsResponse> ListAirports(
        ListAirportsRequest request,
        ServerCallContext context)
    {
        var query = new LocationQueryParameters
        {
            Code = request.Code,
            Name = request.Name,
            Page = request.Page == 0 ? 1 : request.Page,
            PageSize = request.PageSize == 0 ? 20 : request.PageSize
        };
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            var detail = string.Join("; ", errors.SelectMany(error => error.Value));
            throw new RpcException(new Status(StatusCode.InvalidArgument, detail));
        }

        var locations = dbContext.ReferenceLocations.AsNoTracking()
            .Where(location => location.Type == ReferenceLocationType.Airport);
        if (query.NormalizedCode is { } code)
        {
            locations = locations.Where(location => location.Code == code);
        }

        if (query.NormalizedName is { } name)
        {
            locations = locations.Where(location => location.Name.Contains(name));
        }

        var totalCount = await locations.CountAsync(context.CancellationToken);
        var items = await locations
            .OrderBy(location => location.Code)
            .ThenBy(location => location.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(context.CancellationToken);
        var response = new ListAirportsResponse
        {
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
        foreach (var location in items)
        {
            response.Airports.Add(ToAirport(location));
        }

        return response;
    }

    private static Airport ToAirport(ReferenceLocation location)
    {
        var airport = new Airport
        {
            Id = location.Id.ToString(),
            Code = location.Code,
            Name = location.Name,
            CountryCode = location.CountryCode
        };
        if (location.City is not null)
        {
            airport.City = location.City;
        }

        if (location.Latitude is not null)
        {
            airport.Latitude = location.Latitude.Value;
        }

        if (location.Longitude is not null)
        {
            airport.Longitude = location.Longitude.Value;
        }

        return airport;
    }
}
