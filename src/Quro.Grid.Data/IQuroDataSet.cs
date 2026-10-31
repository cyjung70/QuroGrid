namespace Quro.Grid.Data;

/// <summary>
/// Read-only, non-generic view of a <see cref="QuroDataSet{T}"/>. Lets a component that doesn't know the row type read
/// another data set, such as a grid Combo column reading a code list (A-57).
/// </summary>
public interface IQuroDataSet
{
    /// <summary>Row keys in display order, Deleted rows included.</summary>
    IReadOnlyList<int> RowKeys { get; }

    /// <summary>Current value of a field in a row.</summary>
    object? GetValue(int rowKey, string field);

    /// <summary>Row state.</summary>
    RowState GetState(int rowKey);

    /// <summary>Metadata of one column. Throws <see cref="ArgumentException"/> for an unknown field.</summary>
    DataSetColumn GetColumn(string field);

    /// <summary>Raised when a value actually changes.</summary>
    event EventHandler<DataSetValueChangedEventArgs>? ValueChanged;

    /// <summary>Raised when a row enters the list.</summary>
    event EventHandler<DataSetRowEventArgs>? RowAdded;

    /// <summary>Raised when a row is deleted.</summary>
    event EventHandler<DataSetRowEventArgs>? RowDeleted;

    /// <summary>Raised after a load.</summary>
    event EventHandler? Loaded;
}
