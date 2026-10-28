namespace Quro.Grid;

// 이벤트 인자 (Spec 4.10). 취소 가능한 이벤트는 Cancel을 true로 두면 동작을 막는다.

/// <summary>값 확정 직전 (OnCellValueChanging). 취소 가능.</summary>
public sealed class CellValueChangingArgs<TItem>(int rowKey, string field, TItem item, object? oldValue, object? newValue)
    where TItem : class
{
    public int RowKey { get; } = rowKey;
    public string Field { get; } = field;
    public TItem Item { get; } = item;
    public object? OldValue { get; } = oldValue;

    /// <summary>사용자 로캘로 해석한 새 값.</summary>
    public object? NewValue { get; } = newValue;

    public bool Cancel { get; set; }
}

/// <summary>값 확정 후 (OnCellValueChanged). 금액 재계산 등 연관 컬럼 갱신.</summary>
public sealed class CellValueChangedArgs<TItem>(int rowKey, string field, TItem item, object? oldValue, object? newValue)
    where TItem : class
{
    public int RowKey { get; } = rowKey;
    public string Field { get; } = field;
    public TItem Item { get; } = item;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
}

/// <summary>행 추가 전 (OnRowAdding). <see cref="Item"/>에 기본값을 넣는다.</summary>
public sealed class RowAddingArgs<TItem>(TItem item) where TItem : class
{
    public TItem Item { get; } = item;
}

/// <summary>행 추가 후 (OnRowAdded).</summary>
public sealed class RowAddedArgs<TItem>(int rowKey, TItem item) where TItem : class
{
    public int RowKey { get; } = rowKey;
    public TItem Item { get; } = item;
}

/// <summary>행 삭제 전 (OnRowDeleting). 행마다 발생하며 취소한 행만 남는다.</summary>
public sealed class RowDeletingArgs<TItem>(int rowKey, TItem item) where TItem : class
{
    public int RowKey { get; } = rowKey;
    public TItem Item { get; } = item;
    public bool Cancel { get; set; }
}

/// <summary>붙여넣기 직전 (OnPasting). 범위·데이터 확인과 취소.</summary>
public sealed class PastingArgs(int startRowKey, string startField, string[][] cells)
{
    public int StartRowKey { get; } = startRowKey;
    public string StartField { get; } = startField;

    /// <summary>클립보드 내용 (행 × 열, 엑셀 탭·줄바꿈 형식을 나눈 결과).</summary>
    public string[][] Cells { get; } = cells;

    public bool Cancel { get; set; }
}

/// <summary>붙여넣기 후 (OnPasted, Addendum A-04). 안내 표시는 앱이 정한다.</summary>
public sealed class PastedArgs(int startRowKey, string startField, IReadOnlyList<int> addedRowKeys, IReadOnlyList<PasteSkip> skipped)
{
    public int StartRowKey { get; } = startRowKey;
    public string StartField { get; } = startField;

    /// <summary>부족 행 자동 추가로 생긴 행 (AddRowsOnPaste).</summary>
    public IReadOnlyList<int> AddedRowKeys { get; } = addedRowKeys;

    /// <summary>반영하지 않은 셀과 사유 리소스 키 (<see cref="PasteKeys"/>).</summary>
    public IReadOnlyList<PasteSkip> Skipped { get; } = skipped;
}

/// <summary>붙여넣기에서 건너뛴 셀.</summary>
public sealed record PasteSkip(int RowKey, string Field, string MessageKey);

/// <summary>붙여넣기 건너뜀 사유 리소스 키 (Addendum 2절).</summary>
public static class PasteKeys
{
    public const string ReadOnly = "quro.paste.readonly";
    public const string Invalid = "quro.paste.invalid";
    public const string Cancelled = "quro.paste.cancelled";
}
