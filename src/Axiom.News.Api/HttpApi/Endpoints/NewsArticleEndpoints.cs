// 新闻文章 REST 端点（HttpApi 层——瘦绑定）。
// 只做 HTTP 参数绑定、权限检查、ETag 头解析、调用 AppService、ProblemDetails 映射。
using Axiom.News.Application.Dtos;
using Axiom.News.Application.Services;
using Axiom.News.Domain.Exceptions;
using Axiom.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.News.HttpApi.Endpoints;

public static class NewsArticleEndpoints
{
    public const string READ_POLICY = "NewsRead";
    public const string MANAGE_POLICY = "NewsManage";

    public static void MapNewsArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/news/articles").WithTags("News");

        group.MapGet("/", HandleList)
            .RequireAuthorization(READ_POLICY)
            .Produces<PagedResponse<NewsArticleDto>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", HandleGet)
            .RequireAuthorization(READ_POLICY)
            .Produces<NewsArticleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", HandleCreate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<NewsArticleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", HandleUpdate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<NewsArticleDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", HandleDelete)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    private static async Task<IResult> HandleList(
        [AsParameters] NewsArticleListQuery query,
        INewsArticleAppService app,
        CancellationToken ct)
    {
        var paged = await app.ListAsync(query, ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        INewsArticleAppService app,
        HttpContext ctx,
        CancellationToken ct)
    {
        var dto = await app.GetAsync(id, ct);
        if (dto is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的新闻不存在。");
        }
        ctx.Response.Headers.ETag = $"\"{dto.ConcurrencyToken:N}\"";
        return Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        CreateNewsArticleInput? input,
        INewsArticleAppService app,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (input is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = NewsArticleInputValidator.ValidateCreate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var (dto, etag) = await app.CreateAsync(input, ct);
        ctx.Response.Headers.ETag = $"\"{etag:N}\"";
        return Results.Created($"/api/news/articles/{dto.Id}", dto);
    }

    private static async Task<IResult> HandleUpdate(
        Guid id,
        UpdateNewsArticleInput? input,
        INewsArticleAppService app,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (input is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = NewsArticleInputValidator.ValidateUpdate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), out var expectedETag, out var error))
        {
            return error!;
        }

        try
        {
            var (dto, etag) = await app.UpdateAsync(id, input, expectedETag, ct);
            ctx.Response.Headers.ETag = $"\"{etag:N}\"";
            return Results.Ok(dto);
        }
        catch (NewsArticleNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的新闻不存在。");
        }
        catch (NewsArticleConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "新闻已被其他请求修改，请重新获取后再操作。");
        }
    }

    private static async Task<IResult> HandleDelete(
        Guid id,
        INewsArticleAppService app,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), out var expectedETag, out var error))
        {
            return error!;
        }

        try
        {
            await app.DeleteAsync(id, expectedETag, ct);
            return Results.NoContent();
        }
        catch (NewsArticleNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的新闻不存在。");
        }
        catch (NewsArticleConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "新闻已被其他请求修改，请重新获取后再操作。");
        }
    }

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
