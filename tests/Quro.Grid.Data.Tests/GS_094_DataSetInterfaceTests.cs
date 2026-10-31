namespace Quro.Grid.Data.Tests;

/// <summary>GS-094: IQuroDataSet — the read-only, non-generic view a grid column uses for a code DataSet (A-57)</summary>
public class GS_094_DataSetInterfaceTests
{
    public sealed class Code
    {
        public string Value { get; set; } = "";
        public string Name { get; set; } = "";
    }

    [Fact]
    public void GS_094_DataSet_ReadsThroughInterface()
    {
        var ds = new QuroDataSet<Code>();
        ds.Load([new Code { Value = "KR", Name = "Korea" }, new Code { Value = "JP", Name = "Japan" }]);
        ds.DeleteRow(ds.RowKeys[1]);
        IQuroDataSet codes = ds;

        Assert.Equal(ds.RowKeys, codes.RowKeys);
        Assert.Equal("Korea", codes.GetValue(codes.RowKeys[0], "Name"));
        Assert.Equal(RowState.Deleted, codes.GetState(codes.RowKeys[1]));
        Assert.Equal(typeof(string), codes.GetColumn("Value").Type);
        Assert.Throws<ArgumentException>(() => codes.GetColumn("Missing"));
    }

    [Fact]
    public void GS_094_DataSet_InterfaceEvents()
    {
        var ds = new QuroDataSet<Code>();
        IQuroDataSet codes = ds;
        var seen = new List<string>();
        codes.Loaded += (_, _) => seen.Add("loaded");
        codes.RowAdded += (_, _) => seen.Add("added");
        codes.ValueChanged += (_, e) => seen.Add("value " + e.Field);
        codes.RowDeleted += (_, _) => seen.Add("deleted");

        ds.Load([new Code { Value = "KR", Name = "Korea" }]);
        var key = ds.AddRow();
        ds.SetValue(key, "Name", "Japan");
        ds.DeleteRow(key);

        Assert.Equal(["loaded", "added", "value Name", "deleted"], seen);
    }
}
