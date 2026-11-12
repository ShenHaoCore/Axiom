// ReferenceEnum ↔ DTO 手工映射。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class ReferenceEnumMapper
{
    public static ReferenceEnumDto ToDto(ReferenceEnum entity) => new(
        entity.Id,
        entity.Category,
        entity.Code,
        entity.Name,
        entity.SortOrder);
}
