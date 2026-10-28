namespace Quro.Grid.Data;

public enum CellMarkLevel
{
    /// <summary>경고 색 + 툴팁 (예: Minus 재고 허용 ON)</summary>
    Warning,

    /// <summary>오류 색 + 툴팁 (예: Minus 재고 허용 OFF)</summary>
    Error,
}

/// <summary>
/// 앱이 지정하는 셀 표시 (Spec 4.9, 2026-10-28 결정). 판정은 앱이 하고 그리드는 표시만 한다.
/// <see cref="MessageKey"/>는 툴팁에 쓰는 리소스 키.
/// </summary>
public sealed record CellMark(CellMarkLevel Level, string MessageKey);
