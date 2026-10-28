namespace Quro.Grid.Data.Tests;

/// <summary>GS-047 (DataSet 쪽): 행 복사 — 원래 행 바로 아래에 새 행 (Addendum A-12).</summary>
public class GS_047_RowCopyTests
{
    [Fact]
    public void GS_047_AddRowAfter_InsertsDirectlyBelow()
    {
        var ds = TestData.TwoLines();
        var (a, b) = (ds.RowKeys[0], ds.RowKeys[1]);

        var row = ds.AddRow(new PoLine { ItemCode = "N" }, afterRowKey: a);

        Assert.Equal([a, row, b], ds.RowKeys);
        Assert.Equal(RowState.Inserted, ds.GetState(row));
    }

    [Fact]
    public void GS_047_AddRowWithoutPosition_AppendsAtEnd()
    {
        var ds = TestData.TwoLines();

        var row = ds.AddRow();

        Assert.Equal(row, ds.RowKeys[^1]);
    }

    [Fact]
    public void GS_047_CopyOf_SameValues_NewInstance()
    {
        var ds = TestData.TwoLines();
        var source = ds.RowKeys[0];
        ds.SetValue(source, nameof(PoLine.Qty), 12m);

        var copy = ds.CopyOf(source);

        Assert.NotSame(ds.GetItem(source), copy);
        Assert.Equal("A1001", copy.ItemCode);
        Assert.Equal(12m, copy.Qty);          // 현재 값 (원본값이 아님)
        Assert.Equal(7, copy.Version);
    }
}
