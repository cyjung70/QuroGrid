namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-001: 조회 행의 수량을 바꾼 뒤 원래 값으로 되돌림 → 행 상태 Normal 복귀 (Spec 5.1, 4.4)
/// </summary>
public class GS_001_RevertValueTests
{
    private static (QuroDataSet<PoLine> ds, int rowKey) LoadOneLine()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load([new PoLine { LineNo = 1, ItemCode = "A1001", Qty = 10m, UnitPrice = 2500m, Version = 7 }]);
        return (ds, ds.RowKeys[0]);
    }

    [Fact]
    public void GS_001_RevertQty_ReturnsToNormal()
    {
        var (ds, row) = LoadOneLine();
        Assert.Equal(RowState.Normal, ds.GetState(row));

        ds.SetValue(row, nameof(PoLine.Qty), 15m);
        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.Equal(10m, ds.GetOriginal(row, nameof(PoLine.Qty)));

        ds.SetValue(row, nameof(PoLine.Qty), 10m);

        Assert.Equal(RowState.Normal, ds.GetState(row));
        Assert.Equal(10m, ds.GetValue(row, nameof(PoLine.Qty)));
        Assert.True(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public void GS_001_RevertOneOfTwoFields_StaysUpdated()
    {
        var (ds, row) = LoadOneLine();

        ds.SetValue(row, nameof(PoLine.Qty), 15m);
        ds.SetValue(row, nameof(PoLine.UnitPrice), 3000m);
        ds.SetValue(row, nameof(PoLine.Qty), 10m);

        // 모든 값이 원본과 같아져야 Normal. 단가가 아직 다르므로 Updated 유지.
        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.Single(ds.GetChanges().Updated);
    }
}
