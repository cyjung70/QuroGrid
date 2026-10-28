namespace Quro.Grid.Data.Tests;

internal static class TestData
{
    /// <summary>조회된 구매오더 라인 2건을 적재한 DataSet.</summary>
    public static QuroDataSet<PoLine> TwoLines()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load(
        [
            new PoLine { LineNo = 1, ItemCode = "A1001", Qty = 10m, UnitPrice = 2500m, Version = 7 },
            new PoLine { LineNo = 2, ItemCode = "A1002", Qty = 5m, UnitPrice = 1200m, Version = 4 },
        ]);
        return ds;
    }
}
