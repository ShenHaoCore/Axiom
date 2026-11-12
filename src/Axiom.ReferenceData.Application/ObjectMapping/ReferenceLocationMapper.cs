// 手工 ReferenceLocation DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class ReferenceLocationMapper
{
    /// <summary>映射为 DTO；传入翻译查找表时名称/城市优先使用当前语言的译文。</summary>
    public static ReferenceLocationDto ToDto(
        ReferenceLocation entity,
        TranslationLookup? translations = null) => new(
        Id: entity.Id,
        ConcurrencyToken: entity.ConcurrencyToken,
        Type: entity.Type.ToString(),
        Code: entity.Code,
        Name: translations?.Find(entity.Id.ToString(), ReferenceLocationTranslationFields.Name) ?? entity.Name,
        CountryCode: entity.CountryCode,
        City: translations?.Find(entity.Id.ToString(), ReferenceLocationTranslationFields.City) ?? entity.City,
        Latitude: entity.Latitude,
        Longitude: entity.Longitude);

    public static ReferenceLocation ToEntity(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input) => ReferenceLocation.Create(
            type,
            input.Code,
            input.Name,
            input.CountryCode,
            input.City,
            input.Latitude,
            input.Longitude);
}
