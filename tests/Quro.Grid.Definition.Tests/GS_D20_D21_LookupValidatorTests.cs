namespace Quro.Grid.Definition.Tests;

// GS-D20 (lookup catalog checks V-34, V-30..V-32) and GS-D21 (screen x lookup cross-checks V-35..V-37)
// (Addendum A-48, lookup design v0.3 chapter 6). Each case changes one spot of a golden file.
public class GS_D20_D21_LookupValidatorTests
{
    private const string Screen = "sd/sd.so.entry.screen.toon";
    private static readonly string[] LookupIds = ["item", "customer", "salesOrder", "discount"];

    private static string Text(string relative) => Golden.Read(relative).Replace("\r\n", "\n");

    private static LookupDef Lookup(string id, string? from = null, string? to = null)
    {
        var relative = $"sd/{id}.lookup.toon";
        var text = Text(relative);
        if (from is not null)
        {
            Assert.Contains(from, text);
            text = text.Replace(from, to);
        }
        var result = LookupDefLoader.Load(Golden.PathOf(relative), text);
        Assert.Empty(result.Errors);
        return result.Value!;
    }

    private static Dictionary<string, LookupDef> Lookups(params string[] without) =>
        LookupIds.Except(without).ToDictionary(id => id, id => Lookup(id));

    private static (ScreenDef Screen, string Text) MutatedScreen(string? from = null, string? to = null)
    {
        var text = Text(Screen);
        if (from is not null)
        {
            Assert.Contains(from, text);
            text = text.Replace(from, to);
        }
        var result = ScreenDefLoader.Load(Golden.PathOf(Screen), text);
        Assert.Empty(result.Errors);
        return (result.Value!, text);
    }

    private static void AssertSingle(IReadOnlyList<DefinitionError> errors, string code, int line)
    {
        var error = Assert.Single(errors);
        Assert.Equal((code, line), (error.Code, error.Line));
    }

    /// <summary>GS-D20: the golden lookups pass the catalog checks</summary>
    [Fact]
    public void GS_D20_GoldenLookups_Valid()
    {
        var validator = new ScreenDefValidator(Golden.Catalog);

        foreach (var id in LookupIds)
            Assert.Empty(validator.Validate(Lookup(id)));
    }

    /// <summary>GS-D20: resource keys and the permission must be in the catalog (V-34); ids, SQL and colors as for screens</summary>
    [Theory]
    [InlineData("V-34", "  title: mm.item.lookup.title", "  title: mm.item.popup.title", "title: mm.item.popup")]
    [InlineData("V-34", "  title: mm.item.lookup.title", "  title: Items", "title: Items")]
    [InlineData("V-34", "unitPrice,mm.item.unitPrice,", "unitPrice,mm.item.price,", "unitPrice,mm.item.price")]
    [InlineData("V-34", "  minChars: 1", "  minChars: 1\n  perm: MM.Item.Read", "perm: MM.Item.Read")]
    [InlineData("V-30", "  api: /api/mm/items/lookup", "  api: \"/api/q?sql=select code from items\"", "api: \"/api/q")]
    [InlineData("V-31", "  stdPrice,mm.item.stdPrice", "  ch,mm.item.stdPrice", "ch,mm.item.stdPrice")]
    [InlineData("V-32", "stdPrice,mm.item.stdPrice,money,90,\"#,##0.00\"", "stdPrice,mm.item.stdPrice,money,90,\"#fff799\"", "stdPrice,mm.item")]
    public void GS_D20_Catalog(string code, string from, string to, string lineFragment)
    {
        var lookup = Lookup("item", from, to);

        var errors = new ScreenDefValidator(Golden.Catalog).Validate(lookup);

        AssertSingle(errors, code, Golden.LineIn(Text("sd/item.lookup.toon").Replace(from, to), lineFragment));
    }

    /// <summary>GS-D21: the golden screen with the golden lookups passes the cross-checks</summary>
    [Fact]
    public void GS_D21_GoldenScreen_WithLookups_Valid()
    {
        Assert.Empty(new ScreenDefValidator(Golden.Catalog, Lookups()).Validate(Golden.Load(Screen).Value!));
    }

    /// <summary>GS-D21: the lookup rule calls a query the lookup has, with the right number of arguments (V-35)</summary>
    [Theory]
    [InlineData("\"item.byCustomer(customer)\"", "\"item.byVendor(customer)\"")]
    [InlineData("\"item.byCustomer(customer)\"", "\"item.byCustomer()\"")]
    [InlineData("\"item.byCustomer(customer)\"", "\"item.byCustomer(customer, soDate)\"")]
    [InlineData("\"item.byCustomer(customer)\"", "\"item.byCustomer\"")]
    public void GS_D21_UnknownQueryOrArguments_V35(string from, string to)
    {
        var (screen, text) = MutatedScreen(from, to);

        var errors = new ScreenDefValidator(Golden.Catalog, Lookups()).Validate(screen);

        AssertSingle(errors, "V-35", Golden.LineIn(text, "itemCd.change,lookup"));
    }

    /// <summary>GS-D21: lookup rule targets get values from result columns with the same id (V-36)</summary>
    [Fact]
    public void GS_D21_TargetNotInResult_V36()
    {
        var (screen, text) = MutatedScreen("\"itemName|unitPrice|stdPrice\"", "\"itemName|unitPrice|invNo\"");

        var errors = new ScreenDefValidator(Golden.Catalog, Lookups()).Validate(screen);

        AssertSingle(errors, "V-36", Golden.LineIn(text, "itemCd.change,lookup"));
        Assert.Contains("invNo", errors[0].Message);
    }

    /// <summary>GS-D21: every lookup the screen uses needs a definition (V-37) — column lookup, lookup: source, rule</summary>
    [Theory]
    [InlineData("discount", "dcCode,sd.so.line.dcCode")]
    [InlineData("customer", "customer,sd.so.customer")]
    [InlineData("salesOrder", "soNo,sd.so.soNo")]
    public void GS_D21_MissingDefinition_V37(string missing, string lineFragment)
    {
        var errors = new ScreenDefValidator(Golden.Catalog, Lookups(missing)).Validate(Golden.Load(Screen).Value!);

        AssertSingle(errors, "V-37", Golden.LineOf(Screen, lineFragment));
    }

    /// <summary>GS-D21: a rule and a column on the same missing lookup are each reported</summary>
    [Fact]
    public void GS_D21_MissingItem_RuleAndColumn_V37()
    {
        var errors = new ScreenDefValidator(Golden.Catalog, Lookups("item")).Validate(Golden.Load(Screen).Value!);

        Assert.All(errors, e => Assert.Equal("V-37", e.Code));
        Assert.Equal([Golden.LineOf(Screen, "itemCd,sd.so.line.itemCd"), Golden.LineOf(Screen, "itemCd.change,lookup")],
            errors.Select(e => e.Line));
    }
}
