using System.Net;

namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-082~GS-084: DataSet 현재 행과 변경 알림 (Addendum A-51). 한 DataSet을 여러 컨트롤이 바인딩할 때
/// 현재 행을 함께 쓰고, 값·행 변경을 이벤트로 받는다.
/// </summary>
public class GS_082_084_CurrentRowEventsTests
{
    private const string Url = "https://api.test/lines/changes";

    private static QuroDataSet<PoLine> ThreeLines()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.Load(
        [
            new PoLine { LineNo = 1, ItemCode = "A1001", Qty = 10m, Version = 1 },
            new PoLine { LineNo = 2, ItemCode = "A1002", Qty = 5m, Version = 1 },
            new PoLine { LineNo = 3, ItemCode = "A1003", Qty = 7m, Version = 1 },
        ]);
        return ds;
    }

    private static List<CurrentRowChangedEventArgs> TrackMoves(QuroDataSet<PoLine> ds)
    {
        var moves = new List<CurrentRowChangedEventArgs>();
        ds.CurrentRowChanged += (_, e) => moves.Add(e);
        return moves;
    }

    // ── GS-082 현재 행 ──

    [Fact]
    public void GS_082_Load_CurrentRowIsFirstRow()
    {
        var ds = ThreeLines();

        Assert.Equal(ds.RowKeys[0], ds.CurrentRowKey);
    }

    [Fact]
    public void GS_082_LoadEmpty_CurrentRowIsNull()
    {
        var ds = ThreeLines();
        var moves = TrackMoves(ds);

        ds.Load([]);

        Assert.Null(ds.CurrentRowKey);
        var move = Assert.Single(moves);
        Assert.Null(move.NewRowKey);
    }

    [Fact]
    public void GS_082_MoveTo_RaisesCurrentRowChangedOnce()
    {
        var ds = ThreeLines();
        var moves = TrackMoves(ds);
        var (first, second) = (ds.RowKeys[0], ds.RowKeys[1]);

        ds.MoveTo(second);
        ds.MoveTo(second);   // same row: no event

        Assert.Equal(second, ds.CurrentRowKey);
        var move = Assert.Single(moves);
        Assert.Equal(first, move.OldRowKey);
        Assert.Equal(second, move.NewRowKey);
    }

    [Fact]
    public void GS_082_MoveToUnknownRow_Throws()
    {
        var ds = ThreeLines();

        Assert.Throws<KeyNotFoundException>(() => ds.MoveTo(999));
        Assert.Equal(ds.RowKeys[0], ds.CurrentRowKey);
    }

    [Fact]
    public void GS_082_CurrentInsertedRowDeleted_MovesToNextRow()
    {
        var ds = ThreeLines();
        var first = ds.RowKeys[0];
        var added = ds.AddRow(afterRowKey: first);
        ds.MoveTo(added);

        ds.DeleteRow(added);

        Assert.Equal(ds.RowKeys[1], ds.CurrentRowKey);   // the row that was below it
    }

    [Fact]
    public void GS_082_CurrentLastRowRemoved_MovesToPreviousRow()
    {
        var ds = ThreeLines();
        var added = ds.AddRow();
        ds.MoveTo(added);

        ds.RevertRow(added);

        Assert.Equal(ds.RowKeys[^1], ds.CurrentRowKey);
    }

    [Fact]
    public void GS_082_OnlyRowRemoved_CurrentRowIsNull()
    {
        var ds = new QuroDataSet<PoLine>();
        var added = ds.AddRow();
        ds.MoveTo(added);

        ds.DeleteRow(added);

        Assert.Null(ds.CurrentRowKey);
    }

    [Fact]
    public void GS_082_DeletedCurrentRowRemovedOnAccept_MovesToNextRow()
    {
        var ds = ThreeLines();
        var (first, second, third) = (ds.RowKeys[0], ds.RowKeys[1], ds.RowKeys[2]);
        ds.MoveTo(second);
        ds.DeleteRow(second);
        Assert.Equal(second, ds.CurrentRowKey);   // marked Deleted, still in the list

        ds.AcceptChanges();

        Assert.Equal(third, ds.CurrentRowKey);
        Assert.Equal([first, third], ds.RowKeys);
    }

    [Fact]
    public void GS_082_ReleasedCurrentRow_KeepsRowKey()
    {
        var ds = new QuroDataSet<PoLine>(line => line.LineNo);
        var keys = ds.LoadPage([new PoLine { LineNo = 1 }, new PoLine { LineNo = 2 }]);
        ds.MoveTo(keys[1]);
        var moves = TrackMoves(ds);

        ds.Release([keys[1]]);

        Assert.Equal(keys[1], ds.CurrentRowKey);
        Assert.Empty(moves);
    }

    // ── GS-083 변경 이벤트 ──

    [Fact]
    public void GS_083_SetValue_RaisesValueChanged()
    {
        var ds = ThreeLines();
        var row = ds.RowKeys[0];
        var changes = new List<DataSetValueChangedEventArgs>();
        ds.ValueChanged += (_, e) => changes.Add(e);

        ds.SetValue(row, "qty", 15m);   // field names also match ignoring case
        ds.SetValue(row, nameof(PoLine.Qty), 15m);   // same value: no event

        var change = Assert.Single(changes);
        Assert.Equal(row, change.RowKey);
        Assert.Equal(nameof(PoLine.Qty), change.Field);
        Assert.Equal(10m, change.OldValue);
        Assert.Equal(15m, change.NewValue);
    }

    [Fact]
    public void GS_083_AddRows_RaiseRowAdded()
    {
        var ds = ThreeLines();
        var added = new List<int>();
        ds.RowAdded += (_, e) => added.Add(e.RowKey);

        var row = ds.AddRow();
        var blank = ds.AddBlankRow();

        Assert.Equal([row, blank], added);
    }

    [Fact]
    public void GS_083_DeleteRow_RaisesRowDeleted()
    {
        var ds = ThreeLines();
        var loaded = ds.RowKeys[0];
        var inserted = ds.AddRow();
        var deleted = new List<int>();
        ds.RowDeleted += (_, e) => deleted.Add(e.RowKey);

        ds.DeleteRow(loaded);     // marked Deleted
        ds.DeleteRow(inserted);   // removed

        Assert.Equal([loaded, inserted], deleted);
    }

    [Fact]
    public void GS_083_Load_RaisesLoaded()
    {
        var ds = new QuroDataSet<PoLine>();
        var count = 0;
        ds.Loaded += (_, _) => count++;

        ds.Load([new PoLine { LineNo = 1 }]);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GS_083_SaveSuccess_RaisesSaved()
    {
        var ds = ThreeLines();
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 15m);
        var saved = new List<SaveResult>();
        ds.Saved += (_, e) => saved.Add(e.Result);

        await ds.SaveAsync(new HttpClient(new FakeHttpHandler(HttpStatusCode.OK)), Url);

        Assert.Equal(SaveStatus.Saved, Assert.Single(saved).Status);
        Assert.Equal(RowState.Normal, ds.GetState(ds.RowKeys[0]));   // raised after AcceptChanges
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GS_083_SaveNotSuccessful_DoesNotRaiseSaved(HttpStatusCode status)
    {
        var ds = ThreeLines();
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 15m);
        var count = 0;
        ds.Saved += (_, _) => count++;

        await ds.SaveAsync(new HttpClient(new FakeHttpHandler(status)), Url);
        await ThreeLines().SaveAsync(new HttpClient(new FakeHttpHandler(HttpStatusCode.OK)), Url);   // no changes

        Assert.Equal(0, count);
    }

    // ── GS-084 되돌리기의 변경 이벤트 ──

    [Fact]
    public void GS_084_RevertRow_RaisesValueChangedForChangedFields()
    {
        var ds = ThreeLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), 15m);
        ds.SetValue(row, nameof(PoLine.ItemCode), "B2002");
        var changes = new List<DataSetValueChangedEventArgs>();
        ds.ValueChanged += (_, e) => changes.Add(e);

        ds.RevertRow(row);

        Assert.Equal(
            [(nameof(PoLine.ItemCode), (object?)"B2002", (object?)"A1001"), (nameof(PoLine.Qty), 15m, 10m)],
            changes.Select(c => (c.Field, c.OldValue, c.NewValue)).OrderBy(c => c.Field));
        Assert.All(changes, c => Assert.Equal(row, c.RowKey));
    }

    [Fact]
    public void GS_084_RejectChanges_RaisesValueChangedAndRowDeleted()
    {
        var ds = ThreeLines();
        var row = ds.RowKeys[1];
        ds.SetValue(row, nameof(PoLine.Qty), 1m);
        var inserted = ds.AddRow();
        var changes = new List<(int, string)>();
        var deleted = new List<int>();
        ds.ValueChanged += (_, e) => changes.Add((e.RowKey, e.Field));
        ds.RowDeleted += (_, e) => deleted.Add(e.RowKey);

        ds.RejectChanges();

        Assert.Equal([(row, nameof(PoLine.Qty))], changes);
        Assert.Equal([inserted], deleted);
    }

    [Fact]
    public void GS_084_UndoRedo_RaiseValueChanged()
    {
        var ds = ThreeLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), 15m);
        var changes = new List<(object?, object?)>();
        ds.ValueChanged += (_, e) => changes.Add((e.OldValue, e.NewValue));

        ds.Undo();
        ds.Redo();

        Assert.Equal([((object?)15m, (object?)10m), (10m, 15m)], changes);
    }

    [Fact]
    public void GS_084_UndoAddRow_RaisesRowDeleted_RedoRaisesRowAdded()
    {
        var ds = ThreeLines();
        var row = ds.AddRow();
        ds.MoveTo(row);
        var deleted = new List<int>();
        var added = new List<int>();
        ds.RowDeleted += (_, e) => deleted.Add(e.RowKey);
        ds.RowAdded += (_, e) => added.Add(e.RowKey);

        ds.Undo();
        Assert.Equal([row], deleted);
        Assert.Equal(ds.RowKeys[^1], ds.CurrentRowKey);   // current row left the list

        ds.Redo();
        Assert.Equal([row], added);
    }
}
