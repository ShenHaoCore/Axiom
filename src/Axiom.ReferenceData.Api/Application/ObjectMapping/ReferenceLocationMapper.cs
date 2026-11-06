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

    public static ReferenceLocation ToEntity(ReferenceLocationType type, UpsertReferenceLocationInput input) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Code = input.Code.Trim().ToUpperInvariant(),
        Name = input.Name.Trim(),
        CountryCode = input.CountryCode.Trim().ToUpperInvariant(),
        City = NormalizeNullable(input.City),
        Latitude = input.Latitude,
        Longitude = input.Longitude
    };

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
