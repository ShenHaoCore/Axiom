// 父级行政区树形下拉：惰性加载节点 DTO 与控件绑定模型。
namespace Axiom.Web.Models;

/// <summary>父级树下拉单次加载返回的节点（根或某节点的直接子级）。</summary>
public sealed record ParentTreeSelectItemDto(
    Guid Id,
    string Label,
    int Level,
    bool Selectable,
    bool HasChildren,
    bool Selected,
    string CountryCode,
    string? SubdivisionCode);

public sealed class ParentTreeSelectViewModel
{
    public string InputId { get; init; } = "parent-tree-select";
    public string InputName { get; init; } = "parentId";
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

    /// <summary>国家代码输入框的元素 id：新建页输入国家后 AJAX 刷新 Kind 选项与父级树。</summary>
    public string? CountryInputId { get; init; }

    /// <summary>Kind 下拉框的元素 id：国家变更时由 JS 重建选项。</summary>
    public string? KindSelectId { get; init; }

    /// <summary>Kind 下拉"自动推导"选项的显示文案。</summary>
    public string KindAutoLabel { get; init; } = string.Empty;

    /// <summary>层级隐藏域的元素 id：选择父级后由 JS 更新为 所选节点 Level + 1。</summary>
    public string? LevelInputId { get; init; }

    /// <summary>按国家分域拉取 Kind 选项的 JSON 端点（Create?handler=KindOptions）。</summary>
    public string KindOptionsUrl { get; init; } = string.Empty;
}
