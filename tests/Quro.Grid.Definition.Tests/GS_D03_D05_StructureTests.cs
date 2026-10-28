namespace Quro.Grid.Definition.Tests;

// GS-D03 (enum values, structure), GS-D05 (rule targets): checks that need only the definition itself
// (design v0.2 chapter 8, V-03..V-18, harness H1 H2). Each case changes one spot of the golden screen.
public class GS_D03_D05_StructureTests
{
    private const string Screen = "sd/sd.so.entry.screen.toon";

    private static DefinitionResult<ScreenDef> LoadMutated(string from, string to, string? path = null)
    {
        var text = Golden.Read(Screen);
        Assert.Contains(from, text);
        return ScreenDefLoader.Load(path ?? Golden.PathOf(Screen), text.Replace(from, to));
    }

    private static int LineIn(string text, string fragment)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var hits = Enumerable.Range(0, lines.Length).Where(i => lines[i].Contains(fragment, StringComparison.Ordinal)).ToList();
        Assert.Single(hits);
        return hits[0] + 1;
    }

    private static void AssertOnly(DefinitionResult<ScreenDef> result, string code, int line)
    {
        Assert.Null(result.Value);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Equal(code, e.Code));
        Assert.Contains(result.Errors, e => e.Line == line);
    }

    private static void AssertCase(string from, string to, string code, string lineFragment)
    {
        var result = LoadMutated(from, to);
        AssertOnly(result, code, LineIn(Golden.Read(Screen).Replace(from, to), lineFragment));
    }

    /// <summary>GS-D03: enum values outside the schema are V-03 at the row that has them</summary>
    [Theory]
    [InlineData("remarks,sd.so.remarks,text,opt,", "remarks,sd.so.remarks,text,required,", "remarks,sd.so.remarks")]
    [InlineData("itemName,sd.so.line.itemName,text,", "itemName,sd.so.line.itemName,combo,", "itemName,sd.so.line.itemName")]
    [InlineData("status,sd.so.status,hidden,ro,\"\",\"\",SO,\"\",0,", "status,sd.so.status,hidden,ro,\"\",\"\",SO,\"\",4,", "status,sd.so.status")]
    [InlineData("shipZip,sd.so.shipZip,text,req,20,\"\",\"\",\"\",1,", "shipZip,sd.so.shipZip,text,req,20,\"\",\"\",\"\",0,", "shipZip,sd.so.shipZip")]
    public void GS_D03_EnumAndRange_V03(string from, string to, string lineFragment) =>
        AssertCase(from, to, "V-03", lineFragment);

    /// <summary>GS-D03: structure checks V-04..V-14 report the line of the offending entry</summary>
    [Theory]
    [InlineData("V-04", "invNo,sd.so.line.invNo", "status,sd.so.line.invNo", "status,sd.so.line.invNo")]
    [InlineData("V-04", "  - id: lines", "  - id: remarks", "- id: remarks")]
    [InlineData("V-05", "  id: sd.so.entry", "  id: sd.so.order", "id: sd.so.order")]
    [InlineData("V-10", "  key: soNo", "  key: orderNo", "key: orderNo")]
    [InlineData("V-11", "remarks,sd.so.remarks,text,opt,200", "remarks,sd.so.remarks,date,opt,200", "remarks,sd.so.remarks")]
    [InlineData("V-12", "amount,sd.so.line.amount,money,100,false,false", "amount,sd.so.line.amount,money,100,false,true", "amount,sd.so.line.amount")]
    [InlineData("V-13", "invNo,sd.so.line.invNo,text,110,false,false,\"\",\"\",false", "invNo,sd.so.line.invNo,text,110,false,false,\"\",\"\",true", "invNo,sd.so.line.invNo")]
    public void GS_D03_Structure(string code, string from, string to, string lineFragment) =>
        AssertCase(from, to, code, lineFragment);

    /// <summary>GS-D03: grid toolbar commands need a grid (V-14)</summary>
    [Fact]
    public void GS_D03_GridCommandWithoutGrid_V14()
    {
        var text = Golden.Read(Screen);
        var formOnly = text[..text.IndexOf("grids[1]:", StringComparison.Ordinal)].Replace("layout: form-grid", "layout: form");

        var result = ScreenDefLoader.Load(Golden.PathOf(Screen), formOnly);

        AssertOnly(result, "V-14", LineIn(formOnly, "toolbar[9]"));
        Assert.Single(result.Errors);
    }

    /// <summary>GS-D05: rule targets and inputs must exist and have the right edit state or mode</summary>
    [Theory]
    [InlineData("V-15", "qty.change,calc,amount,", "qty.change,calc,netAmount,", "qty.change,calc")]
    [InlineData("V-15", "unitPrice.change,calc,amount,", "unitPrice.change,calc,itemName,", "unitPrice.change,calc")]
    [InlineData("V-15", "qty.change,calc,amount,\"qty * unitPrice", "qty.change,calc,amount,\"qty * unitPrce", "qty.change,calc")]
    [InlineData("V-15", "\"itemName|unitPrice|stdPrice\"", "\"itemName|unitPrice|listPrice\"", "itemCd.change,lookup")]
    [InlineData("V-16", "lines.change,sum,totalAmt,", "lines.change,sum,dueDate,", "lines.change,sum")]
    [InlineData("V-18", "\"requires customer\"", "\"requires client\"", "requires client")]
    public void GS_D05_RuleTargets(string code, string from, string to, string lineFragment) =>
        AssertCase(from, to, code, lineFragment);

    /// <summary>GS-D05: calc rules that feed each other (amount ← dcRate, dcRate ← amount) are a cycle (V-17)</summary>
    [Fact]
    public void GS_D05_CalcCycle_V17()
    {
        var text = Golden.Read(Screen)
            .Replace("rules[4]{", "rules[5]{")
            .Replace("    validate[3]", "      amount.change,calc,dcRate,\"amount / 10\"\n    validate[3]");

        var result = ScreenDefLoader.Load(Golden.PathOf(Screen), text);

        AssertOnly(result, "V-17", LineIn(text, "amount.change,calc,dcRate"));
    }

    /// <summary>Structure checks are skipped while the file has syntax or key errors, so one typo is not reported twice</summary>
    [Fact]
    public void GS_D03_NoCascadeAfterSyntaxErrors()
    {
        var result = Golden.Load("bad/count-mismatch.screen.toon");

        Assert.All(result.Errors, e => Assert.Equal("V-01", e.Code));
    }
}
