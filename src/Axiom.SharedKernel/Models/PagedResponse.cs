// 定义 REST 列表接口统一使用的分页响应结构。
namespace Axiom.SharedKernel.Models;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
