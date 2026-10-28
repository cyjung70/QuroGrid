namespace Quro.Grid.Data.Tests;

/// <summary>GS-048 · GS-049 (DataSet 쪽): 미저장 변경 여부 — 이탈 경고·연결 끊김 경고의 기준 (R7).</summary>
public class GS_048_049_HasChangesTests
{
    [Fact]
    public void GS_049_HasChanges_FalseAfterLoad()
    {
        Assert.False(TestData.TwoLines().HasChanges);
    }

    [Fact]
    public void GS_049_HasChanges_TrueOnUpdate_FalseWhenValueRestored()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        var original = ds.GetValue(row, "Qty");

        ds.SetValue(row, "Qty", 99m);
        Assert.True(ds.HasChanges);

        ds.SetValue(row, "Qty", original);   // Updated → Normal 복귀
        Assert.False(ds.HasChanges);
    }

    [Fact]
    public void GS_049_HasChanges_InsertAndDelete()
    {
        var ds = TestData.TwoLines();

        var added = ds.AddRow();
        Assert.True(ds.HasChanges);

        ds.DeleteRow(added);                 // Inserted 행 삭제 = 목록에서 제거
        Assert.False(ds.HasChanges);

        ds.DeleteRow(ds.RowKeys[0]);         // 조회된 행 삭제 = Deleted
        Assert.True(ds.HasChanges);
    }

    [Fact]
    public void GS_049_HasChanges_FalseAfterAcceptOrReject()
    {
        var ds = TestData.TwoLines();
        ds.SetValue(ds.RowKeys[0], "Qty", 99m);
        ds.AcceptChanges();
        Assert.False(ds.HasChanges);

        ds.SetValue(ds.RowKeys[0], "Qty", 1m);
        ds.RejectChanges();
        Assert.False(ds.HasChanges);
    }
}
