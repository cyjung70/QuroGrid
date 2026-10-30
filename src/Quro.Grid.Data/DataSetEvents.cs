namespace Quro.Grid.Data;

/// <summary>현재 행이 바뀜 (Addendum A-51). 행 키가 null이면 현재 행 없음.</summary>
public sealed class CurrentRowChangedEventArgs(int? oldRowKey, int? newRowKey) : EventArgs
{
    /// <summary>Previous current row, or null.</summary>
    public int? OldRowKey { get; } = oldRowKey;

    /// <summary>New current row, or null.</summary>
    public int? NewRowKey { get; } = newRowKey;
}

/// <summary>셀 값이 바뀜 (Addendum A-51). 되돌리기(RevertRow·RejectChanges·Undo·Redo)로 바뀐 값도 포함한다.</summary>
public sealed class DataSetValueChangedEventArgs(int rowKey, string field, object? oldValue, object? newValue) : EventArgs
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;

    /// <summary>Field (property) name as declared on the row type.</summary>
    public string Field { get; } = field;

    /// <summary>Value before the change.</summary>
    public object? OldValue { get; } = oldValue;

    /// <summary>Value after the change.</summary>
    public object? NewValue { get; } = newValue;
}

/// <summary>행이 목록에 들어오거나(RowAdded) 삭제됨(RowDeleted) (Addendum A-51).</summary>
public sealed class DataSetRowEventArgs(int rowKey) : EventArgs
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
}

/// <summary>저장 성공 (Addendum A-51). <see cref="QuroDataSet{T}.AcceptChanges"/> 뒤에 발생한다.</summary>
public sealed class DataSetSavedEventArgs(SaveResult result) : EventArgs
{
    /// <summary>Result returned by SaveAsync.</summary>
    public SaveResult Result { get; } = result;
}
