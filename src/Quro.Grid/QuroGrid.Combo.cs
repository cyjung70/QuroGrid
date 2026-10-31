using Quro.Grid.Data;

namespace Quro.Grid;

// Combo lists: fixed Options or a code data set with linked combos (A-57, GS-094)
public partial class QuroGrid<TItem>
{
    /// <summary>The row's Combo list. A linked combo lists the codes of the row's parent value.</summary>
    internal IReadOnlyList<ComboOption>? OptionsOf(QuroColumn col, int rowKey) =>
        col.ListFor(col.ParentField is { } parent ? DataSet.GetValue(rowKey, parent) : null);

    /// <summary>Every code of the column (header filter values, export, texts without a row).</summary>
    private static IReadOnlyList<ComboOption>? AllOptions(QuroColumn col) => col.ListFor(null, all: true);

    private string OptionText(QuroColumn col, object? value, int? rowKey = null) =>
        (rowKey is { } row ? OptionsOf(col, row) : AllOptions(col))?.FirstOrDefault(o => Equals(o.Value, value))?.Text
        ?? value?.ToString() ?? "";

    /// <summary>A code data set changed: redraw every row (the shown texts may differ).</summary>
    void IColumnHost.CodesChanged()
    {
        _epoch++;
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>A linked combo's parent changed: clear the child when its code left the row's list (same undo step).</summary>
    private void OnDataSetValueChanged(object? sender, DataSetValueChangedEventArgs e)
    {
        foreach (var col in _columns)
        {
            if (col.ParentField is not { } parent || col.ReadOnly || !string.Equals(parent, e.Field, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = DataSet.GetValue(e.RowKey, col.Field);
            if (value is not null && OptionsOf(col, e.RowKey)?.Any(o => Equals(o.Value, value)) != true)
                DataSet.SetValue(e.RowKey, col.Field, null);
        }
    }

    /// <summary>A linked combo's ParentField must be a field of the grid's data set.</summary>
    private void CheckParentField(QuroColumn col)
    {
        if (col.OptionsSource is not null && col.ParentField is { } parent)
            DataSet.GetColumn(parent);   // ArgumentException for an unknown field
    }
}
