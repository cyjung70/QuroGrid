using System.Text.Json.Serialization;

namespace Quro.Grid;

// 엑셀 내보내기 (GS-054, Addendum A-28). .xlsx는 저장 API 서버가 만든다. CSV도 같다 (GS-079, A-34·A-43).
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
/// <param name="Mask">Mask 셀의 마스크 (A-56). 값은 입력 문자만이므로 저장 API가 고정 문자를 넣어 보인다</param>
public sealed record ExportColumn(
    string Field,
    string HeaderKey,
    string Header,
    CellType Type,
    string? Format,
    int Width,
    string? CurrencyField,
    Aggregate Aggregate,
    IReadOnlyList<ComboOption>? Options,
    string? Mask = null);

/// <summary>내보내기 요청. 앱이 조회 조건과 함께 저장 API 서버에 보낸다.</summary>
/// <param name="Columns">내보낼 컬럼 (화면 순서, Button 셀 제외)</param>
/// <param name="Sort">현재 정렬 (비어 있으면 조회 순서)</param>
/// <param name="Culture">표시 로캘 이름 (예: ko-KR)</param>
/// <param name="Filters">현재 필터 (GS-072, 비어 있으면 전체). 저장 API가 조회 조건과 함께 적용한다</param>
/// <param name="Format">만들 파일 형식 (GS-079). 기본 xlsx</param>
public sealed record GridExportRequest(
    IReadOnlyList<ExportColumn> Columns,
    IReadOnlyList<SortColumn> Sort,
    string Culture,
    IReadOnlyList<ColumnFilter> Filters,
    ExportFormat Format = ExportFormat.Xlsx);

/// <summary>
/// 내보내기 파일 형식 (GS-079, Addendum A-43). JSON에는 소문자(<c>"xlsx"</c>·<c>"csv"</c>)로 쓴다.
/// CSV의 구분자·인코딩·따옴표 처리는 저장 API가 정한다.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExportFormat>))]
public enum ExportFormat
{
    /// <summary>엑셀 (.xlsx)</summary>
    [JsonStringEnumMemberName("xlsx")]
    Xlsx,

    /// <summary>CSV (.csv)</summary>
    [JsonStringEnumMemberName("csv")]
    Csv,
}
