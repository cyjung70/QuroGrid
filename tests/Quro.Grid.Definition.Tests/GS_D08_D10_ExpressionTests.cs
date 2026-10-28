namespace Quro.Grid.Definition.Tests;

// GS-D08 (whitelist), GS-D09 (values), GS-D10 (lockWhen): rule and lock expressions (design v0.2 5.6, 5.7, harness H6 H7 H8).
public class GS_D08_D10_ExpressionTests
{
    private const string Screen = "sd/sd.so.entry.screen.toon";
    private const string Amount = "qty * unitPrice * (1 - dcRate/100)";

    private static Func<string, decimal?> Row(params (string Id, decimal? Value)[] values) =>
        id => values.FirstOrDefault(v => v.Id == id).Value;

    private static DefinitionResult<ScreenDef> LoadMutated(string from, string to, out string text)
    {
        text = Golden.Read(Screen);
        Assert.Contains(from, text);
        text = text.Replace(from, to);
        return ScreenDefLoader.Load(Golden.PathOf(Screen), text);
    }

    /// <summary>GS-D08: the golden amount expression parses and names its inputs</summary>
    [Fact]
    public void GS_D08_Whitelist_ParsesAmount()
    {
        var expr = RuleExpression.Parse(Amount);

        Assert.Equal(["dcRate", "qty", "unitPrice"], expr.Identifiers.Order());
    }

    /// <summary>GS-D08: anything outside the whitelist fails at parse time, before any evaluation</summary>
    [Theory]
    [InlineData("System.IO.File")]
    [InlineData("qty + \"a\"")]
    [InlineData("eval(x)")]
    [InlineData("qty *")]
    [InlineData("(qty + 1")]
    [InlineData("qty unitPrice")]
    [InlineData("qty > 1")]
    [InlineData("sum(qty * 2)")]
    [InlineData("")]
    public void GS_D08_Whitelist_Rejects(string text) =>
        Assert.Throws<FormatException>(() => RuleExpression.Parse(text));

    /// <summary>GS-D08: a column of another grid (or no grid) is not an input of this grid (V-15); bad syntax in a rule is V-18</summary>
    [Theory]
    [InlineData("V-15", "qty.change,calc,amount,\"qty * unitPrice", "qty.change,calc,amount,\"qty * listPrice")]
    [InlineData("V-18", "qty.change,calc,amount,\"qty * unitPrice", "qty.change,calc,amount,\"qty ** unitPrice")]
    [InlineData("V-18", "\"sum(amount)\"", "\"total(amount)\"")]
    public void GS_D08_RuleExpressionInDefinition(string code, string from, string to)
    {
        var result = LoadMutated(from, to, out var text);

        var error = Assert.Single(result.Errors);
        Assert.Equal((code, Golden.LineIn(text, to)), (error.Code, error.Line));
    }

    /// <summary>GS-D09: decimal arithmetic, empty values count as 0, division by zero gives null instead of throwing</summary>
    [Fact]
    public void GS_D09_Values()
    {
        var amount = RuleExpression.Parse(Amount);

        Assert.Equal(33.75m, amount.Evaluate(Row(("qty", 3), ("unitPrice", 12.50m), ("dcRate", 10))));
        Assert.Equal(37.50m, amount.Evaluate(Row(("qty", 3), ("unitPrice", 12.50m), ("dcRate", null))));
        Assert.Equal(0m, amount.Evaluate(Row(("qty", null), ("unitPrice", 12.50m), ("dcRate", 10))));
        Assert.Null(RuleExpression.Parse("x / 0").Evaluate(Row(("x", 5))));
        Assert.Null(RuleExpression.Parse("x / y + 1").Evaluate(Row(("x", 5))));
    }

    /// <summary>GS-D09: sum over a column ignores empty rows (no NaN, R11)</summary>
    [Fact]
    public void GS_D09_SumWithEmptyRows()
    {
        var rows = Enumerable.Repeat<decimal?>(null, 60).Concat([10.5m, 20m]).ToList();

        var total = RuleExpression.Parse("sum(amount)").Evaluate(Row(), id => id == "amount" ? rows : []);

        Assert.Equal(30.5m, total);
    }

    /// <summary>GS-D09: whitelisted functions</summary>
    [Theory]
    [InlineData("round(x / 3, 2)", 3.33)]
    [InlineData("round(x / 4)", 3)] // 2.5 rounds away from zero, not to even
    [InlineData("abs(0 - x)", 10)]
    [InlineData("min(x, y, 4)", 2)]
    [InlineData("max(x, y)", 10)]
    [InlineData("coalesce(z, y)", 2)]
    [InlineData("-x + 2 * (y - 1)", -8)]
    public void GS_D09_Functions(string text, double expected)
    {
        var value = RuleExpression.Parse(text).Evaluate(Row(("x", 10), ("y", 2), ("z", null)));

        Assert.Equal((decimal)expected, value);
    }

    /// <summary>GS-D09: the same input gives the same result wherever it runs (server and client share this code)</summary>
    [Fact]
    public void GS_D09_Deterministic()
    {
        var row = Row(("qty", 7), ("unitPrice", 0.1m), ("dcRate", 33.3m));

        Assert.Equal(RuleExpression.Parse(Amount).Evaluate(row), RuleExpression.Parse(Amount).Evaluate(row));
        Assert.Equal(0.46690m, RuleExpression.Parse(Amount).Evaluate(row));
    }

    /// <summary>GS-D10: lockWhen conditions</summary>
    [Theory]
    [InlineData("status in [RE,CL]", "RE", "A", true)]
    [InlineData("status in [RE,CL]", "SO", "A", false)]
    [InlineData("status in [RE,CL]", null, "A", false)]
    [InlineData("status = RE and type != X", "RE", "A", true)]
    [InlineData("status = RE and type != X", "RE", "X", false)]
    [InlineData("status = SO or status = RE and type = X", "SO", "A", true)]
    [InlineData("status = SO or status = RE and type = X", "RE", "A", false)]
    public void GS_D10_LockWhen(string text, string? status, string? type, bool locked)
    {
        var expr = LockExpression.Parse(text);

        Assert.Equal(locked, expr.Evaluate(id => id switch { "status" => status, "type" => type, _ => null }));
    }

    [Theory]
    [InlineData("status in RE")]
    [InlineData("status == RE")]
    [InlineData("status = RE and")]
    [InlineData("status in [RE,")]
    public void GS_D10_LockWhen_BadSyntax(string text) =>
        Assert.Throws<FormatException>(() => LockExpression.Parse(text));

    /// <summary>GS-D10: lockWhen must name form fields and parse (V-18)</summary>
    [Theory]
    [InlineData("lockWhen: \"status in [RE,CL]\"", "lockWhen: \"state in [RE,CL]\"")]
    [InlineData("lockWhen: \"status in [RE,CL]\"", "lockWhen: \"status in RE\"")]
    public void GS_D10_LockWhenInDefinition_V18(string from, string to)
    {
        var result = LoadMutated(from, to, out var text);

        var error = Assert.Single(result.Errors);
        Assert.Equal(("V-18", Golden.LineIn(text, "lockWhen:")), (error.Code, error.Line));
    }
}
