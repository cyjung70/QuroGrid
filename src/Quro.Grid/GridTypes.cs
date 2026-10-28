using System.Text.Json.Serialization;

namespace Quro.Grid;

/// <summary>셀 타입 (Spec 4.6, P0).</summary>
// JSON(저장 API 요청 등)에는 이름으로 쓴다 (GS-054)
[JsonConverter(typeof(JsonStringEnumConverter<CellType>))]
public enum CellType
{
    Text,
    Number,
    Currency,
    Date,
    Combo,
    Check,
    Popup,
    Button,
}

/// <summary>합계 행 집계 (Spec 4.5).</summary>
// JSON(저장 API 요청 등)에는 이름으로 쓴다 (GS-054)
[JsonConverter(typeof(JsonStringEnumConverter<Aggregate>))]
public enum Aggregate
{
    None,
    Sum,
}

/// <summary>삭제 표시 행의 화면 처리 (O-02 잠정안: 기본 숨김).</summary>
public enum DeletedRowDisplay
{
    Hide,
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
    Right,
    Down,
}
