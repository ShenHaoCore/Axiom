// 地点档案 REST 端点（HttpApi 层——瘦绑定）。
// 三个路由（airports / seaports / rail-stations）共享同一个业务实现。
using Axiom.Contracts.ReferenceData;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.ReferenceData.HttpApi.Endpoints;

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

            group.MapGet("/", ([AsParameters] LocationQueryParameters query,
                    IReferenceLocationAppService app, CancellationToken ct) =>
                    HandleList(type, query, app, ct))
                .RequireAuthorization(READ_POLICY)
                .Produces<PagedResponse<ReferenceLocationDto>>();

            group.MapGet("/{id:guid}", (Guid id, HttpContext ctx,
                    IReferenceLocationAppService app, CancellationToken ct) =>
                    HandleGet(type, id, ctx, app, ct))
                .RequireAuthorization(READ_POLICY)
                .Produces<ReferenceLocationDto>();

            group.MapPost("/", (UpsertReferenceLocationInput? input, HttpContext ctx,
                    IReferenceLocationAppService app, CancellationToken ct) =>
                    HandleCreate(type, path, input, ctx, app, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationDto>(StatusCodes.Status201Created);

            group.MapPut("/{id:guid}", (Guid id, UpsertReferenceLocationInput? input, HttpContext ctx,
                    IReferenceLocationAppService app, CancellationToken ct) =>
                    HandleUpdate(type, id, input, ctx, app, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationDto>();

            group.MapDelete("/{id:guid}", (Guid id, HttpContext ctx,
                    IReferenceLocationAppService app, CancellationToken ct) =>
                    HandleDelete(type, id, ctx, app, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces(StatusCodes.Status204NoContent);
        }
    }

    // ---------- 处理函数 ----------

    private static async Task<IResult> HandleList(
        ReferenceLocationType type,
        LocationQueryParameters query,
        IReferenceLocationAppService app,
        CancellationToken ct)
    {
        var errors = query.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var appQuery = new ReferenceLocationListQuery(
            query.NormalizedCode,
            query.NormalizedName,
            query.Page,
            query.PageSize);

        var paged = await app.ListAsync(type, appQuery, ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleGet(
        ReferenceLocationType type,
        Guid id,
        HttpContext ctx,
        IReferenceLocationAppService app,
        CancellationToken ct)
    {
        var (dto, etag) = await app.GetAsync(type, id, ct);
        if (dto is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的地点不存在。");
        }
        if (etag.HasValue) ctx.Response.Headers.ETag = $"\"{etag.Value:N}\"";
        return Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        ReferenceLocationType type,
        string path,
        UpsertReferenceLocationInput? input,
        HttpContext ctx,
        IReferenceLocationAppService app,
        CancellationToken ct)
    {
        if (input is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = ReferenceLocationInputValidator.Validate(type, input, isCreate: true);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        try
        {
            var (dto, etag, location) = await app.CreateAsync(type, input, ct);
            ctx.Response.Headers.ETag = $"\"{etag:N}\"";
            return Results.Created($"{location}", dto);
        }
        catch (ReferenceLocationCodeConflictException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "相同类型的地点代码已存在。");
        }
    }

    private static async Task<IResult> HandleUpdate(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationInput? input,
        HttpContext ctx,
        IReferenceLocationAppService app,
        CancellationToken ct)
    {
        if (input is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = ReferenceLocationInputValidator.Validate(type, input, isCreate: false);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), out var expectedETag, out var error))
            return error!;

        try
        {
            var (dto, etag) = await app.UpdateAsync(type, id, input, expectedETag, ct);
            ctx.Response.Headers.ETag = $"\"{etag:N}\"";
            return Results.Ok(dto);
        }
        catch (ReferenceLocationNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的地点不存在。");
        }
        catch (ReferenceLocationCodeConflictException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "相同类型的地点代码已存在。");
        }
        catch (ReferenceLocationConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "地点已被其他请求修改，请重新获取后再操作。");
        }
    }

    private static async Task<IResult> HandleDelete(
        ReferenceLocationType type,
        Guid id,
        HttpContext ctx,
        IReferenceLocationAppService app,
        CancellationToken ct)
    {
        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), out var expectedETag, out var error))
            return error!;

        try
        {
            await app.DeleteAsync(type, id, expectedETag, ct);
            return Results.NoContent();
        }
        catch (ReferenceLocationNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的地点不存在。");
        }
        catch (ReferenceLocationConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "地点已被其他请求修改，请重新获取后再操作。");
        }
    }

    // ---------- 私有：ETag 解析 ----------

    private static bool TryParseIfMatch(string etag, out Guid expectedETag, out IResult? error)
    {
        expectedETag = default;
        error = null;
        if (string.IsNullOrWhiteSpace(etag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "缺少 If-Match 请求头。");
            return false;
        }
        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' ||
            !Guid.TryParseExact(etag[1..^1], "N", out expectedETag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "If-Match 必须使用当前资源返回的强 ETag。");
            return false;
        }
        return true;
    }
}
