namespace Quro.Grid.Sample.Data;

/// <summary>
/// 대량 조회 샘플용 가짜 조회 API (GS-051·GS-052). 실제 앱은 REST → Dapper → PostgreSQL에서
/// OFFSET/LIMIT·ORDER BY·SUM으로 같은 일을 한다. 행은 매번 새 객체로 돌려준다 (서버 데이터와 분리).
/// </summary>
public static class LargePoLines
{
    public const int Count = 10_000;

    private static readonly PoLine[] Rows = Enumerable.Range(1, Count).Select(n =>
    {
        var qty = n % 50 + 1;
        var price = n % 7 * 100 + 500;
        return new PoLine { LineNo = n, ItemCode = $"A{n % 900 + 1000}", Qty = qty, UnitPrice = price, Amount = qty * price, Version = 1 };
    }).ToArray();

    private static readonly Dictionary<string, decimal> Totals = new()
    {
        ["Qty"] = Rows.Sum(r => r.Qty),
        ["Amount"] = Rows.Sum(r => r.Amount),
    };

    public static Task<GridItemsResult<PoLine>> QueryAsync(GridItemsRequest request)
    {
        IEnumerable<PoLine> query = Rows;
        IOrderedEnumerable<PoLine>? ordered = null;
        foreach (var sort in request.Sort)
        {
            Func<PoLine, object> key = sort.Field switch
            {
                nameof(PoLine.ItemCode) => r => r.ItemCode,
                nameof(PoLine.Qty) => r => r.Qty,
                nameof(PoLine.UnitPrice) => r => r.UnitPrice,
                nameof(PoLine.Amount) => r => r.Amount,
                _ => r => r.LineNo,
            };
            var ascending = sort.Direction == SortDirection.Ascending;
            ordered = ordered is null
                ? ascending ? query.OrderBy(key) : query.OrderByDescending(key)
                : ascending ? ordered.ThenBy(key) : ordered.ThenByDescending(key);
        }

        var page = (ordered ?? query).Skip(request.StartIndex).Take(request.Count).Select(Copy).ToList();
        return Task.FromResult(new GridItemsResult<PoLine>(page, Count, Totals));
    }

    private static PoLine Copy(PoLine r) => new()
    {
        LineNo = r.LineNo, ItemCode = r.ItemCode, Qty = r.Qty, UnitPrice = r.UnitPrice, Amount = r.Amount, Version = r.Version,
    };
}
