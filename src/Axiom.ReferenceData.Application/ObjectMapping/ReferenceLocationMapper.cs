// 手工 ReferenceLocation DTO ↔ Entity 映射（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class ReferenceLocationMapper
{
    public static ReferenceLocationDto ToDto(ReferenceLocation entity) => new(
        Id: entity.Id,
        ConcurrencyToken: entity.ConcurrencyToken,
        Type: entity.Type.ToString(),
        Code: entity.Code,
        Name: entity.Name,
        CountryCode: entity.CountryCode,
        City: entity.City,
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
