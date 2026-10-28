namespace Quro.Grid;

// 서버측 페이징 (R6, O-04: 화면별 설정, Addendum A-24). 앱이 조회 함수를 주면 그리드는 보이는 범위만 적재한다.

/// <summary>조회 요청. 시작 행은 PageSize의 배수, 건수는 PageSize.</summary>
public sealed class GridItemsRequest(int startIndex, int count, IReadOnlyList<SortColumn> sort,
    CancellationToken cancellationToken = default, IReadOnlyList<ColumnFilter>? filters = null)
{
    public int StartIndex { get; } = startIndex;
    public int Count { get; } = count;

    /// <summary>정렬 조건 (비어 있으면 기본 순서). 페이징 중 정렬은 서버가 한다.</summary>
    public IReadOnlyList<SortColumn> Sort { get; } = sort;

    /// <summary>필터 조건 (GS-073, A-31). 페이징 중 필터는 서버가 한다. 비어 있으면 전체</summary>
    public IReadOnlyList<ColumnFilter> Filters { get; } = filters ?? [];

    public CancellationToken CancellationToken { get; } = cancellationToken;
}

/// <summary>조회 결과.</summary>
/// <param name="items">요청 범위의 행</param>
/// <param name="totalCount">전체 건수</param>
/// <param name="totals">합계 행용 전체 합계 (필드 → 값). 그리드는 저장 전 변경분만큼 더하고 빼서 표시한다.</param>
public sealed class GridItemsResult<TItem>(IReadOnlyList<TItem> items, int totalCount,
    IReadOnlyDictionary<string, decimal>? totals = null)
{
    public IReadOnlyList<TItem> Items { get; } = items;
    public int TotalCount { get; } = totalCount;
    public IReadOnlyDictionary<string, decimal>? Totals { get; } = totals;
}
