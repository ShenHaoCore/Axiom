// 父级行政区树形下拉：节点与控件绑定模型。
namespace Axiom.Web.Models;

public sealed class ParentTreeSelectNode
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public bool Selectable { get; init; }
    public bool Selected { get; init; }
    public IReadOnlyList<ParentTreeSelectNode> Children { get; init; } = [];
}

public sealed class ParentTreeSelectViewModel
{
    public string InputId { get; init; } = "parent-tree-select";
    public string InputName { get; init; } = "parentId";
    public string? FormId { get; init; }
    public bool AutoSubmit { get; init; }
    public string EmptyLabel { get; init; } = string.Empty;
    public string SearchPlaceholder { get; init; } = string.Empty;
    public Guid? SelectedId { get; init; }
    public string SelectedLabel { get; init; } = string.Empty;
    public IReadOnlyList<ParentTreeSelectNode> Nodes { get; init; } = [];

    public static IReadOnlyList<ParentTreeSelectNode> Build(
        GeoDivisionTreeView tree,
        Guid? selectedId,
        Func<GeoDivisionTreeView, bool> isSelectable,
        Guid? excludeBranchId = null)
    {
        ParentTreeSelectNode? Visit(GeoDivisionTreeView node, bool inExcludedBranch)
        {
            var excluded = inExcludedBranch || node.Id == excludeBranchId;
            var children = new List<ParentTreeSelectNode>();
            foreach (var child in node.Children)
            {
                var mapped = Visit(child, excluded);
                if (mapped is not null)
                {
                    children.Add(mapped);
                }
            }

            var selectable = !excluded && isSelectable(node);
            // 不可选且无子节点：不占位（编辑时裁掉本支或非目标层叶子）。
            if (!selectable && children.Count == 0)
            {
                return null;
            }

            return new ParentTreeSelectNode
            {
                Id = node.Id,
                Label = $"{node.Name} ({node.Code})",
                Selectable = selectable,
                Selected = selectedId == node.Id,
                Children = children
            };
        }

        var root = Visit(tree, false);
        return root is null ? [] : [root];
    }

    public static string? FindLabel(IEnumerable<ParentTreeSelectNode> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node.Label;
            }

            var nested = FindLabel(node.Children, id);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    public static bool ContainsSelectable(IEnumerable<ParentTreeSelectNode> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Selectable && node.Id == id)
            {
                return true;
            }

            if (ContainsSelectable(node.Children, id))
            {
                return true;
            }
        }

        return false;
    }
}
