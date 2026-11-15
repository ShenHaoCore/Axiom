// 新闻文章 REST 端点（HttpApi 层——瘦绑定）。
// 只做 HTTP 参数绑定、权限检查、ETag 头解析、调用 AppService、ProblemDetails 映射。
// 响应消息按请求语言本地化；请求语言由 RequestLocalization 中间件协商（?culture= / Cookie / Accept-Language）。
using System.Globalization;
using Axiom.News.Application.Dtos;
using Axiom.News.Application.Services;
using Axiom.News.Domain.Exceptions;
using Axiom.News.HttpApi;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;
using Axiom.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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

        // 翻译子资源：整语言读取 / 全量替换 / 清空，不改动主表 CRUD 的 ETag 语义。
        group.MapGet("/{id:guid}/translations/{culture}", HandleGetTranslations)
            .RequireAuthorization(READ_POLICY)
            .Produces<TranslationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/translations/{culture}", HandleSaveTranslations)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<TranslationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/translations/{culture}", HandleDeleteTranslations)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleList(
        [AsParameters] NewsArticleListQuery query,
        INewsArticleAppService app,
        CancellationToken ct)
    {
        var paged = await app.ListAsync(query, CultureInfo.CurrentUICulture.Name, ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        INewsArticleAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        HttpContext ctx,
        CancellationToken ct)
    {
        var dto = await app.GetAsync(id, CultureInfo.CurrentUICulture.Name, ct);
        if (dto is null)
        {
            return NotFound(localizer, id);
        }
        ctx.Response.Headers.ETag = $"\"{dto.ConcurrencyToken:N}\"";
        return Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        CreateNewsArticleInput? input,
        INewsArticleAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        var errors = NewsArticleInputValidator.ValidateCreate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors.ToMessages(localizer));
        }

        var (dto, etag) = await app.CreateAsync(input, ct);
        ctx.Response.Headers.ETag = $"\"{etag:N}\"";
        return Results.Created($"/api/news/articles/{dto.Id}", dto);
    }

    private static async Task<IResult> HandleUpdate(
        Guid id,
        UpdateNewsArticleInput? input,
        INewsArticleAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        var errors = NewsArticleInputValidator.ValidateUpdate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors.ToMessages(localizer));
        }

        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), localizer, out var expectedETag, out var error))
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
            return NotFound(localizer, id);
        }
        catch (NewsArticleConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.NEWS_CONCURRENCY_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleDelete(
        Guid id,
        INewsArticleAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), localizer, out var expectedETag, out var error))
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
            return NotFound(localizer, id);
        }
        catch (NewsArticleConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.NEWS_CONCURRENCY_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleGetTranslations(
        Guid id,
        string culture,
        INewsArticleAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        var translations = await app.GetTranslationsAsync(id, normalized, ct);
        return translations is null
            ? NotFound(localizer, id)
            : Results.Ok(translations);
    }

    private static async Task<IResult> HandleSaveTranslations(
        Guid id,
        string culture,
        SaveTranslationsInput? input,
        INewsArticleAppService app,
        AxiomLocalizationOptions localization,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (input?.Values is null)
        {
            return BodyRequired(localizer);
        }

        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        if (!localization.IsSupported(normalized))
        {
            return UnsupportedCulture(localizer, culture);
        }

        var translations = await app.SaveTranslationsAsync(id, normalized, input, ct);
        return translations is null
            ? NotFound(localizer, id)
            : Results.Ok(translations);
    }

    private static async Task<IResult> HandleDeleteTranslations(
        Guid id,
        string culture,
        INewsArticleAppService app,
        AxiomLocalizationOptions localization,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        if (!localization.IsSupported(normalized))
        {
            return UnsupportedCulture(localizer, culture);
        }

        try
        {
            await app.DeleteTranslationsAsync(id, normalized, ct);
            return Results.NoContent();
        }
        catch (NewsArticleNotFoundException)
        {
            return NotFound(localizer, id);
        }
    }

    private static IResult BodyRequired(IStringLocalizer<ApiErrorMessages> localizer) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.REQUEST_BODY_REQUIRED].Value);

    private static IResult InvalidCulture(IStringLocalizer<ApiErrorMessages> localizer, string culture) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.LOCALIZATION_INVALID_CULTURE, culture].Value);

    private static IResult UnsupportedCulture(IStringLocalizer<ApiErrorMessages> localizer, string culture) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.LOCALIZATION_UNSUPPORTED_CULTURE, culture].Value);

    private static IResult NotFound(IStringLocalizer<ApiErrorMessages> localizer, Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.NEWS_NOT_FOUND, id].Value);

    private static bool TryParseIfMatch(
        string etag,
        IStringLocalizer<ApiErrorMessages> localizer,
        out Guid expectedETag,
        out IResult? error)
    {
        expectedETag = default;
        error = null;

        if (string.IsNullOrWhiteSpace(etag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: localizer[ApiErrorCodes.REQUEST_MISSING_IF_MATCH].Value);
            return false;
        }

        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' ||
            !Guid.TryParseExact(etag[1..^1], "N", out expectedETag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.REQUEST_INVALID_IF_MATCH].Value);
            return false;
        }

        return true;
    }
}
