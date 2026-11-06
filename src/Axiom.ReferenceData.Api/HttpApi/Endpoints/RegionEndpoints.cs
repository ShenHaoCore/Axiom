// 行政区 REST 端点（HttpApi 层——瘦绑定）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.ReferenceData.HttpApi.Endpoints;

public static class RegionEndpoints
{
    public const string READ_POLICY = "ReferenceDataRead";
    public const string MANAGE_POLICY = "ReferenceDataManage";

    public static void MapRegionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/regions")
            .WithTags("ReferenceData")
            .RequireAuthorization(READ_POLICY);

        group.MapGet("/", HandleList)
            .Produces<PagedResponse<RegionDto>>();

        group.MapGet("/tree", HandleTree)
            .Produces<RegionTreeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", HandleGet)
            .Produces<RegionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", HandleCreate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", HandleUpdate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", HandleDelete)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleList(
        IRegionAppService app,
        int? level = null, string? countryCode = null, string? admin1Code = null, string? q = null,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        var paged = await app.ListAsync(new RegionListQuery(level, countryCode, admin1Code, q, page, pageSize), ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleTree(
        IRegionAppService app,
        string countryCode, int depth = 2,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "countryCode 是必需查询参数。");
        try
        {
            var tree = await app.GetTreeAsync(countryCode, depth, ct);
            return tree is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"国家 {countryCode} 不存在。")
                : Results.Ok(tree);
        }
        catch (RegionCountryNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"国家 {countryCode} 不存在。");
        }
    }

    private static async Task<IResult> HandleGet(Guid id, IRegionAppService app, CancellationToken ct = default)
    {
        var dto = await app.GetAsync(id, ct);
        return dto is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。")
            : Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        CreateRegionInput? input, IRegionAppService app, CancellationToken ct = default)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.CountryCode) || string.IsNullOrWhiteSpace(input.Name))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "countryCode 与 name 是必需字段。");
        if (string.IsNullOrWhiteSpace(input.Level))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "level 必须是 Country/Admin1/City。");
        try
        {
            var (dto, location) = await app.CreateAsync(input, ct);
            return Results.Created(location.ToString(), dto);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
        catch (RegionUniqueKeyConflictException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "相同国家+层级+代码的行政区已存在。");
        }
    }

    private static async Task<IResult> HandleUpdate(
        Guid id, UpdateRegionInput? input, IRegionAppService app, CancellationToken ct = default)
    {
        if (input is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        try
        {
            return Results.Ok(await app.UpdateAsync(id, input, ct));
        }
        catch (RegionNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。");
        }
    }

    private static async Task<IResult> HandleDelete(Guid id, IRegionAppService app, CancellationToken ct = default)
    {
        try
        {
            await app.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (RegionNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "行政区不存在。");
        }
        catch (RegionHasChildrenException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "存在下级行政区，无法删除。");
        }
    }
}
