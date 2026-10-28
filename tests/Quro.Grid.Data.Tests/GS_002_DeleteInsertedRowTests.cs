namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-002: 추가한 행을 저장 전에 삭제 → 목록에서 제거, 변경분에 없음 (Spec 5.1, 4.4)
/// </summary>
public class GS_002_DeleteInsertedRowTests
{
    [Fact]
    public void GS_002_DeleteInsertedRow_NotInChanges()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load([]);

        var row = ds.AddRow();
        Assert.Equal(RowState.Inserted, ds.GetState(row));

        // 값을 바꿔도 Inserted 유지
        ds.SetValue(row, nameof(PoLine.ItemCode), "A1001");
        ds.SetValue(row, nameof(PoLine.Qty), 10m);
        Assert.Equal(RowState.Inserted, ds.GetState(row));

        ds.DeleteRow(row);

        Assert.DoesNotContain(row, ds.RowKeys);
        var changes = ds.GetChanges();
        Assert.Empty(changes.Inserted);
        Assert.Empty(changes.Deleted);
        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void GS_002_DeleteInsertedRow_KeepsOtherRows()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load(
        [
            new PoLine { LineNo = 1, ItemCode = "A1001", Qty = 10m, Version = 3 },
            new PoLine { LineNo = 2, ItemCode = "A1002", Qty = 5m, Version = 4 },
        ]);
        var loaded = ds.RowKeys.ToArray();

        var row = ds.AddRow();
        ds.DeleteRow(row);

        Assert.Equal(loaded, ds.RowKeys);
        Assert.All(loaded, k => Assert.Equal(RowState.Normal, ds.GetState(k)));
        Assert.True(ds.GetChanges().IsEmpty);
    }
}
