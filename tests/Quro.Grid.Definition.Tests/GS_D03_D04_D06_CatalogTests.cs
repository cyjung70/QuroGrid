namespace Quro.Grid.Definition.Tests;

// GS-D03 (resource keys), GS-D04 (references, tokens), GS-D06 (SQL, clean-room): ScreenDefValidator with the
// injected catalog (design v0.2 chapter 8, V-19..V-32, harness H3 H4 H7). Each case changes one spot of the golden screen.
public class GS_D03_D04_D06_CatalogTests
{
    private const string Screen = "sd/sd.so.entry.screen.toon";

    private static void AssertCase(string from, string to, string code, string lineFragment)
    {
        var text = Golden.Read(Screen);
        Assert.Contains(from, text);
        text = text.Replace(from, to);
        var loaded = ScreenDefLoader.Load(Golden.PathOf(Screen), text);
        Assert.Empty(loaded.Errors);

        var errors = new ScreenDefValidator(Golden.Catalog).Validate(loaded.Value!);

        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Equal(code, e.Code));
        Assert.Contains(errors, e => e.Line == Golden.LineIn(text, lineFragment));
        Assert.All(errors, e => Assert.EndsWith("sd.so.entry.screen.toon", e.File));
    }

    /// <summary>The golden screen and the golden catalog agree</summary>
    [Fact]
    public void GS_D03_GoldenScreen_NoCatalogErrors()
    {
        var errors = new ScreenDefValidator(Golden.Catalog).Validate(Golden.Load(Screen).Value!);

        Assert.Empty(errors);
    }

    /// <summary>GS-D03: labels, titles and messages must be resource keys that exist in the catalog (V-21)</summary>
    [Theory]
    [InlineData("itemCd,sd.so.line.itemCd,", "itemCd,\"Item Code\",", "itemCd,\"Item Code\"")]
    [InlineData("remarks,sd.so.remarks,", "remarks,sd.so.note,", "remarks,sd.so.note")]
    [InlineData("title: sd.so.entry.title", "title: sd.so.order.title", "title:")]
    [InlineData("msg.qty.zero", "msg.qty.none", "msg.qty.none")]
    public void GS_D03_ResourceKeys_V21(string from, string to, string lineFragment) =>
        AssertCase(from, to, "V-21", lineFragment);

    /// <summary>GS-D04: code majors, lookup ids, handlers and permissions come from the catalog (V-19, V-22..V-24)</summary>
    [Theory]
    [InlineData("V-22", "\"code:S200\"", "\"code:S999\"", "salesRep,")]
    [InlineData("V-22", "\"lookup:customer\"", "\"lookup:vendor\"", "customer,sd.so.customer")]
    [InlineData("V-22", "150,true,true,\"\",\"item\"", "150,true,true,\"\",\"product\"", "itemCd,sd.so.line.itemCd")]
    [InlineData("V-22", "\"item.byCustomer(customer)\"", "\"product.byCustomer(customer)\"", "itemCd.change,lookup")]
    [InlineData("V-23", ",loadCustomer", ",noSuchHandler", "customer,sd.so.customer")]
    [InlineData("V-24", "write: SD.SO.Write", "write: SD.SO.Edit", "write:")]
    [InlineData("V-24", "SD.SO.ReadOnlyTerms,", "SD.SO.Terms,", "SD.SO.Terms,")]
    [InlineData("V-19", "itemName,sd.so.line.itemName,text,", "itemName,sd.so.line.itemName,select,", "itemName,sd.so.line.itemName")]
    [InlineData("V-19", "\"code:S600\"", "\"\"", "shipVia,")]
    [InlineData("V-19", "80,true,true,\"#,##0.###\",\"\"", "80,true,true,\"#,##0.###\",\"item\"", "qty,sd.so.line.qty")]
    public void GS_D04_References(string code, string from, string to, string lineFragment) =>
        AssertCase(from, to, code, lineFragment);

    /// <summary>GS-D04: default tokens follow design 5.5 (@today, @now, @company.x, @setting:x, @user.x); a typo is V-03 at load</summary>
    [Fact]
    public void GS_D04_DefaultTokenTypo_V03()
    {
        var text = Golden.Read(Screen).Replace("@today,\"\",1,calcDueDate", "@tody,\"\",1,calcDueDate");

        var result = ScreenDefLoader.Load(Golden.PathOf(Screen), text);

        var error = Assert.Single(result.Errors);
        Assert.Equal(("V-03", Golden.LineIn(text, "@tody")), (error.Code, error.Line));
    }

    /// <summary>GS-D06: SQL in values (V-30), clean-room or snake_case ids (V-31), colors carrying meaning (V-32)</summary>
    [Theory]
    [InlineData("V-30", "remarks,sd.so.remarks,text,opt,200,\"\",\"\",", "remarks,sd.so.remarks,text,opt,200,\"\",\"select name from codes where id = 1\",", "remarks,sd.so.remarks")]
    [InlineData("V-31", "invNo,sd.so.line.invNo", "txt,sd.so.line.invNo", "txt,sd.so.line.invNo")]
    [InlineData("V-31", "invNo,sd.so.line.invNo", "item_code,sd.so.line.invNo", "item_code,sd.so.line.invNo")]
    [InlineData("V-32", ",Collect,", ",\"#ffff00\",", "freightCharge,")]
    public void GS_D06_SqlAndCleanRoom(string code, string from, string to, string lineFragment) =>
        AssertCase(from, to, code, lineFragment);
}
