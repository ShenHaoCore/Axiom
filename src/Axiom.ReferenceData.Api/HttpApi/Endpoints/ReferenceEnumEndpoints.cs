// 通用枚举字典 REST 端点（HttpApi 层——瘦绑定）。
using System.Globalization;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;
using Axiom.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Axiom.ReferenceData.HttpApi.Endpoints;

public static class ReferenceEnumEndpoints
{
    public const string READ_POLICY = ReferenceLocationEndpoints.READ_POLICY;
    public const string MANAGE_POLICY = ReferenceLocationEndpoints.MANAGE_POLICY;

    public static void MapReferenceEnumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reference-data/enums").WithTags("ReferenceEnums");

        group.MapGet("/", ([AsParameters] ReferenceEnumListQuery query,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleList(query, appService, localizer, ct))
            .RequireAuthorization(READ_POLICY)
            .Produces<PagedResponse<ReferenceEnumDto>>();

        group.MapGet("/{id:guid}", (Guid id,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleGet(id, appService, localizer, ct))
            .RequireAuthorization(READ_POLICY)
            .Produces<ReferenceEnumDto>();

        group.MapPost("/", (CreateReferenceEnumInput? input,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleCreate(input, appService, localizer, ct))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<ReferenceEnumDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", (Guid id, UpdateReferenceEnumInput? input,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleUpdate(id, input, appService, localizer, ct))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<ReferenceEnumDto>();

        group.MapDelete("/{id:guid}", (Guid id,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleDelete(id, appService, localizer, ct))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/{id:guid}/translations/{culture}", (Guid id, string culture,
                IReferenceEnumAppService appService,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleGetTranslations(id, culture, appService, localizer, ct))
            .RequireAuthorization(READ_POLICY)
            .Produces<TranslationDto>();

        group.MapPut("/{id:guid}/translations/{culture}", (Guid id, string culture,
                SaveTranslationsInput? input,
                IReferenceEnumAppService appService,
                AxiomLocalizationOptions localization,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleSaveTranslations(id, culture, input, appService, localization, localizer, ct))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<TranslationDto>();

        group.MapDelete("/{id:guid}/translations/{culture}", (Guid id, string culture,
                IReferenceEnumAppService appService,
                AxiomLocalizationOptions localization,
                IStringLocalizer<ApiErrorMessages> localizer,
                CancellationToken ct) =>
                HandleDeleteTranslations(id, culture, appService, localization, localizer, ct))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> HandleList(
        ReferenceEnumListQuery query,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        try
        {
            return Results.Ok(await appService.ListAsync(query, CultureInfo.CurrentUICulture.Name, ct));
        }
        catch (ReferenceEnumValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var dto = await appService.GetAsync(id, CultureInfo.CurrentUICulture.Name, ct);
        return dto is null ? NotFound(localizer, id) : Results.Ok(dto);
    }

    private static async Task<IResult> HandleCreate(
        CreateReferenceEnumInput? input,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        try
        {
            var (dto, location) = await appService.CreateAsync(input, ct);
            return Results.Created(location.ToString(), dto);
        }
        catch (ReferenceEnumValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
        catch (ReferenceEnumCodeConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ApiErrorCodes.REFERENCE_ENUM_CODE_CONFLICT, input.Category, input.Code].Value);
        }
    }

    private static async Task<IResult> HandleUpdate(
        Guid id,
        UpdateReferenceEnumInput? input,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        if (input is null)
        {
            return BodyRequired(localizer);
        }

        try
        {
            return Results.Ok(await appService.UpdateAsync(id, input, ct));
        }
        catch (ReferenceEnumNotFoundException)
        {
            return NotFound(localizer, id);
        }
        catch (ReferenceEnumValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDelete(
        Guid id,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        try
        {
            await appService.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (ReferenceEnumNotFoundException)
        {
            return NotFound(localizer, id);
        }
        catch (ReferenceEnumInUseException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer[ex.ErrorCode, ex.Args].Value);
        }
    }

    private static async Task<IResult> HandleGetTranslations(
        Guid id,
        string culture,
        IReferenceEnumAppService appService,
        IStringLocalizer<ApiErrorMessages> localizer,
        CancellationToken ct)
    {
        var normalized = AxiomCultureResolver.TryNormalize(culture);
        if (normalized is null)
        {
            return InvalidCulture(localizer, culture);
        }

        var translations = await appService.GetTranslationsAsync(id, normalized, ct);
        return translations is null ? NotFound(localizer, id) : Results.Ok(translations);
    }

    private static async Task<IResult> HandleSaveTranslations(
        Guid id,
        string culture,
        SaveTranslationsInput? input,
        IReferenceEnumAppService appService,
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
            var translations = await appService.SaveTranslationsAsync(id, normalized, input, ct);
            return translations is null ? NotFound(localizer, id) : Results.Ok(translations);
        }
        catch (ReferenceEnumValidationException ex)
        {
            return ValidationFailure(localizer, ex);
        }
    }

    private static async Task<IResult> HandleDeleteTranslations(
        Guid id,
        string culture,
        IReferenceEnumAppService appService,
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
            await appService.DeleteTranslationsAsync(id, normalized, ct);
            return Results.NoContent();
        }
        catch (ReferenceEnumNotFoundException)
        {
            return NotFound(localizer, id);
        }
    }

    private static IResult BodyRequired(IStringLocalizer<ApiErrorMessages> localizer) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: localizer[ApiErrorCodes.REQUEST_BODY_REQUIRED].Value);

    private static IResult NotFound(IStringLocalizer<ApiErrorMessages> localizer, Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: localizer[ApiErrorCodes.REFERENCE_ENUM_NOT_FOUND, id].Value);

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
