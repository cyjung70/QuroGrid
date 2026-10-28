namespace Quro.Grid;

/// <summary>셀 타입 (Spec 4.6, P0).</summary>
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
