using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-086~GS-088: DataSet column metadata (Addendum A-52). Built from <c>T</c>, supplemented by the screen
/// definition, defaults filled when a row is added.
/// </summary>
public class GS_086_088_ColumnsTests
{
    public sealed class Item
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = "";

        [StringLength(100)]
        public string? Name { get; set; }

        [DefaultValue(1)]   // int literal for a decimal property: converted
        public decimal Qty { get; set; }

        public DateOnly? DueDate { get; set; }

        [DefaultValue("N")]
        public string Status { get; set; } = "";

        public int ReadOnlyTotal => Id;   // not a field
    }

    // ── GS-086 from T ──

    [Fact]
    public void GS_086_Columns_FromT_InDeclarationOrder()
    {
        var ds = new QuroDataSet<Item>();

        Assert.Equal(["Id", "Code", "Name", "Qty", "DueDate", "Status"], ds.Columns.Select(c => c.Name));
        Assert.Equal(typeof(DateOnly?), ds.GetColumn("DueDate").Type);
        Assert.Equal(typeof(decimal), ds.GetColumn("Qty").Type);
    }

    [Fact]
    public void GS_086_Columns_ReadAttributes()
    {
        var ds = new QuroDataSet<Item>();

        Assert.Equal(new DataSetColumn("Id", typeof(int), Key: true), ds.GetColumn("Id"));
        Assert.Equal(new DataSetColumn("Code", typeof(string), Required: true, MaxLength: 20), ds.GetColumn("Code"));
        Assert.Equal(100, ds.GetColumn("Name").MaxLength);
        Assert.Equal(1m, ds.GetColumn("Qty").Default);
        Assert.IsType<decimal>(ds.GetColumn("Qty").Default);
    }

    [Fact]
    public void GS_086_Columns_NoAttributes_DefaultsToFalseAndNull()
    {
        var column = new QuroDataSet<PoLine>().GetColumn("Qty");

        Assert.Equal(new DataSetColumn("Qty", typeof(decimal)), column);
        Assert.Null(column.Scale);
    }

    // ── GS-087 supplemented by the screen definition ──

    [Fact]
    public void GS_087_SetColumn_ReplacesMeta_MatchedIgnoringCase()
    {
        var ds = new QuroDataSet<Item>();

        ds.SetColumn(ds.GetColumn("qty") with { Name = "qty", Required = true, Scale = 2 });

        var qty = ds.GetColumn("Qty");
        Assert.Equal("Qty", qty.Name);
        Assert.True(qty.Required);
        Assert.Equal(2, qty.Scale);
        Assert.Equal(1m, qty.Default);
        Assert.Equal("Qty", ds.Columns[3].Name);
        Assert.Same(qty, ds.Columns[3]);
    }

    [Fact]
    public void GS_087_SetColumn_ConvertsDefault()
    {
        var ds = new QuroDataSet<Item>();

        ds.SetColumn(ds.GetColumn("Qty") with { Default = 5 });

        Assert.Equal(5m, ds.GetColumn("Qty").Default);
    }

    [Fact]
    public void GS_087_SetColumn_UnknownField_Throws()
    {
        var ds = new QuroDataSet<Item>();

        Assert.Throws<ArgumentException>(() => ds.SetColumn(new DataSetColumn("Nope", typeof(int))));
        Assert.Throws<ArgumentException>(() => ds.GetColumn("Nope"));
    }

    [Fact]
    public void GS_087_SetColumn_TypeMismatch_Throws()
    {
        var ds = new QuroDataSet<Item>();

        Assert.Throws<ArgumentException>(() => ds.SetColumn(ds.GetColumn("Qty") with { Type = typeof(int) }));
    }

    [Fact]
    public void GS_087_SetColumn_DefaultNotConvertible_Throws()
    {
        var ds = new QuroDataSet<Item>();

        Assert.Throws<ArgumentException>(() => ds.SetColumn(ds.GetColumn("Qty") with { Default = "abc" }));
        Assert.Throws<ArgumentException>(() => ds.SetColumn(ds.GetColumn("Qty") with { Default = new object() }));
    }

    [Fact]
    public void GS_087_SetColumn_OnlyThisDataSet_KeptAfterLoad()
    {
        var ds = new QuroDataSet<Item>();
        var other = new QuroDataSet<Item>();

        ds.SetColumn(ds.GetColumn("Name") with { Required = true });
        ds.Load([new Item { Id = 1, Code = "A" }]);

        Assert.True(ds.GetColumn("Name").Required);
        Assert.False(other.GetColumn("Name").Required);
    }

    // ── GS-088 defaults on AddRow ──

    [Fact]
    public void GS_088_AddRow_NoTemplate_FillsDefaults()
    {
        var ds = new QuroDataSet<Item>();
        ds.SetColumn(ds.GetColumn("DueDate") with { Default = new DateOnly(2026, 10, 30) });

        var key = ds.AddRow();

        Assert.Equal(1m, ds.GetValue(key, "Qty"));
        Assert.Equal("N", ds.GetValue(key, "Status"));
        Assert.Equal(new DateOnly(2026, 10, 30), ds.GetValue(key, "DueDate"));
        Assert.Equal("", ds.GetValue(key, "Code"));   // no default: value from T's constructor
    }

    [Fact]
    public void GS_088_AddRow_WithTemplate_KeepsTemplateValues()
    {
        var ds = new QuroDataSet<Item>();

        var key = ds.AddRow(new Item { Qty = 0m, Status = "Y" });

        Assert.Equal(0m, ds.GetValue(key, "Qty"));
        Assert.Equal("Y", ds.GetValue(key, "Status"));
    }

    [Fact]
    public void GS_088_AddBlankRow_FillsDefaults_StaysUntouched()
    {
        var ds = new QuroDataSet<Item>();

        var key = ds.AddBlankRow();

        Assert.Equal(1m, ds.GetValue(key, "Qty"));
        Assert.False(ds.HasChanges);
        Assert.True(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public void GS_088_AddRow_Undo_RemovesRow()
    {
        var ds = new QuroDataSet<Item>();
        var key = ds.AddRow();

        Assert.True(ds.Undo());

        Assert.DoesNotContain(key, ds.RowKeys);
    }
}
