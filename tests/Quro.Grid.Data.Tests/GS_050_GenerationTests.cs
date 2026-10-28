namespace Quro.Grid.Data.Tests;

/// <summary>GS-050 (DataSet 쪽): 다시 조회(Load)했는지 화면이 알 수 있게 세대 번호를 둔다 (정렬 재적용, Addendum A-22).</summary>
public class GS_050_GenerationTests
{
    [Fact]
    public void GS_050_Generation_ChangesOnLoadOnly()
    {
        var ds = TestData.TwoLines();
        var loaded = ds.Generation;

        ds.SetValue(ds.RowKeys[0], "Qty", 99m);
        ds.AddRow();
        ds.AcceptChanges();
        Assert.Equal(loaded, ds.Generation);

        ds.Load([]);
        Assert.NotEqual(loaded, ds.Generation);
    }
}
