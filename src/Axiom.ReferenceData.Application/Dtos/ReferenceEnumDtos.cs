// Application 层 DTO：通用枚举字典。
namespace Axiom.ReferenceData.Application.Dtos;

public sealed record ReferenceEnumDto(
    Guid Id,
    string Category,
    string Code,
    string Name,
    int SortOrder);

public sealed record CreateReferenceEnumInput(
    string Category,
    string Code,
    string Name,
    int SortOrder = 0);

public sealed record UpdateReferenceEnumInput(
    string? Name = null,
    int? SortOrder = null);

public sealed record ReferenceEnumListQuery(
    string? Category = null,
    int Page = 1,
    int PageSize = 50);
