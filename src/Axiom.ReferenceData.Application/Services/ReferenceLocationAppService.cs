// 地点档案应用服务实现（Application 层）。
// 唯一键检查、并发令牌协调、ETag 解析——全部放在这里。
// Outbox 消息发布由 Infrastructure 层的 Repository 实现内部处理，保证事务一致性。
// 读取时按语言回退链合并翻译子表；没有译文则回退到主表默认语言原值。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class ReferenceLocationAppService(
    IReferenceLocationRepository repository,
    AxiomLocalizationOptions localization) : IReferenceLocationAppService
{
    public async Task<PagedResponse<ReferenceLocationDto>> ListAsync(
        ReferenceLocationType type,
        ReferenceLocationListQuery query,
        string? culture = null,
        CancellationToken ct = default)
    {
        var paged = await repository.ListByTypeAsync(
            type,
            query.Code,
            query.Name,
            query.Page,
            query.PageSize,
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            paged.Items.Select(location => location.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return new PagedResponse<ReferenceLocationDto>(
            paged.Items.Select(location => ReferenceLocationMapper.ToDto(location, lookup)).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<(ReferenceLocationDto? Dto, Guid? ETag)> GetAsync(
        ReferenceLocationType type,
        Guid id,
        string? culture = null,
        CancellationToken ct = default)
    {
        var entity = await FindAsync(type, id, ct);
        if (entity is null)
        {
            return (null, null);
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return (ReferenceLocationMapper.ToDto(entity, lookup), entity.ConcurrencyToken);
    }

    public async Task<(ReferenceLocationDto Dto, Guid ETag, Uri LocationUri)> CreateAsync(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input,
        CancellationToken ct = default)
    {
        var entity = ReferenceLocationMapper.ToEntity(type, input);
        if (await repository.CodeExistsAsync(type, entity.Code, excludedId: null, ct))
        {
            throw new ReferenceLocationCodeConflictException(entity.Code, (int)type);
        }

        await repository.AddAsync(entity, ct);

        var dto = ReferenceLocationMapper.ToDto(entity);
        var location = new Uri($"/api/reference-data/{TypeToPlural(type).ToLowerInvariant()}/{entity.Id}", UriKind.Relative);
        return (dto, entity.ConcurrencyToken, location);
    }

    public async Task<(ReferenceLocationDto Dto, Guid ETag)> UpdateAsync(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationInput input,
        Guid expectedETag,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceLocationNotFoundException(id);
        if (entity.Type != type)
            throw new ReferenceLocationNotFoundException(id);

        var normalizedCode = ReferenceLocation.NormalizeCode(input.Code);
        if (await repository.CodeExistsAsync(type, normalizedCode, excludedId: id, ct))
        {
            throw new ReferenceLocationCodeConflictException(normalizedCode, (int)type);
        }

        entity.Update(
            input.Code,
            input.Name,
            input.CountryCode,
            input.City,
            input.Latitude,
            input.Longitude);

        await repository.UpdateWithETagAsync(entity, expectedETag, ct);

        var dto = ReferenceLocationMapper.ToDto(entity);
        return (dto, entity.ConcurrencyToken);
    }

    public async Task DeleteAsync(
        ReferenceLocationType type,
        Guid id,
        Guid expectedETag,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceLocationNotFoundException(id);
        if (entity.Type != type)
            throw new ReferenceLocationNotFoundException(id);

        await repository.RemoveWithETagAsync(entity, expectedETag, ct);
    }

    public async Task<TranslationDto?> GetTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        CancellationToken ct = default)
    {
        if (await FindAsync(type, id, ct) is null)
        {
            return null;
        }

        var rows = await repository.GetTranslationsAsync(id, culture, ct);
        return new TranslationDto(culture, rows.ToDictionary(row => row.Field, row => row.Value));
    }

    public async Task<TranslationDto?> SaveTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default)
    {
        if (await FindAsync(type, id, ct) is null)
        {
            return null;
        }

        var values = ValidateTranslationValues(input.Values);
        await repository.ReplaceTranslationsAsync(id, culture, values, ct);
        return new TranslationDto(culture, values);
    }

    public async Task DeleteTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        CancellationToken ct = default)
    {
        if (await FindAsync(type, id, ct) is null)
        {
            throw new ReferenceLocationNotFoundException(id);
        }

        await repository.DeleteTranslationsAsync(id, culture, ct);
    }

    // ---------- 私有辅助方法 ----------

    private async Task<ReferenceLocation?> FindAsync(
        ReferenceLocationType type,
        Guid id,
        CancellationToken ct)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        return entity is not null && entity.Type == type ? entity : null;
    }

    private static TranslationValue ToTranslationValue(ReferenceLocationTranslation row) =>
        new(row.ReferenceLocationId.ToString(), row.Culture, row.Field, row.Value);

    private static Dictionary<string, string> ValidateTranslationValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (field, value) in values)
        {
            if (!ReferenceLocationTranslationFields.IsKnown(field))
            {
                throw ReferenceLocationValidationException.UnknownTranslatableField(field);
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw ReferenceLocationValidationException.Required(field);
            }

            var maxLength = ReferenceLocationTranslationFields.MaxLength(field);
            if (trimmed.Length > maxLength)
            {
                throw ReferenceLocationValidationException.TooLong(field, maxLength);
            }

            normalized[field] = trimmed;
        }

        return normalized;
    }

    private static string TypeToPlural(ReferenceLocationType type) => type switch
    {
        ReferenceLocationType.Airport => "airports",
        ReferenceLocationType.Seaport => "seaports",
        ReferenceLocationType.RailStation => "rail-stations",
        _ => type.ToString()
    };
}
