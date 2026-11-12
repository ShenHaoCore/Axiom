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

        group.MapGet("/{country}/{code}/children", HandleChildren)
            .Produces<IReadOnlyList<RegionChildDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{country}/{code}", HandleGet)
            .Produces<RegionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", HandleCreate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{country}/{code}", HandleUpdate)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<RegionDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{country}/{code}", HandleDelete)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 翻译子资源：整语言读取 / 全量替换 / 清空。
        group.MapGet("/{country}/{code}/translations/{culture}", HandleGetTranslations)
            .RequireAuthorization(READ_POLICY)
            .Produces<TranslationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{country}/{code}/translations/{culture}", HandleSaveTranslations)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<TranslationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{country}/{code}/translations/{culture}", HandleDeleteTranslations)
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleList(
        IRegionAppService app,
        int? depth = null, string? countryCode = null, string? parentCode = null, string? q = null,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        var paged = await app.ListAsync(
            new RegionListQuery(depth, countryCode, parentCode, q, page, pageSize),
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
        string country, string code,
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct = default)
    {
        try
        {
            return Results.Ok(await app.GetChildrenAsync(country, code, CultureInfo.CurrentUICulture.Name, ct));
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, country, code);
        }
    }

    private static async Task<IResult> HandleGet(
        string country, string code,
        IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct = default)
    {
        var dto = await app.GetAsync(country, code, CultureInfo.CurrentUICulture.Name, ct);
        return dto is null ? RegionNotFound(localizer, country, code) : Results.Ok(dto);
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
        catch (RegionParentNotFoundException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer[ApiErrorCodes.REGION_PARENT_NOT_FOUND, ex.Args].Value);
        }
    }

    private static async Task<IResult> HandleUpdate(
        string country, string code, UpdateRegionInput? input, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        try
        {
            return Results.Ok(await app.UpdateAsync(country, code, input, ct));
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, country, code);
        }
        catch (RegionValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDelete(
        string country, string code, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        try
        {
            await app.DeleteAsync(country, code, ct);
            return Results.NoContent();
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, country, code);
        }
        catch (RegionHasChildrenException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.REGION_HAS_CHILDREN, ex.Args].Value);
        }
    }

    private static async Task<IResult> HandleGetTranslations(
        string country, string code, string culture, IRegionAppService app,
        IStringLocalizer<ApiErrorMessages> localizer, CancellationToken ct = default)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        var translations = await app.GetTranslationsAsync(country, code, normalized, ct);
        return translations is null ? RegionNotFound(localizer, country, code) : Results.Ok(translations);
    }

    private static async Task<IResult> HandleSaveTranslations(
        string country, string code, string culture, SaveTranslationsInput? input,
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
            var translations = await app.SaveTranslationsAsync(country, code, normalized, input, ct);
            return translations is null ? RegionNotFound(localizer, country, code) : Results.Ok(translations);
        }
        catch (RegionValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDeleteTranslations(
        string country, string code, string culture, IRegionAppService app,
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
            await app.DeleteTranslationsAsync(country, code, normalized, ct);
            return Results.NoContent();
        }
        catch (RegionNotFoundException)
        {
            return RegionNotFound(localizer, country, code);
        }
    }

    // ---------- 私有：响应构造 ----------

    private static IResult BodyRequired(IStringLocalizer<ApiErrorMessages> localizer) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.REQUEST_BODY_REQUIRED].Value);

    private static IResult RegionNotFound(IStringLocalizer<ApiErrorMessages> localizer, string country, string code) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.REGION_NOT_FOUND, country, code].Value);

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
