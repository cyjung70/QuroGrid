using System.Text.Json.Serialization;

namespace Quro.Grid;

// 이벤트 인자 (Spec 4.10). 취소 가능한 이벤트는 Cancel을 true로 두면 동작을 막는다.

/// <summary>값 확정 직전 (OnCellValueChanging). 취소 가능.</summary>
public sealed class CellValueChangingArgs<TItem>(int rowKey, string field, TItem item, object? oldValue, object? newValue)
    where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Field of the edited cell.</summary>
    public string Field { get; } = field;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
    /// <summary>Value before the change.</summary>
    public object? OldValue { get; } = oldValue;

    /// <summary>사용자 로캘로 해석한 새 값.</summary>
    public object? NewValue { get; } = newValue;

    /// <summary>True to keep the old value.</summary>
    public bool Cancel { get; set; }
}

/// <summary>값 확정 후 (OnCellValueChanged). 금액 재계산 등 연관 컬럼 갱신.</summary>
public sealed class CellValueChangedArgs<TItem>(int rowKey, string field, TItem item, object? oldValue, object? newValue)
    where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Field of the edited cell.</summary>
    public string Field { get; } = field;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
    /// <summary>Value before the change.</summary>
    public object? OldValue { get; } = oldValue;
    /// <summary>Committed value.</summary>
    public object? NewValue { get; } = newValue;
}

/// <summary>행 추가 전 (OnRowAdding). <see cref="Item"/>에 기본값을 넣는다.</summary>
public sealed class RowAddingArgs<TItem>(TItem item, int? sourceRowKey = null) where TItem : class
{
    /// <summary>New row item.</summary>
    public TItem Item { get; } = item;

    /// <summary>행 복사(A-12)면 원본 행 키. 앱은 여기서 키·버전 등 복사하면 안 되는 값을 정리한다.</summary>
    public int? SourceRowKey { get; } = sourceRowKey;
}

/// <summary>
/// 코드도움 요청 (OnPopupRequested, Spec 4.10 · Addendum A-16). 앱은 팝업(다이얼로그 등)을 띄우고
/// 고른 코드를 <see cref="Value"/>에 넣는다. null이면 확정하지 않고 편집을 유지한다.
/// </summary>
public sealed class PopupRequestedArgs<TItem>(int rowKey, string field, TItem item, string text,
    IReadOnlyList<string> matches, PopupTrigger trigger) where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Field of the popup cell.</summary>
    public string Field { get; } = field;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;

    /// <summary>입력 문자열</summary>
    public string Text { get; } = text;

    /// <summary>컬럼 Lookup 결과 (Lookup이 없으면 빈 목록)</summary>
    public IReadOnlyList<string> Matches { get; } = matches;

    /// <summary>What opened the request.</summary>
    public PopupTrigger Trigger { get; } = trigger;

    /// <summary>앱이 고른 코드. null이면 확정하지 않는다.</summary>
    public string? Value { get; set; }
}

/// <summary>Button 셀 클릭 (OnCellButtonClick, Addendum A-19).</summary>
public sealed class CellButtonClickArgs<TItem>(int rowKey, string field, TItem item) where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Field of the button column.</summary>
    public string Field { get; } = field;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
}

/// <summary>Image cell click — mouse, Enter or Space (OnCellClick, A-56).</summary>
public sealed class CellClickArgs<TItem>(int rowKey, string field, TItem item, object? value) where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Field of the clicked column.</summary>
    public string Field { get; } = field;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
    /// <summary>Cell value (the image URL).</summary>
    public object? Value { get; } = value;
}

/// <summary>활성 행 변경 (OnSelectionChanged, Addendum A-13).</summary>
public sealed class SelectionChangedArgs<TItem>(int rowKey, TItem item) where TItem : class
{
    /// <summary>Row key of the new active row.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
}

/// <summary>행 추가 후 (OnRowAdded).</summary>
public sealed class RowAddedArgs<TItem>(int rowKey, TItem item) where TItem : class
{
    /// <summary>Row key of the new row (negative until saved).</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
}

/// <summary>행 삭제 후 (OnRowDeleted, Addendum A-14). 삭제 호출 1회에 한 번, 실제 삭제된 행 키(취소 행 제외).</summary>
public sealed class RowDeletedArgs(IReadOnlyList<int> rowKeys)
{
    /// <summary>Keys of the deleted rows.</summary>
    public IReadOnlyList<int> RowKeys { get; } = rowKeys;
}

/// <summary>행 삭제 전 (OnRowDeleting). 행마다 발생하며 취소한 행만 남는다.</summary>
public sealed class RowDeletingArgs<TItem>(int rowKey, TItem item) where TItem : class
{
    /// <summary>Row key.</summary>
    public int RowKey { get; } = rowKey;
    /// <summary>Row item.</summary>
    public TItem Item { get; } = item;
    /// <summary>True to keep the row.</summary>
    public bool Cancel { get; set; }
}

/// <summary>붙여넣기 직전 (OnPasting). 범위·데이터 확인과 취소.</summary>
public sealed class PastingArgs(int startRowKey, string startField, string[][] cells)
{
    /// <summary>Row key of the top-left target cell.</summary>
    public int StartRowKey { get; } = startRowKey;
    /// <summary>Field of the top-left target cell.</summary>
    public string StartField { get; } = startField;

    /// <summary>클립보드 내용 (행 × 열, 엑셀 탭·줄바꿈 형식을 나눈 결과).</summary>
    public string[][] Cells { get; } = cells;

    /// <summary>True to skip the paste.</summary>
    public bool Cancel { get; set; }
}

/// <summary>붙여넣기 후 (OnPasted, Addendum A-04). 안내 표시는 앱이 정한다.</summary>
public sealed class PastedArgs(int startRowKey, string startField, IReadOnlyList<int> addedRowKeys, IReadOnlyList<PasteSkip> skipped)
{
    /// <summary>Row key of the top-left target cell.</summary>
    public int StartRowKey { get; } = startRowKey;
    /// <summary>Field of the top-left target cell.</summary>
    public string StartField { get; } = startField;

    /// <summary>부족 행 자동 추가로 생긴 행 (AddRowsOnPaste).</summary>
    public IReadOnlyList<int> AddedRowKeys { get; } = addedRowKeys;

    /// <summary>반영하지 않은 셀과 사유 리소스 키 (<see cref="PasteKeys"/>).</summary>
    public IReadOnlyList<PasteSkip> Skipped { get; } = skipped;
}

/// <summary>붙여넣기에서 건너뛴 셀.</summary>
public sealed record PasteSkip(int RowKey, string Field, string MessageKey);

/// <summary>Result of <see cref="QuroGrid{TItem}.ImportRows"/> (G-05).</summary>
/// <param name="AddedRowKeys">Added rows, in the order of the imported rows</param>
/// <param name="Skipped">Cells that were not set and why (<see cref="PasteKeys"/>)</param>
public sealed record ImportResult(IReadOnlyList<int> AddedRowKeys, IReadOnlyList<PasteSkip> Skipped);

/// <summary>붙여넣기 건너뜀 사유 리소스 키 (Addendum 2절).</summary>
public static class PasteKeys
{
    /// <summary>The cell is read-only.</summary>
    public const string ReadOnly = "quro.paste.readonly";
    /// <summary>The value cannot be parsed for the cell type.</summary>
    public const string Invalid = "quro.paste.invalid";
    /// <summary>OnCellValueChanging cancelled the value.</summary>
    public const string Cancelled = "quro.paste.cancelled";
}

/// <summary>그리드가 직접 표시하는 안내의 리소스 키 (R7, Addendum A-20·A-21). 앱의 Localizer에 문구를 둔다.</summary>
public static class GridKeys
{
    /// <summary>연결 끊김 중: 입력 잠금 안내</summary>
    public const string ConnectionOffline = "quro.connection.offline";

    /// <summary>연결 끊김 중 미저장 변경이 있을 때 덧붙이는 경고</summary>
    public const string ConnectionUnsaved = "quro.connection.unsaved";

    /// <summary>회로가 바뀌어(세션 소멸) 미저장 변경이 사라졌음을 다시 연 화면에서 알림</summary>
    public const string ChangesLost = "quro.connection.changes_lost";

    /// <summary>미저장 변경이 있는 채로 앱 안 화면 이동 시 확인</summary>
    public const string LeaveUnsaved = "quro.leave.unsaved";

    // 헤더 필터 패널 (A-31)

    /// <summary>헤더의 필터 버튼 (스크린리더 이름)</summary>
    public const string FilterOpen = "quro.filter.open";
    /// <summary>필터 패널의 적용 버튼</summary>
    public const string FilterApply = "quro.filter.apply";
    /// <summary>필터 패널의 해제 버튼</summary>
    public const string FilterClear = "quro.filter.clear";

    /// <summary>텍스트 입력 이름 (포함)</summary>
    public const string FilterContains = "quro.filter.contains";
    /// <summary>숫자 최소 입력 이름</summary>
    public const string FilterMin = "quro.filter.min";
    /// <summary>숫자 최대 입력 이름</summary>
    public const string FilterMax = "quro.filter.max";
    /// <summary>시작일 입력 이름</summary>
    public const string FilterFrom = "quro.filter.from";
    /// <summary>종료일 입력 이름</summary>
    public const string FilterTo = "quro.filter.to";

    /// <summary>Check 컬럼 값 목록의 체크·해제</summary>
    public const string FilterChecked = "quro.filter.checked";

    /// <summary>Check 컬럼 값 목록의 해제 (그룹 머리 행 값에도 쓴다)</summary>
    public const string FilterUnchecked = "quro.filter.unchecked";

    // 그룹핑 (A-42)

    /// <summary>그룹 머리 행: {0} 컬럼 헤더, {1} 값, {2} 건수</summary>
    public const string GroupHeader = "quro.group.header";

    /// <summary>소계 행의 첫 칸</summary>
    public const string GroupSubtotal = "quro.group.subtotal";

    /// <summary>빈 값 그룹</summary>
    public const string GroupEmpty = "quro.group.empty";
}

/// <summary>
/// 미저장 변경이 있는 채로 앱 안 화면 이동 (OnLeaving, Addendum A-20). 취소 가능.
/// 구독하면 그리드 기본 확인창 대신 앱이 묻는다 (예: 앱의 다이얼로그).
/// </summary>
public sealed class LeavingArgs(string targetLocation)
{
    /// <summary>Address the user is navigating to.</summary>
    public string TargetLocation { get; } = targetLocation;

    /// <summary>true면 이동하지 않는다.</summary>
    public bool Cancel { get; set; }
}

/// <summary>정렬 방향 (Addendum A-22).</summary>
// JSON(저장 API 요청 등)에는 이름으로 쓴다 (GS-054)
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    /// <summary>Ascending.</summary>
    Ascending,
    /// <summary>Descending.</summary>
    Descending,
}

/// <summary>정렬 조건 하나. 목록 순서가 1차·2차… 정렬 순서다.</summary>
public sealed record SortColumn(string Field, SortDirection Direction);

/// <summary>사용자가 헤더를 눌러 정렬이 바뀐 뒤 (OnSortChanged, Addendum A-22). 비어 있으면 정렬 해제.</summary>
public sealed class SortChangedArgs(IReadOnlyList<SortColumn> sort)
{
    /// <summary>New sort, in priority order.</summary>
    public IReadOnlyList<SortColumn> Sort { get; } = sort;
}

/// <summary>컬럼 경계 드래그가 끝난 뒤 (OnColumnResized, Addendum A-23). 레이아웃 저장(P1)에 쓴다.</summary>
public sealed class ColumnResizedArgs(string field, int width)
{
    /// <summary>Field of the resized column.</summary>
    public string Field { get; } = field;

    /// <summary>새 너비 (px, 최소 40)</summary>
    public int Width { get; } = width;
}

/// <summary>헤더 드래그로 컬럼을 옮긴 뒤 (OnColumnMoved, GS-068). 레이아웃 저장에 쓴다.</summary>
public sealed class ColumnMovedArgs(string field, int index)
{
    /// <summary>Field of the moved column.</summary>
    public string Field { get; } = field;

    /// <summary>옮긴 뒤 화면 위치 (보이는 컬럼 중 0부터)</summary>
    public int Index { get; } = index;
}
