namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-103: QueryRowKeys (A-54) — returns row keys by filter and sort conditions. It is a read: row order, row state, the current
/// row and Undo stay as they are, and no event is raised. Deleted rows are left out unless asked for.
/// </summary>
public class GS_103_QueryRowKeysTests
{
    private static QuroDataSet<PoLine> Lines()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load(
        [
            new PoLine { LineNo = 1, ItemCode = "C", Qty = 10m },
            new PoLine { LineNo = 2, ItemCode = "A", Qty = 20m },
            new PoLine { LineNo = 3, ItemCode = "B", Qty = 10m },
            new PoLine { LineNo = 4, ItemCode = "D", Qty = 30m },
        ]);
        return ds;
    }

    private static int[] LineNos(QuroDataSet<PoLine> ds, IEnumerable<int> keys) =>
        keys.Select(k => ds.GetItem(k).LineNo).ToArray();

    [Fact]
    public void GS_103_NoConditions_AllRows_InListOrder()
    {
        var ds = Lines();

        Assert.Equal(ds.RowKeys, ds.QueryRowKeys());
    }

    [Fact]
    public void GS_103_Filter_KeepsListOrder()
    {
        var ds = Lines();

        var keys = ds.QueryRowKeys(filter: l => l.Qty == 10m);

        Assert.Equal([1, 3], LineNos(ds, keys));
    }

    [Fact]
    public void GS_103_Sort_DoesNotChangeTheDataSetOrder()
    {
        var ds = Lines();
        var before = ds.RowKeys;

        var keys = ds.QueryRowKeys(sort: (a, b) => string.CompareOrdinal(a.ItemCode, b.ItemCode));

        Assert.Equal([2, 3, 1, 4], LineNos(ds, keys));
        Assert.Equal(before, ds.RowKeys);
    }

    [Fact]
    public void GS_103_Sort_IsStable()
    {
        var ds = Lines();

        var keys = ds.QueryRowKeys(sort: (a, b) => a.Qty.CompareTo(b.Qty));

        Assert.Equal([1, 3, 2, 4], LineNos(ds, keys));   // 1 and 3 have the same Qty: list order
    }

    [Fact]
    public void GS_103_FilterAndSort_Together()
    {
        var ds = Lines();

        var keys = ds.QueryRowKeys(l => l.Qty == 10m, (a, b) => string.CompareOrdinal(a.ItemCode, b.ItemCode));

        Assert.Equal([3, 1], LineNos(ds, keys));
    }

    [Fact]
    public void GS_103_DeletedRows_LeftOut_UnlessAsked()
    {
        var ds = Lines();
        var deleted = ds.RowKeys[1];
        ds.DeleteRow(deleted);

        Assert.DoesNotContain(deleted, ds.QueryRowKeys());
        Assert.DoesNotContain(deleted, ds.QueryRowKeys(l => l.LineNo == 2));
        Assert.Equal([deleted], ds.QueryRowKeys(l => l.LineNo == 2, includeDeleted: true));
        Assert.Equal(ds.RowKeys, ds.QueryRowKeys(includeDeleted: true));
    }

    [Fact]
    public void GS_103_SeesCurrentValues_AndAddedRows()
    {
        var ds = Lines();
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 99m);
        var added = ds.AddRow(new PoLine { LineNo = 5, Qty = 99m });

        Assert.Equal([ds.RowKeys[0], added], ds.QueryRowKeys(l => l.Qty == 99m));
    }

    [Fact]
    public void GS_103_NothingMatches_EmptyList()
    {
        Assert.Empty(Lines().QueryRowKeys(l => l.Qty < 0));
    }

    [Fact]
    public void GS_103_IsARead_NoStateEventOrUndoChange()
    {
        var ds = Lines();
        var key = ds.RowKeys[0];
        var revision = ds.GetRevision(key);
        var events = 0;
        ds.ValueChanged += (_, _) => events++;
        ds.RowAdded += (_, _) => events++;
        ds.RowDeleted += (_, _) => events++;
        ds.CurrentRowChanged += (_, _) => events++;
        ds.Loaded += (_, _) => events++;

        ds.QueryRowKeys(l => l.Qty > 0, (a, b) => b.Qty.CompareTo(a.Qty), includeDeleted: true);

        Assert.Equal(0, events);
        Assert.Equal(revision, ds.GetRevision(key));
        Assert.Equal(RowState.Normal, ds.GetState(key));
        Assert.False(ds.HasChanges);
        Assert.False(ds.Undo());   // nothing to undo
    }

    [Fact]
    public void GS_103_ReturnsANewListEachTime()
    {
        var ds = Lines();

        Assert.NotSame(ds.QueryRowKeys(), ds.QueryRowKeys());
    }
}
