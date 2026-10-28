namespace Quro.Grid.Data.Tests;

/// <summary>GS-026 (DataSet 쪽): 행 리비전은 그 행이 바뀔 때만 증가한다 (R4).</summary>
public class GS_026_RowRevisionTests
{
    [Fact]
    public void GS_026_SetValue_BumpsOnlyThatRow()
    {
        var ds = TestData.TwoLines();
        var (a, b) = (ds.RowKeys[0], ds.RowKeys[1]);
        var (ra, rb) = (ds.GetRevision(a), ds.GetRevision(b));

        ds.SetValue(a, nameof(PoLine.Qty), 11m);

        Assert.True(ds.GetRevision(a) > ra);
        Assert.Equal(rb, ds.GetRevision(b));
    }

    [Fact]
    public void GS_026_MarkDeleteRevert_BumpRevision()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        var r0 = ds.GetRevision(row);
        ds.SetCellMark(row, nameof(PoLine.Qty), new CellMark(CellMarkLevel.Warning, "k"));
        var r1 = ds.GetRevision(row);
        ds.DeleteRow(row);
        var r2 = ds.GetRevision(row);
        ds.RevertRow(row);
        var r3 = ds.GetRevision(row);

        Assert.True(r0 < r1 && r1 < r2 && r2 < r3);
    }
}
