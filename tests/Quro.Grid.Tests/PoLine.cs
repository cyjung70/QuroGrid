using Quro.Grid.Data;

namespace Quro.Grid.Tests;

/// <summary>구매오더 라인 (Spec 4.4·4.5 예시). 컴포넌트 테스트 공용 모델.</summary>
public sealed class PoLine
{
    public int LineNo { get; set; }
    public string ItemCode { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "KRW";
    public DateOnly? DueDate { get; set; }
    public int Version { get; set; }
}

internal static class TestData
{
    /// <summary>조회된 구매오더 라인 3건 (수량 합계 18).</summary>
    public static QuroDataSet<PoLine> ThreeLines()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load(
        [
            new PoLine { LineNo = 1, ItemCode = "A1001", Qty = 10m, UnitPrice = 2500m, Version = 7 },
            new PoLine { LineNo = 2, ItemCode = "A1002", Qty = 5m, UnitPrice = 1200m, Version = 4 },
            new PoLine { LineNo = 3, ItemCode = "A1003", Qty = 3m, UnitPrice = 12.5m, CurrencyCode = "USD", Version = 1 },
        ]);
        return ds;
    }
}
