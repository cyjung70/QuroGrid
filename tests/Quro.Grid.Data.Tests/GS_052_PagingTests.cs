namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-052 (DataSet 쪽): 서버측 페이징 (R6). 화면에 필요한 페이지만 적재하고, 벗어난 페이지는 놓되 변경 행은 남긴다.
/// 같은 행(행 ID)은 다시 적재해도 같은 행 키, 변경 행은 다시 적재해도 변경 값 유지 (Addendum A-24).
/// </summary>
public class GS_052_PagingTests
{
    private static QuroDataSet<PoLine> Paged() => new(line => line.LineNo);

    private static PoLine[] Page(int from, int count) =>
        Enumerable.Range(from, count).Select(n => new PoLine { LineNo = n, ItemCode = $"I{n}", Qty = n, Version = 1 }).ToArray();

    [Fact]
    public void GS_052_LoadPage_SameRowKeepsKey()
    {
        var ds = Paged();

        var first = ds.LoadPage(Page(1, 3));
        var second = ds.LoadPage(Page(3, 3));     // 3번 행이 겹친다

        Assert.Equal(3, first.Count);
        Assert.Equal(first[2], second[0]);
        Assert.Equal(5, ds.RowKeys.Count);
        Assert.All(ds.RowKeys, k => Assert.Equal(RowState.Normal, ds.GetState(k)));
    }

    [Fact]
    public void GS_052_Release_DropsUnchanged_KeepsChanged()
    {
        var ds = Paged();
        var keys = ds.LoadPage(Page(1, 4));
        ds.SetValue(keys[0], "Qty", 99m);         // Updated
        ds.DeleteRow(keys[1]);                    // Deleted
        var added = ds.AddRow();                  // Inserted

        ds.Release(keys);

        Assert.Equal([keys[0], keys[1], added], ds.RowKeys);
        Assert.Equal(1, ds.GetChanges().Updated.Count);
        Assert.Equal(1, ds.GetChanges().Deleted.Count);
    }

    /// <summary>놓았다가 다시 적재한 행도 같은 행 키 (화면의 활성 셀·선택이 행 키로 이어진다)</summary>
    [Fact]
    public void GS_052_ReleasedRow_ReloadsWithSameKey()
    {
        var ds = Paged();
        var keys = ds.LoadPage(Page(1, 3));
        ds.Release(keys);
        Assert.Empty(ds.RowKeys);

        var again = ds.LoadPage(Page(1, 3));

        Assert.Equal(keys, again);
        Assert.Equal(RowState.Normal, ds.GetState(again[0]));
    }

    [Fact]
    public void GS_052_ReloadedChangedRow_KeepsChange()
    {
        var ds = Paged();
        var key = ds.LoadPage(Page(1, 2))[0];
        ds.SetValue(key, "Qty", 99m);
        ds.Release(ds.RowKeys);

        var again = ds.LoadPage(Page(1, 2));      // 서버는 원래 값(1)을 돌려준다

        Assert.Equal(key, again[0]);
        Assert.Equal(99m, ds.GetValue(key, "Qty"));
        Assert.Equal(1m, ds.GetOriginal(key, "Qty"));
        Assert.Equal(RowState.Updated, ds.GetState(key));
        Assert.Equal(2, ds.RowKeys.Count);        // 변경 행(1번) + 다시 적재한 2번 행
    }

    /// <summary>10,000행을 100행씩 훑어도 DataSet에는 적재 범위(2페이지) + 변경 행만 남는다</summary>
    [Fact]
    public void GS_052_ScrollThrough10000_HoldsWindowPlusChanges()
    {
        var ds = Paged();
        IReadOnlyList<int>? previous = null;
        var changed = new List<int>();

        for (var page = 0; page < 100; page++)
        {
            var keys = ds.LoadPage(Page(page * 100 + 1, 100));
            if (page % 10 == 0)
            {
                ds.SetValue(keys[5], "Qty", -1m);
                changed.Add(keys[5]);
            }
            if (previous is not null)
                ds.Release(previous.Except(keys).ToList());   // 바로 앞 페이지는 놓는다 (창 = 현재 페이지)
            previous = keys;
        }

        Assert.True(ds.RowKeys.Count <= 100 + changed.Count, $"적재 행 {ds.RowKeys.Count}");
        Assert.All(changed, k => Assert.Contains(k, ds.RowKeys));
        Assert.Equal(changed.Count, ds.GetChanges().Updated.Count);
    }

    [Fact]
    public void GS_052_LoadPage_RequiresRowId()
    {
        var ds = new QuroDataSet<PoLine>();

        Assert.Throws<InvalidOperationException>(() => ds.LoadPage(Page(1, 1)));
    }

    [Fact]
    public void GS_052_Load_ClearsPagedRows()
    {
        var ds = Paged();
        var key = ds.LoadPage(Page(1, 2))[0];
        ds.SetValue(key, "Qty", 99m);

        ds.Load([]);                              // 다시 조회 (저장 후 등)

        Assert.Empty(ds.RowKeys);
        var again = ds.LoadPage(Page(1, 1));
        Assert.Equal(1m, ds.GetValue(again[0], "Qty"));
    }
}
