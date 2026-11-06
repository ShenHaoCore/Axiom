// 手工 Region DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class RegionMapper
{
    /// <summary>映射为 DTO；传入翻译查找表时名称优先使用当前语言的译文。</summary>
    public static RegionDto ToDto(Region entity, TranslationLookup? translations = null) => new(
        Id: entity.Id,
        ParentId: entity.ParentId,
        Level: (int)entity.Level,
        CountryCode: entity.CountryCode,
        Admin1Code: entity.Admin1Code,
        Code: entity.Code,
        Name: translations?.Find(entity.Id, RegionTranslationFields.Name) ?? entity.Name,
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
        Id: entity.Id,
        Level: (int)entity.Level,
        CountryCode: entity.CountryCode,
        Admin1Code: entity.Admin1Code,
        Code: entity.Code,
        Name: translations?.Find(entity.Id, RegionTranslationFields.Name) ?? entity.Name,
        Population: entity.Population,
        Children: children);
}
