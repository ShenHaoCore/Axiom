// 父级行政区树形下拉：惰性加载节点 DTO 与控件绑定模型。
namespace Axiom.Web.Models;

/// <summary>父级树下拉单次加载返回的节点（根或某节点的直接子级）。</summary>
public sealed record ParentTreeSelectItemDto(
    Guid Id,
    string Label,
    int Level,
    bool Selectable,
    bool HasChildren,
    bool Selected);

public sealed class ParentTreeSelectViewModel
{
    public string InputId { get; init; } = "parent-tree-select";
    public string InputName { get; init; } = "parentId";
    public string? FormId { get; init; }
    public bool AutoSubmit { get; init; }
    public string EmptyLabel { get; init; } = string.Empty;
    public string SearchPlaceholder { get; init; } = string.Empty;
    public string LoadErrorText { get; init; } = string.Empty;
    public Guid? SelectedId { get; init; }
    public string SelectedLabel { get; init; } = string.Empty;

    /// <summary>惰性加载 URL（Create?handler=ParentNodes），由 JS 追加 countryCode/parentId 等参数。</summary>
    public string NodesUrl { get; init; } = string.Empty;

    public string? CountryCode { get; init; }

    /// <summary>可展开的最大层级（该层节点仍可选，但其下不再展开）。</summary>
    public int MaxParentLevel { get; init; } = 3;

    /// <summary>仅该层级可选；null 表示 Level ≤ MaxParentLevel 均可选。</summary>
    public int? SelectableLevel { get; init; }

    /// <summary>编辑时排除的分支根（自身），其子树不可选也不可展开为父级。</summary>
    public Guid? ExcludeId { get; init; }
}
