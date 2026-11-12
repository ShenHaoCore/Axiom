// 手工 Region DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class RegionMapper
{
    /// <summary>翻译查找使用的实体标识：复合业务键的字符串形式（CN.110000）。</summary>
    public static string TranslationKey(Region entity) => $"{entity.CountryCode}.{entity.Code}";

    /// <summary>映射为 DTO；传入翻译查找表时名称优先使用当前语言的译文。</summary>
    public static RegionDto ToDto(Region entity, TranslationLookup? translations = null) => new(
        CountryCode: entity.CountryCode,
        Code: entity.Code,
        ParentCode: entity.ParentCode,
        Depth: entity.Depth,
        Type: entity.Type,
        Status: entity.Status,
        CodeIso: entity.CodeIso,
        SortKey: entity.SortKey,
        Abbrev: entity.Abbrev,
        Name: translations?.Find(TranslationKey(entity), RegionTranslationFields.Name) ?? entity.Name,
        AsciiName: entity.AsciiName,
        LocalName: entity.LocalName,
        Latitude: entity.Latitude,
        Longitude: entity.Longitude,
        Population: entity.Population,
        GeoNameId: entity.GeoNameId);

    public static RegionTreeDto ToTreeNode(
        Region entity,
        IReadOnlyList<RegionTreeDto> children,
        TranslationLookup? translations = null) => new(
        CountryCode: entity.CountryCode,
        Code: entity.Code,
        ParentCode: entity.ParentCode,
        Depth: entity.Depth,
        Type: entity.Type,
        Name: translations?.Find(TranslationKey(entity), RegionTranslationFields.Name) ?? entity.Name,
        Population: entity.Population,
        Children: children);
}
