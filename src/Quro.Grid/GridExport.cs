namespace Quro.Grid;

// 엑셀 내보내기 (GS-054, Addendum A-28). .xlsx는 저장 API 서버가 만든다.
// 그리드는 현재 화면의 컬럼 정보(사용자 언어 헤더·순서·너비·정렬)만 주고, 앱이 저장 API에 보내 파일을 받는다.

/// <summary>내보내기 컬럼 하나 (화면 순서).</summary>
/// <param name="Field">필드</param>
/// <param name="HeaderKey">헤더 리소스 키</param>
/// <param name="Header">사용자 언어로 바꾼 헤더 (리소스가 없으면 키)</param>
/// <param name="Type">셀 타입</param>
/// <param name="Format">표시 서식 (없으면 셀 타입 기본)</param>
/// <param name="Width">화면 너비(px). 사용자가 리사이즈했으면 그 너비</param>
/// <param name="CurrencyField">Currency 셀의 통화 코드 필드</param>
/// <param name="Aggregate">합계 행 집계</param>
/// <param name="Options">Combo 셀 목록 (코드 → 명칭)</param>
public sealed record ExportColumn(
    string Field,
    string HeaderKey,
    string Header,
    CellType Type,
    string? Format,
    int Width,
    string? CurrencyField,
    Aggregate Aggregate,
    IReadOnlyList<ComboOption>? Options);

/// <summary>내보내기 요청. 앱이 조회 조건과 함께 저장 API 서버에 보낸다.</summary>
/// <param name="Columns">내보낼 컬럼 (화면 순서, Button 셀 제외)</param>
/// <param name="Sort">현재 정렬 (비어 있으면 조회 순서)</param>
/// <param name="Culture">표시 로캘 이름 (예: ko-KR)</param>
public sealed record GridExportRequest(
    IReadOnlyList<ExportColumn> Columns,
    IReadOnlyList<SortColumn> Sort,
    string Culture);
