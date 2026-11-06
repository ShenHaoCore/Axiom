// 行政区层级只读查询 + 管理员修正端点。
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Models;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api.Endpoints;

public static class RegionEndpoints
{
    public const string READ_POLICY = ReferenceLocationEndpoints.READ_POLICY;
    public const string MANAGE_POLICY = ReferenceLocationEndpoints.MANAGE_POLICY;

    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static void MapRegionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/regions")
            .WithTags("ReferenceData")
            .RequireAuthorization(READ_POLICY);

        group.MapGet("/", ListAsync)
            .Produces<PagedResponse<RegionResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/tree", GetTreeAsync)
            .Produces<RegionTreeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<RegionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 管理员修正端点（RegionManage）。
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        int? level,
        string? countryCode,
        string? admin1Code,
        string? q,
        int page = 1,
        int pageSize = DefaultPageSize,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        IQueryable<Region> query = dbContext.Regions.AsNoTracking();
        if (level is { } l) query = query.Where(r => (int)r.Level == l);
        if (!string.IsNullOrWhiteSpace(countryCode)) query = query.Where(r => r.CountryCode == countryCode.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(admin1Code)) query = query.Where(r => r.Admin1Code == admin1Code);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{kw}%")
                                     || (r.AsciiName != null && EF.Functions.Like(r.AsciiName, $"%{kw}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.CountryCode)
            .ThenBy(r => (int)r.Level)
            .ThenBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ToResponse())
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<RegionResponse>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetTreeAsync(
        string countryCode,
        int depth = 2,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "countryCode 是必需查询参数。");
        }

        depth = Math.Clamp(depth, 1, 3);
        var cc = countryCode.ToUpperInvariant();

        var regions = await dbContext.Regions.AsNoTracking()
            .Where(r => r.CountryCode == cc && (int)r.Level <= depth)
            .ToListAsync(cancellationToken);

        var country = regions.FirstOrDefault(r => r.Level == RegionLevel.Country && r.CountryCode == cc);
        if (country is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"国家 {cc} 不存在。");
        }

        // 构建层级：Country → Admin1 (children) → City (children)
        var byParent = regions
            .Where(r => r.ParentId.HasValue)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var countryNode = BuildTree(country, byParent);

        return Results.Ok(countryNode);
    }

    private static RegionTreeResponse BuildTree(Region node, Dictionary<Guid, List<Region>> byParent)
    {
        if (!byParent.TryGetValue(node.Id, out var children))
        {
            return node.ToTreeNode(children: new List<RegionTreeResponse>());
        }

        var childNodes = children
            .OrderBy(r => r.Code)
            .Select(child => BuildTree(child, byParent))
            .ToList();

        return node.ToTreeNode(children: childNodes);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        var region = await dbContext.Regions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return region is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。")
            : Results.Ok(region.ToResponse());
    }

    private static async Task<IResult> CreateAsync(
        RegionRequest? request,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CountryCode) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "countryCode 与 name 是必需字段。");
        }

        if (!Enum.TryParse<RegionLevel>(request.Level, ignoreCase: true, out var level))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "level 必须是 Country/Admin1/City。");
        }

        var countryCode = request.CountryCode.ToUpperInvariant();
        var code = request.Code?.Trim().ToUpperInvariant() ?? request.Name.Trim();

        // 唯一键冲突检查
        var exists = await dbContext.Regions.AnyAsync(
            r => r.CountryCode == countryCode && r.Level == level && r.Code == code,
            cancellationToken);
        if (exists)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "相同国家+层级+代码的行政区已存在。");
        }

        var region = new Region
        {
            Id = Guid.NewGuid(),
            Level = level,
            CountryCode = countryCode,
            Admin1Code = string.IsNullOrWhiteSpace(request.Admin1Code) ? null : request.Admin1Code.Trim(),
            Code = code,
            Name = request.Name.Trim(),
            AsciiName = string.IsNullOrWhiteSpace(request.AsciiName) ? null : request.AsciiName.Trim(),
            LocalName = string.IsNullOrWhiteSpace(request.LocalName) ? null : request.LocalName.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Population = request.Population,
            GeoNameId = null
        };

        if (level != RegionLevel.Country && request.ParentId.HasValue)
        {
            region.ParentId = request.ParentId;
        }

        dbContext.Regions.Add(region);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/regions/{region.Id}", region.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        RegionRequest? request,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var region = await dbContext.Regions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (region is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。");
        }

        if (!string.IsNullOrWhiteSpace(request.Name)) region.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.AsciiName)) region.AsciiName = request.AsciiName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LocalName)) region.LocalName = request.LocalName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Code)) region.Code = request.Code.Trim().ToUpperInvariant();
        if (request.Latitude.HasValue) region.Latitude = request.Latitude;
        if (request.Longitude.HasValue) region.Longitude = request.Longitude;
        if (request.Population.HasValue) region.Population = request.Population;
        if (request.ParentId.HasValue) region.ParentId = request.ParentId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(region.ToResponse());
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ReferenceDataDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        var region = await dbContext.Regions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (region is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。");
        }

        var hasChildren = await dbContext.Regions.AnyAsync(r => r.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "存在下级行政区，无法删除。");
        }

        dbContext.Regions.Remove(region);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static RegionResponse ToResponse(this Region region) => new(
        region.Id,
        region.ParentId,
        (int)region.Level,
        region.CountryCode,
        region.Admin1Code,
        region.Code,
        region.Name,
        region.AsciiName,
        region.LocalName,
        region.Latitude,
        region.Longitude,
        region.Population,
        region.GeoNameId);

    private static RegionTreeResponse ToTreeNode(this Region region, List<RegionTreeResponse> children) => new(
        region.Id,
        (int)region.Level,
        region.CountryCode,
        region.Admin1Code,
        region.Code,
        region.Name,
        region.Population,
        children);
}

public sealed record RegionRequest(
    string? Level,
    string? CountryCode,
    string? Admin1Code,
    string? Code,
    string? Name,
    string? AsciiName,
    string? LocalName,
    double? Latitude,
    double? Longitude,
    long? Population,
    Guid? ParentId);
