namespace Quro.Grid.Data.Tests;

/// <summary>GS-011 ~ GS-013: 행 상태 전이 (Spec 4.4)</summary>
public class GS_011_013_RowStateTests
{
    /// <summary>GS-011: Normal 행의 값 변경 → Updated, GetOriginal은 원래 값</summary>
    [Fact]
    public void GS_011_ChangeNormalRow_BecomesUpdated_KeepsOriginal()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        ds.SetValue(row, nameof(PoLine.ItemCode), "B2001");

        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.Equal("B2001", ds.GetValue(row, nameof(PoLine.ItemCode)));
        Assert.Equal("A1001", ds.GetOriginal(row, nameof(PoLine.ItemCode)));
        Assert.Equal(RowState.Normal, ds.GetState(ds.RowKeys[1]));
    }

    /// <summary>GS-012: Inserted 행의 값 변경 → Inserted 유지</summary>
    [Fact]
    public void GS_012_ChangeInsertedRow_StaysInserted()
    {
        var ds = TestData.TwoLines();
        var row = ds.AddRow();

        ds.SetValue(row, nameof(PoLine.ItemCode), "C3001");
        ds.SetValue(row, nameof(PoLine.Qty), 3m);

        Assert.Equal(RowState.Inserted, ds.GetState(row));
        var inserted = Assert.Single(ds.GetChanges().Inserted);
        Assert.Equal("C3001", inserted.Item.ItemCode);
    }

    /// <summary>GS-013: Normal 행 삭제 → Deleted, 변경분 deleted[]에 포함</summary>
    [Fact]
    public void GS_013_DeleteNormalRow_MarkedDeleted_InChanges()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[1];

        ds.DeleteRow(row);

        Assert.Equal(RowState.Deleted, ds.GetState(row));
        var changes = ds.GetChanges();
        var deleted = Assert.Single(changes.Deleted);
        Assert.Equal(2, deleted.Item.LineNo);
        Assert.Empty(changes.Inserted);
        Assert.Empty(changes.Updated);
    }
}
