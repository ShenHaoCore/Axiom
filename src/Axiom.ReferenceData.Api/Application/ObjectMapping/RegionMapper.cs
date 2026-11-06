// 手工 Region DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class RegionMapper
{
    public static RegionDto ToDto(Region entity) => new(
        Id: entity.Id,
        ParentId: entity.ParentId,
        Level: (int)entity.Level,
        CountryCode: entity.CountryCode,
        Admin1Code: entity.Admin1Code,
        Code: entity.Code,
        Name: entity.Name,
        AsciiName: entity.AsciiName,
        LocalName: entity.LocalName,
        Latitude: entity.Latitude,
        Longitude: entity.Longitude,
        Population: entity.Population,
        GeoNameId: entity.GeoNameId);

    public static RegionTreeDto ToTreeNode(Region entity, IReadOnlyList<RegionTreeDto> children) => new(
        Id: entity.Id,
        Level: (int)entity.Level,
        CountryCode: entity.CountryCode,
        Admin1Code: entity.Admin1Code,
        Code: entity.Code,
        Name: entity.Name,
        Population: entity.Population,
        Children: children);
}
