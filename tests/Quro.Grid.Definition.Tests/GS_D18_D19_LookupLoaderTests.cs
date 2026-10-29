namespace Quro.Grid.Definition.Tests;

// GS-D18, GS-D19: code-help definitions *.lookup.toon (Addendum A-48, lookup design v0.3 chapters 4 and 6).
// GS-D18 loads the golden lookup; GS-D19 changes one spot of it and expects one error at that line.
public class GS_D18_D19_LookupLoaderTests
{
    private const string Item = "sd/item.lookup.toon";

    /// <summary>The golden lookup with \n line ends, so cases can replace across lines.</summary>
    private static string ItemText => Golden.Read(Item).Replace("\r\n", "\n");

    private static DefinitionResult<LookupDef> LoadMutated(string from, string to, string? path = null)
    {
        var text = ItemText;
        Assert.Contains(from, text);
        return LookupDefLoader.Load(path ?? Golden.PathOf(Item), text.Replace(from, to));
    }

    private static void AssertCase(string from, string to, string code, string lineFragment)
    {
        var result = LoadMutated(from, to);

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(Golden.LineIn(ItemText.Replace(from, to), lineFragment), error.Line);
        Assert.Equal(Golden.PathOf(Item), error.File);
    }

    /// <summary>GS-D18: the golden lookup loads with every section and value type</summary>
    [Fact]
    public void GS_D18_LoadItemLookup()
    {
        var result = Golden.LoadLookup(Item);

        Assert.Empty(result.Errors);
        var item = Assert.IsType<LookupDef>(result.Value);
        Assert.Equal(("item", "mm.item.lookup.title", 1, "/api/mm/items/lookup"), (item.Id, item.Title, item.Version, item.Api));
        Assert.Equal(("itemCd", "itemName", 1, (string?)null), (item.Key, item.Text, item.MinChars, item.Perm));

        var param = Assert.Single(item.Params);
        Assert.Equal(("customer", LookupParamType.Text, false), (param.Id, param.Type, param.Req));

        Assert.Equal(["all", "byCustomer"], item.Queries.Select(q => q.Id));
        Assert.Empty(item.Queries[0].Params);
        Assert.Equal(["customer"], item.Queries[1].Params);

        Assert.Equal(["itemCd", "itemName", "unitPrice", "stdPrice"], item.Columns.Select(c => c.Id));
        var price = item.Columns[2];
        Assert.Equal(("mm.item.unitPrice", ColumnType.Money, 90, "#,##0.00"), (price.Label, price.Type, price.Width, price.Fmt));
        Assert.Null(item.Columns[0].Fmt);
        Assert.Equal(new SourceInfo(Golden.PathOf(Item), Golden.LineOf(Item, "unitPrice,mm.item")), price.Source);
        Assert.Equal(Golden.LineOf(Item, "lookup:"), item.Source.Line);
    }

    /// <summary>GS-D18: optional keys take their defaults (text none, minChars 1, perm none)</summary>
    [Fact]
    public void GS_D18_OptionalKeys_Defaults()
    {
        var result = LoadMutated("  text: itemName\n  minChars: 1\n", "");

        var item = Assert.IsType<LookupDef>(result.Value);
        Assert.Equal(((string?)null, 1), (item.Text, item.MinChars));
    }

    /// <summary>GS-D19: TOON and schema errors are the same codes as for screens (V-01..V-03)</summary>
    [Theory]
    [InlineData("V-01", "columns[4]{", "columns[5]{", "columns[5]{")]
    [InlineData("V-02", "  minChars: 1", "  minChars: 1\n  color: red", "color: red")]
    [InlineData("V-02", "  api: /api/mm/items/lookup\n", "", "lookup:")]
    [InlineData("V-03", "customer,text,false", "customer,string,false", "customer,string")]
    [InlineData("V-03", "unitPrice,mm.item.unitPrice,money,", "unitPrice,mm.item.unitPrice,select,", "unitPrice,mm.item")]
    [InlineData("V-03", "  version: 1", "  version: 0", "version: 0")]
    [InlineData("V-03", "  minChars: 1", "  minChars: -1", "minChars: -1")]
    [InlineData("V-03", "  api: /api/mm/items/lookup", "  api: api/mm/items/lookup", "api: api/mm")]
    [InlineData("V-03", "stdPrice,mm.item.stdPrice,money,90,", "stdPrice,mm.item.stdPrice,money,0,", "stdPrice,mm.item")]
    public void GS_D19_SchemaErrors(string code, string from, string to, string lineFragment) =>
        AssertCase(from, to, code, lineFragment);

    /// <summary>GS-D19: structure errors are V-33 at the offending line</summary>
    [Theory]
    [InlineData("  id: item", "  id: product", "id: product")]
    [InlineData("  key: itemCd", "  key: code", "key: code")]
    [InlineData("  text: itemName", "  text: name", "text: name")]
    [InlineData("  byCustomer,customer", "  byCustomer,vendor", "byCustomer,vendor")]
    [InlineData("  stdPrice,mm.item.stdPrice", "  unitPrice,mm.item.stdPrice", "unitPrice,mm.item.stdPrice")]
    [InlineData("  byCustomer,customer", "  all,customer", "all,customer")]
    public void GS_D19_Structure_V33(string from, string to, string lineFragment) =>
        AssertCase(from, to, "V-33", lineFragment);

    /// <summary>GS-D19: the id must match the file name {id}.lookup.toon</summary>
    [Fact]
    public void GS_D19_FileName_V33()
    {
        var result = LoadMutated("  id: item", "  id: item", Golden.PathOf("sd/items.lookup.toon"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(("V-33", Golden.LineOf(Item, "id: item")), (error.Code, error.Line));
    }
}
