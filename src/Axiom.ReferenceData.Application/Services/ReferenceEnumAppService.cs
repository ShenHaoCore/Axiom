// 通用枚举字典应用服务：唯一键冲突、引用检查；读取时按语言回退链合并 Name 译文。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class ReferenceEnumAppService(
    IReferenceEnumRepository repository,
    IGeoDivisionRepository geoDivisionRepository,
    AxiomLocalizationOptions localization) : IReferenceEnumAppService
{
    public async Task<PagedResponse<ReferenceEnumDto>> ListAsync(
        ReferenceEnumListQuery query,
        string? culture = null,
        CancellationToken ct = default)
    {
        var category = string.IsNullOrWhiteSpace(query.Category)
            ? null
            : ReferenceEnum.NormalizeKey(query.Category, nameof(query.Category));

        var paged = await repository.ListAsync(
            category,
            query.Page,
            Math.Clamp(query.PageSize, 1, 200),
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            paged.Items.Select(item => item.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return new PagedResponse<ReferenceEnumDto>(
            paged.Items.Select(item => ReferenceEnumMapper.ToDto(item, lookup)).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<ReferenceEnumDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);
        return ReferenceEnumMapper.ToDto(entity, lookup);
    }

    public async Task<(ReferenceEnumDto Dto, Uri LocationUri)> CreateAsync(
        CreateReferenceEnumInput input,
        CancellationToken ct = default)
    {
        var entity = ReferenceEnum.Create(input.Category, input.Code, input.Name, input.SortOrder);
        if (await repository.ExistsAsync(entity.Category, entity.Code, ct))
        {
            throw new ReferenceEnumCodeConflictException(entity.Category, entity.Code);
        }

        await repository.AddAsync(entity, ct);
        var location = new Uri($"/api/reference-data/enums/{entity.Id}", UriKind.Relative);
        return (ReferenceEnumMapper.ToDto(entity), location);
    }

    public async Task<ReferenceEnumDto> UpdateAsync(
        Guid id,
        UpdateReferenceEnumInput input,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceEnumNotFoundException(id);

        entity.Update(input.Name, input.SortOrder);
        await repository.UpdateAsync(entity, ct);
        return ReferenceEnumMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceEnumNotFoundException(id);

        if (entity.Category.StartsWith(ReferenceEnumCategories.GeoDivisionKind, StringComparison.Ordinal)
            && await geoDivisionRepository.AnyByKindAsync(entity.Code, ct))
        {
            throw new ReferenceEnumInUseException(entity.Category, entity.Code);
        }

        await repository.RemoveAsync(entity, ct);
    }

    public async Task<TranslationDto?> GetTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(id, ct) is null)
        {
            return null;
        }

        var rows = await repository.GetTranslationsAsync(id, culture, ct);
        var values = rows.ToDictionary(
            _ => ReferenceEnumTranslationFields.Name,
            row => row.Name);
        return new TranslationDto(culture, values);
    }

    public async Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(id, ct) is null)
        {
            return null;
        }

        var values = ValidateTranslationValues(input.Values);
        await repository.ReplaceTranslationsAsync(id, culture, values, ct);
        return new TranslationDto(culture, values);
    }

    public async Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(id, ct) is null)
        {
            throw new ReferenceEnumNotFoundException(id);
        }

        await repository.DeleteTranslationsAsync(id, culture, ct);
    }

    private static TranslationValue ToTranslationValue(ReferenceEnumTranslation row) =>
        new(row.ReferenceEnumId, row.Culture, ReferenceEnumTranslationFields.Name, row.Name);

    private static Dictionary<string, string> ValidateTranslationValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (field, value) in values)
        {
            if (!ReferenceEnumTranslationFields.IsKnown(field))
            {
                throw ReferenceEnumValidationException.UnknownTranslatableField(field);
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw ReferenceEnumValidationException.Required(field);
            }

            var maxLength = ReferenceEnumTranslationFields.MaxLength(field);
            if (trimmed.Length > maxLength)
            {
                throw ReferenceEnumValidationException.TooLong(field, maxLength);
            }

            normalized[field] = trimmed;
        }

        return normalized;
    }
}
