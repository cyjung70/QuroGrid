namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-008: Minus 재고 표시 (Spec 4.9, 2026-10-28 결정으로 변경).
/// 재고 판정·셀 오류·저장 차단은 앱이 한다. DataSet은 앱이 지정한 셀 표시를 보관하고 그리드는 그것을 그린다.
/// </summary>
public class GS_008_CellMarkTests
{
    private const string MinusStockKey = "app.stock.minus";

    [Fact]
    public void GS_008_AppMarksMinusStock_MarkIsKept()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), 999m);

        ds.SetCellMark(row, nameof(PoLine.Qty), new CellMark(CellMarkLevel.Error, MinusStockKey));

        var mark = ds.GetCellMark(row, nameof(PoLine.Qty));
        Assert.Equal(new CellMark(CellMarkLevel.Error, MinusStockKey), mark);
        Assert.Null(ds.GetCellMark(row, nameof(PoLine.UnitPrice)));
        Assert.Null(ds.GetCellMark(ds.RowKeys[1], nameof(PoLine.Qty)));
    }

    [Fact]
    public void GS_008_ClearMark_RemovesMark()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetCellMark(row, nameof(PoLine.Qty), new CellMark(CellMarkLevel.Warning, MinusStockKey));

        ds.SetCellMark(row, nameof(PoLine.Qty), null);

        Assert.Null(ds.GetCellMark(row, nameof(PoLine.Qty)));
    }

    /// <summary>그리드(DataSet)는 재고를 판정하지 않는다. 음수도 그대로 받고 검증 결과에 영향이 없다.</summary>
    [Fact]
    public void GS_008_NegativeQty_NoBuiltInJudgement()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        ds.SetValue(row, nameof(PoLine.Qty), -5m);

        Assert.True(ds.Validate().IsValid);
        Assert.Null(ds.GetCellMark(row, nameof(PoLine.Qty)));
    }

    [Fact]
    public void GS_008_MarkOnUnknownField_Throws()
    {
        var ds = TestData.TwoLines();

        Assert.Throws<ArgumentException>(() =>
            ds.SetCellMark(ds.RowKeys[0], "NoSuchField", new CellMark(CellMarkLevel.Error, MinusStockKey)));
    }
}
