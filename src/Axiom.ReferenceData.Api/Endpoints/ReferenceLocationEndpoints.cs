// 提供机场、港口和火车站基础档案的 REST 端点。
using Axiom.Contracts.ReferenceData;
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Models;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api.Endpoints;

public static class ReferenceLocationEndpoints
{
    public const string READ_POLICY = "ReferenceDataRead";
    public const string MANAGE_POLICY = "ReferenceDataManage";

    public static void MapReferenceLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var routes = new[]
        {
            (Path: "/api/reference-data/airports", Type: ReferenceLocationType.Airport),
            (Path: "/api/reference-data/seaports", Type: ReferenceLocationType.Seaport),
            (Path: "/api/reference-data/rail-stations", Type: ReferenceLocationType.RailStation)
        };

        foreach (var (path, type) in routes)
        {
            var group = app.MapGroup(path).WithTags("ReferenceData");
            group.MapGet("/", ([AsParameters] LocationQueryParameters query, ReferenceDataDbContext dbContext,
                    CancellationToken cancellationToken) => ListAsync(type, query, dbContext, cancellationToken))
                .RequireAuthorization(READ_POLICY)
                .Produces<PagedResponse<ReferenceLocationResponse>>()
                .ProducesValidationProblem();
            group.MapGet("/{id:guid}", (Guid id, ReferenceDataDbContext dbContext, HttpContext httpContext,
                    CancellationToken cancellationToken) => GetAsync(type, id, dbContext, httpContext, cancellationToken))
                .RequireAuthorization(READ_POLICY)
                .Produces<ReferenceLocationResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPost("/", (UpsertReferenceLocationRequest? request, ReferenceDataDbContext dbContext,
                    HttpContext httpContext, CancellationToken cancellationToken) =>
                    CreateAsync(path, type, request, dbContext, httpContext, cancellationToken))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPut("/{id:guid}", (Guid id, UpsertReferenceLocationRequest? request,
                    ReferenceDataDbContext dbContext, HttpContext httpContext, CancellationToken cancellationToken) =>
                    UpdateAsync(type, id, request, dbContext, httpContext, cancellationToken))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationResponse>()
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status428PreconditionRequired);
            group.MapDelete("/{id:guid}", (Guid id, ReferenceDataDbContext dbContext, HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                    DeleteAsync(type, id, dbContext, httpContext, cancellationToken))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        }
    }

    private static async Task<IResult> ListAsync(
        ReferenceLocationType type,
        LocationQueryParameters query,
        ReferenceDataDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var locations = dbContext.ReferenceLocations.AsNoTracking()
            .Where(location => location.Type == type);
        if (query.NormalizedCode is { } code)
        {
            locations = locations.Where(location => location.Code == code);
        }

        if (query.NormalizedName is { } name)
        {
            locations = locations.Where(location => location.Name.Contains(name));
        }

        var totalCount = await locations.CountAsync(cancellationToken);
        var items = await locations
            .OrderBy(location => location.Code)
            .ThenBy(location => location.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var response = new PagedResponse<ReferenceLocationResponse>(
            items.Select(location => location.ToResponse()).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetAsync(
        ReferenceLocationType type,
        Guid id,
        ReferenceDataDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.ReferenceLocations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Type == type && item.Id == id, cancellationToken);
        if (location is null)
        {
            return LocationNotFound();
        }

        SetETag(httpContext, location.ConcurrencyToken);
        return Results.Ok(location.ToResponse());
    }

    private static async Task<IResult> CreateAsync(
        string path,
        ReferenceLocationType type,
        UpsertReferenceLocationRequest? request,
        ReferenceDataDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = ReferenceLocationInputValidator.Validate(type, request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var code = NormalizeCode(request.Code!);
        if (await CodeExistsAsync(type, code, null, dbContext, cancellationToken))
        {
            return CodeConflict();
        }

        var location = new ReferenceLocation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Code = code,
            Name = request.Name!.Trim(),
            CountryCode = NormalizeCode(request.CountryCode!),
            City = NormalizeCity(request.City),
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        dbContext.ReferenceLocations.Add(location);
        dbContext.OutboxMessages.Add(OutboxMessage.FromLocation(location, OutboxMessage.LOCATION_CREATED));
        await dbContext.SaveChangesAsync(cancellationToken);
        SetETag(httpContext, location.ConcurrencyToken);
        return Results.Created($"{path}/{location.Id}", location.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationRequest? request,
        ReferenceDataDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = ReferenceLocationInputValidator.Validate(type, request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var location = await dbContext.ReferenceLocations
            .SingleOrDefaultAsync(item => item.Type == type && item.Id == id, cancellationToken);
        if (location is null)
        {
            return LocationNotFound();
        }

        if (!TryGetExpectedVersion(httpContext, out var expectedVersion, out var preconditionError))
        {
            return preconditionError!;
        }

        var code = NormalizeCode(request.Code!);
        if (await CodeExistsAsync(type, code, id, dbContext, cancellationToken))
        {
            return CodeConflict();
        }

        dbContext.Entry(location).Property(item => item.ConcurrencyToken).OriginalValue = expectedVersion;
        location.ConcurrencyToken = Guid.NewGuid();
        location.Code = code;
        location.Name = request.Name!.Trim();
        location.CountryCode = NormalizeCode(request.CountryCode!);
        location.City = NormalizeCity(request.City);
        location.Latitude = request.Latitude;
        location.Longitude = request.Longitude;
        dbContext.OutboxMessages.Add(OutboxMessage.FromLocation(location, OutboxMessage.LOCATION_UPDATED));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        SetETag(httpContext, location.ConcurrencyToken);
        return Results.Ok(location.ToResponse());
    }

    private static async Task<IResult> DeleteAsync(
        ReferenceLocationType type,
        Guid id,
        ReferenceDataDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.ReferenceLocations
            .SingleOrDefaultAsync(item => item.Type == type && item.Id == id, cancellationToken);
        if (location is null)
        {
            return LocationNotFound();
        }

        if (!TryGetExpectedVersion(httpContext, out var expectedVersion, out var preconditionError))
        {
            return preconditionError!;
        }

        dbContext.Entry(location).Property(item => item.ConcurrencyToken).OriginalValue = expectedVersion;
        dbContext.OutboxMessages.Add(OutboxMessage.FromLocation(location, OutboxMessage.LOCATION_DELETED));
        dbContext.ReferenceLocations.Remove(location);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        return Results.NoContent();
    }

    private static bool TryGetExpectedVersion(
        HttpContext httpContext,
        out Guid expectedVersion,
        out IResult? error)
    {
        expectedVersion = default;
        error = null;
        var etag = httpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(etag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "缺少 If-Match 请求头。");
            return false;
        }

        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' ||
            !Guid.TryParseExact(etag[1..^1], "N", out expectedVersion))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "If-Match 必须使用当前资源返回的强 ETag。");
            return false;
        }

        return true;
    }

    private static void SetETag(HttpContext httpContext, Guid version) =>
        httpContext.Response.Headers.ETag = $"\"{version:N}\"";

    private static IResult LocationNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的地点不存在。");

    private static IResult CodeConflict() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "相同类型的地点代码已存在。");

    private static IResult ConcurrencyConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "地点已被其他请求修改，请重新获取后再操作。");

    private static Task<bool> CodeExistsAsync(
        ReferenceLocationType type,
        string code,
        Guid? excludedId,
        ReferenceDataDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.ReferenceLocations.AnyAsync(location =>
            location.Type == type && location.Code == code && location.Id != excludedId, cancellationToken);

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeCity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
