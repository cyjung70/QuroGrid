using Quro.Grid.Data;
using Quro.Grid.Definition;

namespace Quro.Grid.Tests;

/// <summary>Sales order line for the golden screen definition (GS-D11~GS-D15). PascalCase DTO, camelCase definition ids.</summary>
public sealed class SoLine
{
    public string ItemCd { get; set; } = "";
    public string ItemName { get; set; } = "";
    public decimal? Qty { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public string DcCode { get; set; } = "";
    public decimal? DcRate { get; set; }
    public decimal? StdPrice { get; set; }
    public decimal? InvQty { get; set; }
    public string InvNo { get; set; } = "";
}

internal static class GoldenScreen
{
    private static string PathOf => Path.Combine(AppContext.BaseDirectory, "Golden", "sd.so.entry.screen.toon");

    /// <summary>The <c>lines</c> grid of the golden screen, optionally with one text change.</summary>
    public static GridDef Lines(string? from = null, string? to = null)
    {
        var text = File.ReadAllText(PathOf);
        if (from is not null)
        {
            Assert.Contains(from, text);
            text = text.Replace(from, to);
        }
        var result = ScreenDefLoader.Load(PathOf, text);
        Assert.Empty(result.Errors);
        return result.Value!.Grids[0];
    }

    /// <summary>One saved line: qty 2, unit price 10, no discount.</summary>
    public static QuroDataSet<SoLine> OneLine()
    {
        var ds = new QuroDataSet<SoLine>();
        ds.Load([new SoLine { ItemCd = "I-100", ItemName = "Bolt", Qty = 2, UnitPrice = 10, Amount = 20, DcRate = 0 }]);
        return ds;
    }
}
