namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-D14 (G-02): exclusive bounds and not-equal for screen definition <c>validate</c> rows (gt, lt, ne), with the
/// definition's message key. Field names from a screen definition are camelCase; DTO properties are PascalCase.
/// </summary>
public class GS_D14_RuleBoundsTests
{
    private static (QuroDataSet<PoLine> Ds, int Row) NewRow(decimal qty)
    {
        var ds = TestData.TwoLines();
        var row = ds.AddRow();
        ds.SetValue(row, nameof(PoLine.Qty), qty);
        return (ds, row);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0.001, true)]
    public void GS_D14_MinExclusive_GreaterThan(decimal qty, bool valid)
    {
        var (ds, _) = NewRow(qty);
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { Min = 0, MinExclusive = true });

        var result = ds.Validate();

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            Assert.Equal(ValidationKeys.MinExclusive, Assert.Single(result.Errors).MessageKey);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(99.99, true)]
    public void GS_D14_MaxExclusive_LessThan(decimal qty, bool valid)
    {
        var (ds, _) = NewRow(qty);
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { Max = 100, MaxExclusive = true });

        var result = ds.Validate();

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            Assert.Equal(ValidationKeys.MaxExclusive, Assert.Single(result.Errors).MessageKey);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void GS_D14_NotEqual(decimal qty, bool valid)
    {
        var (ds, _) = NewRow(qty);
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { NotEqual = 0 });

        var result = ds.Validate();

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            Assert.Equal(ValidationKeys.NotEqual, Assert.Single(result.Errors).MessageKey);
    }

    /// <summary>GS-D14: the definition's msg key replaces the default key for range and not-equal errors, not for required</summary>
    [Fact]
    public void GS_D14_MessageKey_ReplacesRangeKeys()
    {
        var (ds, row) = NewRow(0);
        ds.SetValue(row, nameof(PoLine.ItemCode), "");
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { Min = 0, MinExclusive = true, MessageKey = "msg.qty.zero" });
        ds.SetRule(nameof(PoLine.ItemCode), new FieldRule { Required = true, MessageKey = "msg.item.code" });

        var errors = ds.Validate().Errors;

        Assert.Contains(errors, e => e.Field == nameof(PoLine.Qty) && e.MessageKey == "msg.qty.zero");
        Assert.Contains(errors, e => e.Field == nameof(PoLine.ItemCode) && e.MessageKey == ValidationKeys.Required);
    }

    /// <summary>Screen definition ids (camelCase) find PascalCase properties; exact names still win</summary>
    [Fact]
    public void GS_D14_CamelCaseField_FindsProperty()
    {
        var (ds, row) = NewRow(0);
        ds.SetRule("qty", new FieldRule { NotEqual = 0 });

        ds.SetValue(row, "unitPrice", 12.5m);

        Assert.Equal(12.5m, ds.GetValue(row, nameof(PoLine.UnitPrice)));
        Assert.Equal(nameof(PoLine.Qty), Assert.Single(ds.Validate().Errors).Field);
    }
}
