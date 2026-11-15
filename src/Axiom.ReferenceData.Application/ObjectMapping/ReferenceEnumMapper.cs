// ReferenceEnum ↔ DTO 手工映射。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.ReferenceData.Application.ObjectMapping;

public static class ReferenceEnumMapper
{
    /// <summary>映射为 DTO；传入翻译查找表时名称优先使用当前语言的译文。</summary>
    public static ReferenceEnumDto ToDto(ReferenceEnum entity, TranslationLookup? translations = null) => new(
        entity.Id,
        entity.Category,
        entity.Code,
        translations?.Find(entity.Id, ReferenceEnumTranslationFields.Name) ?? entity.Name,
        entity.SortOrder);
}
