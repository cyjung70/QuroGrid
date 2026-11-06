using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quro.Grid.Data;

namespace Quro.Grid;

public partial class QuroGrid<TItem>
{
    /// <summary>Collapsed rows by key value, not row key, so the state survives Load (GS-104).</summary>
    private readonly HashSet<object> _treeCollapsed = [];

    /// <summary>Field holding each row's own tree key. Together with <see cref="TreeParentField"/> it turns tree mode on (A-61).</summary>
    [Parameter] public string? TreeKeyField { get; set; }

    /// <summary>Field holding the key of the row's parent. Empty or unknown means a root row (A-61).</summary>
    [Parameter] public string? TreeParentField { get; set; }

    /// <summary>The column showing the indent and the toggle. Defaults to the first shown column (A-61).</summary>
    [Parameter] public string? TreeColumn { get; set; }

    internal bool TreeMode => !string.IsNullOrEmpty(TreeKeyField) && !string.IsNullOrEmpty(TreeParentField);

    internal bool IsTreeCell(QuroColumn col)
    {
        if (!TreeMode)
            return false;
        var columns = Columns;
        return ReferenceEquals(col, columns.FirstOrDefault(c => c.Field == TreeColumn) ?? columns.FirstOrDefault());
    }

    /// <summary>Expands or collapses one row. False when the row is not shown in the tree or has no children (GS-104).</summary>
    [JSInvokable]
    public bool ExpandTreeRow(int rowKey, bool expanded)
    {
        if (!TreeMode || !BuildTree(all: true).Any(i => i.RowKey == rowKey && i.HasChildren))
            return false;
        var key = DataSet.GetValue(rowKey, TreeKeyField!)!;
        if (expanded)
        {
            _treeCollapsed.Remove(key);
        }
        else
        {
            _treeCollapsed.Add(key);
            _ensureActive = true;   // the active cell may have been inside the collapsed rows
        }
        StateHasChanged();
        return true;
    }

    /// <summary>Expands every row of the tree.</summary>
    public void ExpandAllTree()
    {
        if (!TreeMode)
            return;
        _treeCollapsed.Clear();
        StateHasChanged();
    }

    /// <summary>Collapses every row of the tree that has children.</summary>
    public void CollapseAllTree()
    {
        if (!TreeMode)
            return;
        _treeCollapsed.UnionWith(BuildTree(all: true).Where(i => i.HasChildren).Select(i => DataSet.GetValue(i.RowKey, TreeKeyField!)!));
        _ensureActive = true;
        StateHasChanged();
    }

    /// <summary>
    /// Adds a child: sets the parent field, shows it after the parent's last descendant and expands the parent and its ancestors.
    /// Null, with no row added, when the grid is not a tree or the parent is unknown, deleted or has no key value yet (GS-105).
    /// </summary>
    [JSInvokable]
    public async Task<int?> AddChildRow(int parentRowKey)
    {
        if (!TreeMode || !DataSet.RowKeys.Contains(parentRowKey) || DataSet.GetState(parentRowKey) == RowState.Deleted)
            return null;
        var parentKey = DataSet.GetValue(parentRowKey, TreeKeyField!);
        if (EmptyTreeValue(parentKey))
            return null;

        using var step = DataSet.BeginUndoStep();   // one user action = one undo step (A-33)
        var rows = TreeRowKeys();
        var parents = TreeParents(rows);
        for (int? row = parentRowKey; row is { } at; row = parents.TryGetValue(at, out var up) ? up : null)
            _treeCollapsed.Remove(DataSet.GetValue(at, TreeKeyField!) ?? "");

        // after the last sibling in the list: the tree then shows the new row after that sibling's descendants too
        var after = rows.Where(k => parents.TryGetValue(k, out var p) && p == parentRowKey).Select(k => (int?)k).LastOrDefault() ?? parentRowKey;
        var key = await AddRowCore(after: after, tree: new TreePlacement(parentKey));
        StateHasChanged();
        return key;
    }

    /// <summary>The parent field value a new row gets (GS-105).</summary>
    private readonly record struct TreePlacement(object? Parent);

    /// <summary>A row added below another is its sibling. Without a position the app decides; an empty item is a root.</summary>
    private TreePlacement? TreeSiblingOf(int? rowKey) =>
        TreeMode && rowKey is { } row ? new TreePlacement(DataSet.GetValue(row, TreeParentField!)) : null;

    /// <summary>
    /// Each selected row with its descendants, parent first, including collapsed and filtered-out ones.
    /// A selected row under another selected row belongs to that one's set.
    /// </summary>
    private List<IReadOnlyList<int>> TreeSubtrees(IReadOnlyList<int> rowKeys)
    {
        var rows = OrderedRowKeys().Where(k => DataSet.GetState(k) != RowState.Deleted).ToList();
        var parents = TreeParents(rows);
        var children = rows.Where(parents.ContainsKey).ToLookup(k => parents[k]);
        var present = rows.ToHashSet();
        var selected = rowKeys.Where(present.Contains).ToHashSet();

        var sets = new List<IReadOnlyList<int>>();
        foreach (var top in rowKeys.Distinct().Where(present.Contains))
        {
            var under = false;
            for (var row = top; !under && parents.TryGetValue(row, out var parent); row = parent)
                under = selected.Contains(parent);
            if (under)
                continue;

            var set = new List<int>();
            var stack = new Stack<int>([top]);
            while (stack.TryPop(out var row))
            {
                set.Add(row);
                foreach (var child in children[row].Reverse())
                    stack.Push(child);
            }
            sets.Add(set);
        }
        return sets;
    }

    private static bool EmptyTreeValue(object? value) => value is null or "";

    /// <summary>The rows the tree is built from, in list order.</summary>
    private List<int> TreeRowKeys() =>
        OrderedRowKeys().Where(k => DeletedRows != DeletedRowDisplay.Hide || DataSet.GetState(k) != RowState.Deleted).ToList();

    /// <summary>
    /// Depth-first rows of the tree. Siblings keep the sorted list order. A row whose parent is empty, unknown, hidden or on a
    /// parent cycle is a root. With a filter, ancestors of a matching row stay. <paramref name="all"/> ignores collapsed rows.
    /// </summary>
    private List<BodyItem> BuildTree(bool all)
    {
        var keys = TreeRowKeys();
        var parents = TreeParents(keys);

        HashSet<int>? kept = null;
        if (_filters.Count > 0)
        {
            kept = [];
            foreach (var key in keys.Where(k => !FilteredOut(k)))
                for (var row = key; kept.Add(row) && parents.TryGetValue(row, out var parent); row = parent) { }
        }

        var children = new Dictionary<int, List<int>>();
        var roots = new List<int>();
        foreach (var key in keys)
        {
            if (kept?.Contains(key) == false)
                continue;
            if (!parents.TryGetValue(key, out var parent))
                roots.Add(key);
            else if (children.TryGetValue(parent, out var siblings))
                siblings.Add(key);
            else
                children[parent] = [key];
        }

        var items = new List<BodyItem>(keys.Count);
        var stack = new Stack<(int Key, int Level)>();
        void Push(List<int> rows, int level)
        {
            for (var i = rows.Count - 1; i >= 0; i--)
                stack.Push((rows[i], level));
        }

        Push(roots, 1);
        while (stack.TryPop(out var row))
        {
            var hasChildren = children.TryGetValue(row.Key, out var below);
            var expanded = !hasChildren || !_treeCollapsed.Contains(DataSet.GetValue(row.Key, TreeKeyField!)!);
            items.Add(new BodyItem(BodyItemKind.Row, items.Count, row.Key, Level: row.Level, Expanded: expanded, HasChildren: hasChildren));
            if (hasChildren && (expanded || all))
                Push(below!, row.Level + 1);
        }
        return items;
    }

    /// <summary>Parent row of each row that has one. Rows on a parent cycle get none; rows hanging off a cycle keep theirs.</summary>
    private Dictionary<int, int> TreeParents(List<int> keys)
    {
        var rowOf = new Dictionary<object, int>();
        foreach (var key in keys)
        {
            if (DataSet.GetValue(key, TreeKeyField!) is { } value && !EmptyTreeValue(value))
                rowOf.TryAdd(value, key);
        }

        var parents = new Dictionary<int, int>();
        foreach (var key in keys)
        {
            if (DataSet.GetValue(key, TreeParentField!) is { } value && !EmptyTreeValue(value) && rowOf.TryGetValue(value, out var parent))
                parents[key] = parent;
        }

        var done = new HashSet<int>();
        foreach (var key in keys)
        {
            if (done.Contains(key))
                continue;
            var path = new List<int>();
            var onPath = new HashSet<int>();
            var row = key;
            while (!done.Contains(row) && onPath.Add(row))
            {
                path.Add(row);
                if (!parents.TryGetValue(row, out var parent))
                    break;
                row = parent;
            }
            // the walk came back to a row of this path that still has a parent: from there on is the cycle
            if (onPath.Contains(row) && parents.ContainsKey(row))
            {
                foreach (var onCycle in path.Skip(path.IndexOf(row)))
                    parents.Remove(onCycle);
            }
            done.UnionWith(path);
        }
        return parents;
    }
}
