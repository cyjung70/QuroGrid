namespace Quro.Grid;

// 헤더 필터 (GS-071~GS-073, P1, Addendum A-31). 메모리 모드는 그리드가 거르고, 페이징 모드는 조회 요청(GridItemsRequest.Filters)으로 서버가 거른다.

/// <summary>
/// 컬럼 하나의 필터 조건. 쓰는 항목은 컬럼 타입에 따라 다르다:
/// Text·Popup → <see cref="Contains"/>, Number·Currency → <see cref="Min"/>·<see cref="Max"/>,
/// Date → <see cref="From"/>·<see cref="To"/>, Combo·Check → <see cref="Values"/>.
/// 컬럼끼리는 AND, 범위는 양 끝 포함.
/// </summary>
public sealed record ColumnFilter(string Field)
{
    /// <summary>포함할 문자열 (대소문자 무시)</summary>
    public string? Contains { get; init; }

    /// <summary>최소 (포함)</summary>
    public decimal? Min { get; init; }

    /// <summary>최대 (포함)</summary>
    public decimal? Max { get; init; }

    /// <summary>시작일 (포함)</summary>
    public DateOnly? From { get; init; }

    /// <summary>종료일 (포함)</summary>
    public DateOnly? To { get; init; }

    /// <summary>고른 값: Combo는 코드, Check는 저장 값 (언어 무관 문자열)</summary>
    public IReadOnlyList<string>? Values { get; init; }
}

/// <summary>
/// 필터 패널에서 입력한 그대로의 문자열 (quro-grid.js → ApplyFilter). 숫자는 사용자 로캘로, 날짜는 ISO(yyyy-MM-dd)로 해석한다.
/// 모두 비어 있으면 그 컬럼의 필터를 푼다.
/// </summary>
public sealed record FilterInput(
    string? Text = null,
    string? Min = null,
    string? Max = null,
    string? From = null,
    string? To = null,
    IReadOnlyList<string>? Values = null);

/// <summary>사용자가 필터를 바꾼 뒤 (OnFilterChanged).</summary>
public sealed class FilterChangedArgs(IReadOnlyList<ColumnFilter> filters)
{
    /// <summary>현재 필터 (컬럼마다 하나, 적용한 순서)</summary>
    public IReadOnlyList<ColumnFilter> Filters { get; } = filters;
}
