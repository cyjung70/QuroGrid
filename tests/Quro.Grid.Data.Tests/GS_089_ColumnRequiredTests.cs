using System.ComponentModel.DataAnnotations;

namespace Quro.Grid.Data.Tests;

/// <summary>GS-089: Validate also checks column metadata Required (Addendum A-63, decision 2(a)).</summary>
public class GS_089_ColumnRequiredTests
{
    public sealed class Customer
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        [MaxLength(3)]
        public string? Zip { get; set; }
    }

    private static QuroDataSet<Customer> OneCustomer()
    {
        var ds = new QuroDataSet<Customer>();
        ds.Load([new Customer { Id = 1, Name = "Kim" }]);
        return ds;
    }

    [Fact]
    public void GS_089_ColumnRequired_Empty_Fails()
    {
        var ds = OneCustomer();
        var row = ds.RowKeys[0];

        ds.SetValue(row, "Name", " ");

        var error = Assert.Single(ds.Validate().Errors);
        Assert.Equal(new ValidationError(row, "Name", ValidationKeys.Required), error);
    }

    [Fact]
    public void GS_089_SetColumnRequired_InsertedRow_Fails()
    {
        var ds = OneCustomer();
        ds.SetColumn(ds.GetColumn("Zip") with { Required = true });

        var row = ds.AddRow(new Customer { Name = "Lee" });

        Assert.Equal(["Zip"], ds.Validate().Errors.Where(e => e.RowKey == row).Select(e => e.Field));
    }

    [Fact]
    public void GS_089_ColumnAndRuleRequired_OneError()
    {
        var ds = OneCustomer();
        ds.SetRule("Name", new FieldRule { Required = true });

        ds.SetValue(ds.RowKeys[0], "Name", null);

        Assert.Single(ds.Validate().Errors);
    }

    [Fact]
    public void GS_089_NormalAndUntouchedBlankRows_NotChecked()
    {
        var ds = new QuroDataSet<Customer>();
        ds.Load([new Customer { Id = 1 }]);   // loaded without a name: not a change
        ds.AddBlankRow();

        Assert.True(ds.Validate().IsValid);
    }

    [Fact]
    public void GS_089_MaxLength_NotChecked()
    {
        var ds = OneCustomer();

        ds.SetValue(ds.RowKeys[0], "Zip", "12345");

        Assert.True(ds.Validate().IsValid);
    }
}
