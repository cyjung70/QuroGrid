namespace Quro.Grid.Data.Tests;

/// <summary>GS-015 ~ GS-017: 변경분 취소·추출·확정 (Spec 4.4)</summary>
public class GS_015_017_ChangesTests
{
    /// <summary>추가 1건, 수정 1건(라인 1), 삭제 1건(라인 2)</summary>
    private static (QuroDataSet<PoLine> ds, int updated, int deleted, int inserted) MixedChanges()
    {
        var ds = TestData.TwoLines();
        var updated = ds.RowKeys[0];
        var deleted = ds.RowKeys[1];

        ds.SetValue(updated, nameof(PoLine.Qty), 15m);
        ds.DeleteRow(deleted);
        var inserted = ds.AddRow();
        ds.SetValue(inserted, nameof(PoLine.ItemCode), "A1003");
        ds.SetValue(inserted, nameof(PoLine.Qty), 1m);

        return (ds, updated, deleted, inserted);
    }

    /// <summary>GS-015: 여러 행 변경 후 RejectChanges → 전체 원복, 추가 행 제거, 삭제 행 복귀</summary>
    [Fact]
    public void GS_015_RejectChanges_RestoresAll()
    {
        var (ds, updated, deleted, inserted) = MixedChanges();

        ds.RejectChanges();

        Assert.Equal([updated, deleted], ds.RowKeys);
        Assert.DoesNotContain(inserted, ds.RowKeys);
        Assert.Equal(RowState.Normal, ds.GetState(updated));
        Assert.Equal(RowState.Normal, ds.GetState(deleted));
        Assert.Equal(10m, ds.GetValue(updated, nameof(PoLine.Qty)));
        Assert.True(ds.GetChanges().IsEmpty);
    }

    /// <summary>GS-016: 섞인 변경분 → 상태별 분류, 수정·삭제 행에 version, 추가 행은 음수 임시 키</summary>
    [Fact]
    public void GS_016_GetChanges_Mixed_GroupsByState()
    {
        var (ds, updated, deleted, inserted) = MixedChanges();

        var changes = ds.GetChanges();

        var u = Assert.Single(changes.Updated);
        Assert.Equal(updated, u.RowKey);
        Assert.Equal(7, u.Item.Version);
        Assert.Equal(15m, u.Item.Qty);

        var d = Assert.Single(changes.Deleted);
        Assert.Equal(deleted, d.RowKey);
        Assert.Equal(4, d.Item.Version);

        var i = Assert.Single(changes.Inserted);
        Assert.Equal(inserted, i.RowKey);
        Assert.True(i.RowKey < 0, "추가 행의 임시 키는 음수");
        Assert.Equal("A1003", i.Item.ItemCode);
    }

    /// <summary>GS-017: AcceptChanges → 모든 행 Normal, 원본값 갱신, Deleted 행 제거</summary>
    [Fact]
    public void GS_017_AcceptChanges_CommitsAll()
    {
        var (ds, updated, deleted, inserted) = MixedChanges();

        ds.AcceptChanges();

        Assert.Equal([updated, inserted], ds.RowKeys);
        Assert.All(ds.RowKeys, k => Assert.Equal(RowState.Normal, ds.GetState(k)));
        Assert.Equal(15m, ds.GetOriginal(updated, nameof(PoLine.Qty)));
        Assert.Equal("A1003", ds.GetOriginal(inserted, nameof(PoLine.ItemCode)));
        Assert.True(ds.GetChanges().IsEmpty);

        // 확정 후 다시 원래 값으로 바꾸면 이제는 Updated
        ds.SetValue(updated, nameof(PoLine.Qty), 10m);
        Assert.Equal(RowState.Updated, ds.GetState(updated));
    }
}
