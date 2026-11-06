// 地点档案 REST 端点（HttpApi 层——瘦绑定）。
// 三个路由（airports / seaports / rail-stations）共享同一个业务实现。
// 响应消息按请求语言本地化；请求语言由 RequestLocalization 中间件协商。
using System.Globalization;
using Axiom.Contracts.ReferenceData;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;
using Axiom.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleList(type, query, app, localizer, ct))
                .RequireAuthorization(READ_POLICY)
                .Produces<PagedResponse<ReferenceLocationDto>>();

            group.MapGet("/{id:guid}", (Guid id, HttpContext ctx,
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleGet(type, id, ctx, app, localizer, ct))
                .RequireAuthorization(READ_POLICY)
                .Produces<ReferenceLocationDto>();

            group.MapPost("/", (UpsertReferenceLocationInput? input, HttpContext ctx,
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleCreate(type, input, ctx, app, localizer, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationDto>(StatusCodes.Status201Created);

            group.MapPut("/{id:guid}", (Guid id, UpsertReferenceLocationInput? input, HttpContext ctx,
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleUpdate(type, id, input, ctx, app, localizer, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<ReferenceLocationDto>();

            group.MapDelete("/{id:guid}", (Guid id, HttpContext ctx,
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleDelete(type, id, ctx, app, localizer, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces(StatusCodes.Status204NoContent);

            // 翻译子资源：整语言读取 / 全量替换 / 清空。
            group.MapGet("/{id:guid}/translations/{culture}", (Guid id, string culture,
                    IReferenceLocationAppService app,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleGetTranslations(type, id, culture, app, localizer, ct))
                .RequireAuthorization(READ_POLICY)
                .Produces<TranslationDto>();

            group.MapPut("/{id:guid}/translations/{culture}", (Guid id, string culture,
                    SaveTranslationsInput? input, IReferenceLocationAppService app,
                    AxiomLocalizationOptions localization,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleSaveTranslations(type, id, culture, input, app, localization, localizer, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces<TranslationDto>();

            group.MapDelete("/{id:guid}/translations/{culture}", (Guid id, string culture,
                    IReferenceLocationAppService app,
                    AxiomLocalizationOptions localization,
                    IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct) =>
                    HandleDeleteTranslations(type, id, culture, app, localization, localizer, ct))
                .RequireAuthorization(MANAGE_POLICY)
                .Produces(StatusCodes.Status204NoContent);
        }
    }

    // ---------- 处理函数 ----------

    private static async Task<IResult> HandleList(
        ReferenceLocationType type,
        LocationQueryParameters query,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var errors = query.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors.ToMessages(localizer));

        var appQuery = new ReferenceLocationListQuery(
            query.NormalizedCode,
            query.NormalizedName,
            query.Page,
            query.PageSize);

        var paged = await app.ListAsync(type, appQuery, CultureInfo.CurrentUICulture.Name, ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleGet(
        ReferenceLocationType type,
        Guid id,
        HttpContext ctx,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var (dto, etag) = await app.GetAsync(type, id, CultureInfo.CurrentUICulture.Name, ct);
        if (dto is null)
        {
            return LocationNotFound(localizer, id);
        }
        if (etag.HasValue) ctx.Response.Headers.ETag = $"\"{etag.Value:N}\"";
        return Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        ReferenceLocationType type,
        UpsertReferenceLocationInput? input,
        HttpContext ctx,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        var errors = ReferenceLocationInputValidator.Validate(type, input, isCreate: true);
        if (errors.Count > 0) return Results.ValidationProblem(errors.ToMessages(localizer));

        try
        {
            var (dto, etag, location) = await app.CreateAsync(type, input, ct);
            ctx.Response.Headers.ETag = $"\"{etag:N}\"";
            return Results.Created($"{location}", dto);
        }
        catch (ReferenceLocationValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
        catch (ReferenceLocationCodeConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.LOCATION_CODE_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleUpdate(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationInput? input,
        HttpContext ctx,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        var errors = ReferenceLocationInputValidator.Validate(type, input, isCreate: false);
        if (errors.Count > 0) return Results.ValidationProblem(errors.ToMessages(localizer));

        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), localizer, out var expectedETag, out var error))
            return error!;

        try
        {
            var (dto, etag) = await app.UpdateAsync(type, id, input, expectedETag, ct);
            ctx.Response.Headers.ETag = $"\"{etag:N}\"";
            return Results.Ok(dto);
        }
        catch (ReferenceLocationNotFoundException)
        {
            return LocationNotFound(localizer, id);
        }
        catch (ReferenceLocationValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
        catch (ReferenceLocationCodeConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.LOCATION_CODE_CONFLICT].Value);
        }
        catch (ReferenceLocationConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.LOCATION_CONCURRENCY_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleDelete(
        ReferenceLocationType type,
        Guid id,
        HttpContext ctx,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (!TryParseIfMatch(ctx.Request.Headers.IfMatch.ToString(), localizer, out var expectedETag, out var error))
            return error!;

        try
        {
            await app.DeleteAsync(type, id, expectedETag, ct);
            return Results.NoContent();
        }
        catch (ReferenceLocationNotFoundException)
        {
            return LocationNotFound(localizer, id);
        }
        catch (ReferenceLocationConcurrencyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.LOCATION_CONCURRENCY_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleGetTranslations(
        ReferenceLocationType type,
        Guid id,
        string culture,
        IReferenceLocationAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        var translations = await app.GetTranslationsAsync(type, id, normalized, ct);
        return translations is null ? LocationNotFound(localizer, id) : Results.Ok(translations);
    }

    private static async Task<IResult> HandleSaveTranslations(
        ReferenceLocationType type,
        Guid id,
        string culture,
        SaveTranslationsInput? input,
        IReferenceLocationAppService app,
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

        try
        {
            var translations = await app.SaveTranslationsAsync(type, id, normalized, input, ct);
            return translations is null ? LocationNotFound(localizer, id) : Results.Ok(translations);
        }
        catch (ReferenceLocationValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDeleteTranslations(
        ReferenceLocationType type,
        Guid id,
        string culture,
        IReferenceLocationAppService app,
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
            await app.DeleteTranslationsAsync(type, id, normalized, ct);
            return Results.NoContent();
        }
        catch (ReferenceLocationNotFoundException)
        {
            return LocationNotFound(localizer, id);
        }
    }

    // ---------- 私有：响应构造 ----------

    private static IResult BodyRequired(IStringLocalizer<ApiErrorMessages> localizer) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.REQUEST_BODY_REQUIRED].Value);

    private static IResult LocationNotFound(IStringLocalizer<ApiErrorMessages> localizer, Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.LOCATION_NOT_FOUND, id].Value);

    private static IResult InvalidCulture(IStringLocalizer<ApiErrorMessages> localizer, string culture) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.LOCALIZATION_INVALID_CULTURE, culture].Value);

    private static IResult UnsupportedCulture(IStringLocalizer<ApiErrorMessages> localizer, string culture) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.LOCALIZATION_UNSUPPORTED_CULTURE, culture].Value);

    private static IResult ValidationFailure(
        IStringLocalizer<ApiErrorMessages> localizer,
        AxiomException exception) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[exception.ErrorCode, exception.Args].Value);

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
