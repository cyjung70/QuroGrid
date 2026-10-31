using System.Text.Json.Serialization;

namespace Quro.Grid;

/// <summary>셀 타입 (Spec 4.6, P0).</summary>
// JSON(저장 API 요청 등)에는 이름으로 쓴다 (GS-054)
[JsonConverter(typeof(JsonStringEnumConverter<CellType>))]
public enum CellType
{
    /// <summary>Free text.</summary>
    Text,
    /// <summary>Number shown with the column format.</summary>
    Number,
    /// <summary>Amount whose decimals follow the row currency.</summary>
    Currency,
    /// <summary>Date.</summary>
    Date,
    /// <summary>Value chosen from a list of options.</summary>
    Combo,
    /// <summary>Check box.</summary>
    Check,
    /// <summary>Code entered or picked with a lookup popup.</summary>
    Popup,
    /// <summary>Button that raises OnCellButtonClick.</summary>
    Button,
}

/// <summary>
/// Cell look returned by <see cref="QuroColumn.CellStyle"/> (A-59). A fixed set, not CSS: each tone is a class
/// <c>quro-tone-*</c> colored from the <c>--quro-tone-*</c> theme tokens.
/// </summary>
[Flags]
public enum CellTone
{
    /// <summary>Default look.</summary>
    None = 0,
    /// <summary>Red text (e.g. negative amount).</summary>
    Danger = 1,
    /// <summary>Green text.</summary>
    Success = 2,
    /// <summary>Grey text.</summary>
    Muted = 4,
    /// <summary>Light background.</summary>
    Highlight = 8,
    /// <summary>Bold text.</summary>
    Bold = 16,
}

/// <summary>합계 행 집계 (Spec 4.5).</summary>
// JSON(저장 API 요청 등)에는 이름으로 쓴다 (GS-054)
[JsonConverter(typeof(JsonStringEnumConverter<Aggregate>))]
public enum Aggregate
{
    /// <summary>No footer value.</summary>
    None,
    /// <summary>Sum of the column.</summary>
    Sum,
}

/// <summary>삭제 표시 행의 화면 처리 (O-02 잠정안: 기본 숨김).</summary>
public enum DeletedRowDisplay
{
    /// <summary>Deleted rows are hidden.</summary>
    Hide,
    /// <summary>Deleted rows are shown with a strikethrough.</summary>
    Strikethrough,
}

/// <summary>코드도움 요청 계기 (Addendum A-16).</summary>
public enum PopupTrigger
{
    /// <summary>Popup 셀 확정(Tab·Enter·셀 이탈) 시 조회 결과가 여러 건</summary>
    Commit,

    /// <summary>F4 키</summary>
    F4,
}

/// <summary>Combo 셀 목록 항목 (공통코드). <see cref="Text"/>는 화면 표시용 데이터(코드 명칭)다.</summary>
public sealed record ComboOption(object? Value, string Text);

/// <summary>Enter 확정 후 이동 방향 (O-05 결정: 기본 오른쪽).</summary>
public enum EnterDirection
{
    /// <summary>Move to the next cell on the right.</summary>
    Right,
    /// <summary>Move to the cell below.</summary>
    Down,
}
