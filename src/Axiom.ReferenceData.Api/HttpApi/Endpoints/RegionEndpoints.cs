// 行政区 REST 端点（HttpApi 层——瘦绑定）。
// 响应消息按请求语言本地化；请求语言由 RequestLocalization 中间件协商。
using System.Globalization;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.HttpApi;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;
using Axiom.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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

        group.MapGet("/{id:guid}/children", HandleChildren)
            .Produces<IReadOnlyList<RegionChildDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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

        // 翻译子资源：整语言读取 / 全量替换 / 清空。
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
        IRegionAppService app,
        int? level = null, string? countryCode = null, string? admin1Code = null, string? q = null,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        var paged = await app.ListAsync(
            new RegionListQuery(level, countryCode, admin1Code, q, page, pageSize),
            CultureInfo.CurrentUICulture.Name,
            ct);
        return Results.Ok(paged);
    }

    private static async Task<IResult> HandleTree(
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        string countryCode, int depth = 2,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "countryCode"].Value);
        }

        try
        {
            var tree = await app.GetTreeAsync(
                countryCode,
                depth,
                CultureInfo.CurrentUICulture.Name,
                ct);
            return tree is null ? CountryNotFound(localizer, countryCode) : Results.Ok(tree);
        }
        catch (RegionCountryNotFoundException)
        {
            return CountryNotFound(localizer, countryCode);
        }
    }

    private static async Task<IResult> HandleChildren(
        Guid id,
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct = default)
    {
        try
        {
            return Results.Ok(await app.GetChildrenAsync(id, CultureInfo.CurrentUICulture.Name, ct));
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, id);
        }
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct = default)
    {
        var dto = await app.GetAsync(id, CultureInfo.CurrentUICulture.Name, ct);
        return dto is null ? RegionNotFound(localizer, id) : Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        CreateRegionInput? input,
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct = default)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        if (string.IsNullOrWhiteSpace(input.CountryCode) || string.IsNullOrWhiteSpace(input.Name))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "countryCode/name"].Value);
        }

        if (string.IsNullOrWhiteSpace(input.Level))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.REGION_INVALID_LEVEL].Value);
        }

        try
        {
            var (dto, location) = await app.CreateAsync(input, ct);
            return Results.Created(location.ToString(), dto);
        }
        catch (RegionValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
        catch (RegionUniqueKeyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.REGION_UNIQUE_KEY_CONFLICT].Value);
        }
    }

    private static async Task<IResult> HandleUpdate(
        Guid id, UpdateRegionInput? input, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        try
        {
            return Results.Ok(await app.UpdateAsync(id, input, ct));
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, id);
        }
        catch (RegionValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDelete(
        Guid id, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        try
        {
            await app.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, id);
        }
        catch (RegionHasChildrenException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.REGION_HAS_CHILDREN, id].Value);
        }
    }

    private static async Task<IResult> HandleGetTranslations(
        Guid id, string culture, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        var translations = await app.GetTranslationsAsync(id, normalized, ct);
        return translations is null ? RegionNotFound(localizer, id) : Results.Ok(translations);
    }

    private static async Task<IResult> HandleSaveTranslations(
        Guid id, string culture, SaveTranslationsInput? input,
        IRegionAppService app, AxiomLocalizationOptions localization,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
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
            var translations = await app.SaveTranslationsAsync(id, normalized, input, ct);
            return translations is null ? RegionNotFound(localizer, id) : Results.Ok(translations);
        }
        catch (RegionValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDeleteTranslations(
        Guid id, string culture, IRegionAppService app,
        AxiomLocalizationOptions localization,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
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
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, id);
        }
    }

    // ---------- 私有：响应构造 ----------

    private static IResult BodyRequired(IStringLocalizer<ApiErrorMessages> localizer) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.REQUEST_BODY_REQUIRED].Value);

    private static IResult RegionNotFound(IStringLocalizer<ApiErrorMessages> localizer, Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.REGION_NOT_FOUND, id].Value);

    private static IResult CountryNotFound(IStringLocalizer<ApiErrorMessages> localizer, string countryCode) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.REGION_COUNTRY_NOT_FOUND, countryCode].Value);

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
}
