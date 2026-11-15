// 手工 GeoDivision DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class GeoDivisionMapper
{
    /// <summary>映射为 DTO；传入翻译查找表时名称优先使用当前语言的译文。</summary>
    public static GeoDivisionDto ToDto(GeoDivision entity, TranslationLookup? translations = null) => new(
        Id: entity.Id,
        ParentId: entity.ParentId,
        Level: entity.Level,
        Kind: entity.Kind,
        CountryCode: entity.CountryCode,
        SubdivisionCode: entity.SubdivisionCode,
        Code: entity.Code,
        Name: translations?.Find(entity.Id, GeoDivisionTranslationFields.Name) ?? entity.Name,
        AsciiName: entity.AsciiName,
        LocalName: entity.LocalName,
        Latitude: entity.Latitude,
        Longitude: entity.Longitude,
        Population: entity.Population,
        GeoNameId: entity.GeoNameId);

    public static GeoDivisionTreeDto ToTreeNode(
        GeoDivision entity,
        IReadOnlyList<GeoDivisionTreeDto> children,
        TranslationLookup? translations = null) => new(
        Id: entity.Id,
        Level: entity.Level,
        Kind: entity.Kind,
        CountryCode: entity.CountryCode,
        SubdivisionCode: entity.SubdivisionCode,
        Code: entity.Code,
        Name: translations?.Find(entity.Id, GeoDivisionTranslationFields.Name) ?? entity.Name,
        Population: entity.Population,
        Children: children);
}
