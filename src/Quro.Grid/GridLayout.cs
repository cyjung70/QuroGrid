namespace Quro.Grid;

// 사용자별 레이아웃 (GS-069, P1). 그리드는 꺼내고 적용만 한다.
// 저장 위치(사용자 × 화면 × 그리드)는 앱·저장 API가 정한다 (JSON으로 직렬화해 보관).

/// <summary>레이아웃의 컬럼 하나 (화면 순서, 숨긴 컬럼 포함).</summary>
/// <param name="Field">필드</param>
/// <param name="Width">사용자가 리사이즈한 너비(px). 리사이즈하지 않았으면 null (컬럼 정의 너비를 따른다)</param>
/// <param name="Hidden">숨김</param>
public sealed record LayoutColumn(string Field, int? Width, bool Hidden);

/// <summary>그리드 레이아웃: 컬럼 순서·너비·숨김과 정렬.</summary>
/// <param name="Columns">컬럼 (화면 순서, 숨긴 컬럼은 제자리)</param>
/// <param name="Sort">정렬 (비어 있으면 조회 순서)</param>
public sealed record GridLayout(IReadOnlyList<LayoutColumn> Columns, IReadOnlyList<SortColumn> Sort);
