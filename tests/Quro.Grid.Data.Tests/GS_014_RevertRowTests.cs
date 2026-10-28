namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-014: RevertRow (Spec 4.4, O-01 잠정안 "행 단위 변경 취소").
/// Updated·Deleted는 원본으로 Normal 복귀, Inserted는 행 제거 (2026-10-28 결정).
/// </summary>
public class GS_014_RevertRowTests
{
    [Fact]
    public void GS_014_RevertUpdatedRow_RestoresOriginal()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), 99m);
        ds.SetValue(row, nameof(PoLine.UnitPrice), 1m);

        ds.RevertRow(row);

        Assert.Equal(RowState.Normal, ds.GetState(row));
        Assert.Equal(10m, ds.GetValue(row, nameof(PoLine.Qty)));
        Assert.Equal(2500m, ds.GetValue(row, nameof(PoLine.UnitPrice)));
        Assert.True(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public void GS_014_RevertDeletedRow_BecomesNormal()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[1];
        ds.SetValue(row, nameof(PoLine.Qty), 50m);
        ds.DeleteRow(row);

        ds.RevertRow(row);

        Assert.Equal(RowState.Normal, ds.GetState(row));
        Assert.Equal(5m, ds.GetValue(row, nameof(PoLine.Qty)));
        Assert.True(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public void GS_014_RevertInsertedRow_RemovesRow()
    {
        var ds = TestData.TwoLines();
        var loaded = ds.RowKeys.ToArray();
        var row = ds.AddRow();

        ds.RevertRow(row);

        Assert.Equal(loaded, ds.RowKeys);
        Assert.True(ds.GetChanges().IsEmpty);
    }
}
