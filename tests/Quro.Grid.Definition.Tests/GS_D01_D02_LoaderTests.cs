namespace Quro.Grid.Definition.Tests;

// GS-D01, GS-D02: strict TOON loading of a screen definition (design v0.2 chapters 3-4, harness H1).
public class GS_D01_D02_LoaderTests
{
    private const string Screen = "sd/sd.so.entry.screen.toon";
    private const string CountMismatch = "bad/count-mismatch.screen.toon";

    /// <summary>GS-D01: the golden screen loads without errors and every table keeps its declared size</summary>
    [Fact]
    public void GS_D01_LoadSalesOrderScreen_Counts()
    {
        var result = Golden.Load(Screen);

        Assert.Empty(result.Errors);
        var screen = Assert.IsType<ScreenDef>(result.Value);
        Assert.Equal("sd.so.entry", screen.Id);
        Assert.Equal("soNo", screen.Key);
        Assert.Equal(ScreenLayout.FormGrid, screen.Layout);
        Assert.Equal("status in [RE,CL]", screen.LockWhen);
        Assert.Equal(9, screen.Toolbar.Count);
        Assert.Equal(ToolbarCommand.Print, screen.Toolbar[^1]);

        var form = Assert.IsType<FormDef>(screen.Form);
        Assert.Equal(3, form.Cols);
        Assert.Equal(22, form.Fields.Count);
        Assert.Single(form.ReadOnlyFor);
        Assert.Equal(["terms", "dcStatus", "freightCharge"], form.ReadOnlyFor[0].Fields);

        var grid = Assert.Single(screen.Grids);
        Assert.Equal("lines", grid.Id);
        Assert.Equal("seq", grid.RowKey);
        Assert.Equal(10, grid.Columns.Count);
        Assert.Equal(4, grid.Rules.Count);
        Assert.Equal(3, grid.Validate.Count);
    }

    /// <summary>GS-D01: cell values keep their types — quoted commas, empty cells, numbers, booleans, enums, | lists</summary>
    [Fact]
    public void GS_D01_LoadSalesOrderScreen_Values()
    {
        var screen = Golden.Load(Screen).Value!;
        var form = screen.Form!;
        var grid = screen.Grids[0];

        var soNo = form.Fields[0];
        Assert.Equal(("soNo", FieldType.Text, FieldMode.Auto, 18, CaseRule.Upper), (soNo.Id, soNo.Type, soNo.Mode, soNo.Max, soNo.Case));
        Assert.Equal("lookup:salesOrder", soNo.DataSource);
        Assert.Null(soNo.On);
        Assert.Equal("@today", form.Fields.Single(f => f.Id == "soDate").Default);
        Assert.Equal(0, form.Fields.Single(f => f.Id == "status").Span);

        var qty = grid.Columns.Single(c => c.Id == "qty");
        Assert.Equal(("#,##0.###", ColumnType.Number, ColumnEdit.Editable, true), (qty.Fmt, qty.Type, qty.Edit, qty.Req));
        var amount = grid.Columns.Single(c => c.Id == "amount");
        Assert.Equal((ColumnEdit.ReadOnly, true, 100), (amount.Edit, amount.Sum, amount.Width));
        Assert.Equal("item", grid.Columns[0].Lookup);

        var lookup = grid.Rules[0];
        Assert.Equal((RuleAction.Lookup, "itemCd.change"), (lookup.Action, lookup.On));
        Assert.Equal(["itemName", "unitPrice", "stdPrice"], lookup.Targets);
        Assert.Equal("qty * unitPrice * (1 - dcRate/100)", grid.Rules[1].Expr);
        Assert.Equal(("qty", "gt 0", "msg.qty.zero"), (grid.Validate[0].Target, grid.Validate[0].Rule, grid.Validate[0].Msg));

        // Every definition knows where it came from (design 9: SourceInfo)
        Assert.Equal(Golden.LineOf(Screen, "unitPrice,sd.so.line.unitPrice"), grid.Columns[3].Source.Line);
        Assert.EndsWith("sd.so.entry.screen.toon", grid.Columns[3].Source.File);
    }

    /// <summary>GS-D02: a short table, a short row and a short primitive array are all reported at once with file:line</summary>
    [Fact]
    public void GS_D02_CountMismatch_AllReportedWithLine()
    {
        var result = Golden.Load(CountMismatch);

        Assert.Null(result.Value);
        Assert.All(result.Errors, e => Assert.Equal("V-01", e.Code));
        Assert.All(result.Errors, e => Assert.EndsWith("count-mismatch.screen.toon", e.File));
        Assert.Equal(
            new[]
            {
                Golden.LineOf(CountMismatch, "toolbar[9]"),
                Golden.LineOf(CountMismatch, "columns[10]"),
                Golden.LineOf(CountMismatch, "qty,\"gt 0\""),
            },
            result.Errors.Select(e => e.Line));
        Assert.Contains(":" + result.Errors[0].Line + ":", result.Errors[0].ToString());
    }

    /// <summary>GS-D02: tabs, odd indentation and unknown syntax fail loudly instead of being skipped</summary>
    [Theory]
    [InlineData("screen:\n\tid: x\n", 2)]
    [InlineData("screen:\n   id: x\n", 2)]
    [InlineData("screen:\n  id x\n", 2)]
    [InlineData("toolbar[two]: query\n", 1)]
    public void GS_D02_BadSyntax_Reported(string text, int line)
    {
        var result = ScreenDefLoader.Load("inline.screen.toon", text);

        Assert.Null(result.Value);
        Assert.Contains(result.Errors, e => e.Code == "V-01" && e.Line == line);
    }
}
