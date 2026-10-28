namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-D15: blank rows from a screen definition's <c>initRows</c>. While a blank row still has the values it was added
/// with, it is not a change: Validate, GetChanges and HasChanges skip it and AcceptChanges leaves it as an added row.
/// </summary>
public class GS_D15_BlankRowTests
{
    private static QuroDataSet<PoLine> WithBlankRows(int count)
    {
        var ds = new QuroDataSet<PoLine>();
        ds.SetRule(nameof(PoLine.ItemCode), new FieldRule { Required = true });
        for (var i = 0; i < count; i++)
            ds.AddBlankRow();
        return ds;
    }

    [Fact]
    public void GS_D15_UntouchedBlankRows_AreNotChanges()
    {
        var ds = WithBlankRows(3);

        Assert.Equal(3, ds.RowKeys.Count);
        Assert.All(ds.RowKeys, k => Assert.Equal(RowState.Inserted, ds.GetState(k)));
        Assert.True(ds.Validate().IsValid);
        Assert.Empty(ds.GetChanges().Inserted);
        Assert.False(ds.HasChanges);
    }

    [Fact]
    public void GS_D15_EditedBlankRow_IsChecked()
    {
        var ds = WithBlankRows(3);
        var row = ds.RowKeys[1];

        ds.SetValue(row, nameof(PoLine.Qty), 5m);

        Assert.Equal(row, Assert.Single(ds.Validate().Errors).RowKey);
        Assert.Equal(row, Assert.Single(ds.GetChanges().Inserted).RowKey);
        Assert.True(ds.HasChanges);
    }

    [Fact]
    public void GS_D15_BlankRowBackToItsValues_IsUntouchedAgain()
    {
        var ds = WithBlankRows(1);
        var row = ds.RowKeys[0];

        ds.SetValue(row, nameof(PoLine.Qty), 5m);
        Assert.True(ds.Undo());

        Assert.False(ds.HasChanges);
        Assert.True(ds.Validate().IsValid);
    }

    [Fact]
    public void GS_D15_AcceptChanges_KeepsUntouchedBlankRows()
    {
        var ds = WithBlankRows(2);
        var edited = ds.RowKeys[0];
        ds.SetValue(edited, nameof(PoLine.ItemCode), "I-1");

        ds.AcceptChanges();

        Assert.Equal(RowState.Normal, ds.GetState(edited));
        Assert.Equal(RowState.Inserted, ds.GetState(ds.RowKeys[1]));
        Assert.False(ds.HasChanges);
    }

    [Fact]
    public void GS_D15_BlankRowTemplate_IsItsStartingValues()
    {
        var ds = new QuroDataSet<PoLine>();
        var row = ds.AddBlankRow(new PoLine { Qty = 1 });

        Assert.Equal(1m, ds.GetValue(row, nameof(PoLine.Qty)));
        Assert.False(ds.HasChanges);
        ds.SetValue(row, nameof(PoLine.Qty), 2m);
        Assert.True(ds.HasChanges);
    }

    /// <summary>Rows from AddRow are changes even with default values (unchanged behavior)</summary>
    [Fact]
    public void GS_D15_AddRow_IsStillAChange()
    {
        var ds = new QuroDataSet<PoLine>();
        ds.SetRule(nameof(PoLine.ItemCode), new FieldRule { Required = true });

        ds.AddRow();

        Assert.True(ds.HasChanges);
        Assert.Single(ds.GetChanges().Inserted);
        Assert.False(ds.Validate().IsValid);
    }

    /// <summary>Field names: an exact name wins; case-only matches are a fallback for unambiguous names</summary>
    [Fact]
    public void GS_D15_FieldNames_ExactNameFirst()
    {
        var ds = new QuroDataSet<CaseLine>();
        var row = ds.AddRow();

        ds.SetValue(row, "Code", "upper-first");
        ds.SetValue(row, "CODE", "all-upper");
        ds.SetValue(row, "name", "fallback");

        var item = ds.GetItem(row);
        Assert.Equal(("upper-first", "all-upper", "fallback"), (item.Code, item.CODE, item.Name));
        Assert.ThrowsAny<Exception>(() => ds.SetValue(row, "code", "ambiguous"));
    }

    public sealed class CaseLine
    {
        public string Code { get; set; } = "";
#pragma warning disable IDE1006 // two properties that differ only in case
        public string CODE { get; set; } = "";
#pragma warning restore IDE1006
        public string Name { get; set; } = "";
    }
}
