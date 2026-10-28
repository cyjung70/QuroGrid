namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-076~GS-078: Undo / Redo (P1, Addendum A-33). 셀 값 변경·붙여넣기·일괄 삭제·행 추가를 한 단계씩 되돌린다.
/// 여러 변경을 한 단계로 묶을 때는 BeginUndoStep 범위 안에서 한다. 저장 성공·다시 조회 때 이력을 비운다. 50단계.
/// </summary>
public class GS_076_078_UndoTests
{
    private const string Qty = nameof(PoLine.Qty);
    private const string Price = nameof(PoLine.UnitPrice);

    [Fact]
    public void GS_076_SetValue_UndoRedo_FollowsStateRules()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        Assert.False(ds.CanUndo);

        ds.SetValue(row, Qty, 7m);
        Assert.True(ds.CanUndo);
        Assert.False(ds.CanRedo);

        Assert.True(ds.Undo());
        Assert.Equal(10m, ds.GetValue(row, Qty));
        Assert.Equal(RowState.Normal, ds.GetState(row));     // 원본과 같아지면 Normal
        Assert.False(ds.CanUndo);
        Assert.True(ds.CanRedo);

        Assert.True(ds.Redo());
        Assert.Equal(7m, ds.GetValue(row, Qty));
        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.False(ds.Redo());                             // 더 없음
    }

    [Fact]
    public void GS_076_EachCallIsOneStep_UndoInReverseOrder()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, Qty, 7m);
        ds.SetValue(row, Price, 3000m);

        ds.Undo();
        Assert.Equal((7m, 2500m), ((decimal)ds.GetValue(row, Qty)!, (decimal)ds.GetValue(row, Price)!));
        ds.Undo();
        Assert.Equal((10m, 2500m), ((decimal)ds.GetValue(row, Qty)!, (decimal)ds.GetValue(row, Price)!));
    }

    [Fact]
    public void GS_076_InsertedRow_ValueUndo_StaysInserted()
    {
        var ds = TestData.TwoLines();
        var added = ds.AddRow();
        ds.SetValue(added, Qty, 3m);

        ds.Undo();

        Assert.Equal(RowState.Inserted, ds.GetState(added));
        Assert.Equal(0m, ds.GetValue(added, Qty));
    }

    [Fact]
    public void GS_077_Step_GroupsManyChanges_RestoresRowsAndOrder()
    {
        var ds = TestData.TwoLines();
        var (first, second) = (ds.RowKeys[0], ds.RowKeys[1]);
        var removed = ds.AddRow(new PoLine { ItemCode = "X" });
        Assert.Equal([first, second, removed], ds.RowKeys);

        int added;
        using (ds.BeginUndoStep())
        {
            ds.SetValue(first, Qty, 1m);
            ds.SetValue(first, Qty, 2m);
            added = ds.AddRow(new PoLine { ItemCode = "N" }, afterRowKey: first);
            ds.DeleteRow(removed);                           // 추가 행 삭제: 목록에서 제거
            ds.DeleteRow(second);                            // 조회 행 삭제: Deleted
            using (ds.BeginUndoStep())                       // 안쪽 범위는 바깥 단계에 합쳐진다
                ds.SetValue(added, Qty, 9m);
        }
        Assert.Equal([first, added, second], ds.RowKeys);

        Assert.True(ds.Undo());                              // 한 단계로 전부
        Assert.Equal([first, second, removed], ds.RowKeys);
        Assert.Equal(10m, ds.GetValue(first, Qty));
        Assert.Equal(RowState.Normal, ds.GetState(first));
        Assert.Equal(RowState.Normal, ds.GetState(second));
        Assert.Equal(RowState.Inserted, ds.GetState(removed));
        Assert.Equal("X", ds.GetValue(removed, nameof(PoLine.ItemCode)));
        Assert.Contains(removed, ds.GetChanges().Inserted.Select(r => r.RowKey));

        Assert.True(ds.Redo());
        Assert.Equal([first, added, second], ds.RowKeys);   // 같은 행 키
        Assert.Equal(2m, ds.GetValue(first, Qty));
        Assert.Equal(9m, ds.GetValue(added, Qty));
        Assert.Equal(RowState.Deleted, ds.GetState(second));
        Assert.Equal(RowState.Inserted, ds.GetState(added));
    }

    [Fact]
    public void GS_077_DeleteUpdatedRow_Undo_KeepsUpdatedValues()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, Qty, 7m);
        ds.DeleteRow(row);

        ds.Undo();

        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.Equal(7m, ds.GetValue(row, Qty));
        Assert.Equal(10m, ds.GetOriginal(row, Qty));
    }

    [Fact]
    public void GS_077_RevertRow_IsOneUndoableStep()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, Qty, 7m);
        ds.SetValue(row, Price, 1m);
        ds.RevertRow(row);

        ds.Undo();

        Assert.Equal((7m, 1m), ((decimal)ds.GetValue(row, Qty)!, (decimal)ds.GetValue(row, Price)!));
        Assert.Equal(RowState.Updated, ds.GetState(row));
    }

    [Fact]
    public void GS_077_NewChangeAfterUndo_ClearsRedo_EmptyStepNotRecorded()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, Qty, 7m);
        ds.Undo();
        ds.SetValue(row, Price, 1m);

        Assert.False(ds.CanRedo);

        using (ds.BeginUndoStep())
        {
        }
        Assert.True(ds.Undo());                              // 빈 단계는 남지 않음: 바로 앞 단계가 되돌려진다
        Assert.Equal(2500m, ds.GetValue(row, Price));
        Assert.False(ds.CanUndo);
    }

    [Fact]
    public void GS_077_HistoryKeepsLast50Steps()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        for (var i = 1; i <= 60; i++)
            ds.SetValue(row, Qty, (decimal)i);

        var undone = 0;
        while (ds.Undo())
            undone++;

        Assert.Equal(50, undone);
        Assert.Equal(10m, ds.GetValue(row, Qty));             // 11단계째 값(10)까지
    }

    [Fact]
    public void GS_078_AcceptChanges_ClearsHistory()
    {
        var ds = TestData.TwoLines();
        ds.SetValue(ds.RowKeys[0], Qty, 7m);
        ds.Undo();
        ds.SetValue(ds.RowKeys[0], Qty, 8m);

        ds.AcceptChanges();

        Assert.False(ds.CanUndo);
        Assert.False(ds.CanRedo);
        Assert.Equal(8m, ds.GetValue(ds.RowKeys[0], Qty));
    }

    [Fact]
    public void GS_078_Load_ClearsHistory()
    {
        var ds = TestData.TwoLines();
        ds.SetValue(ds.RowKeys[0], Qty, 7m);

        ds.Load([new PoLine { LineNo = 1, Qty = 1m }]);

        Assert.False(ds.CanUndo);
        Assert.False(ds.Undo());
    }
}
