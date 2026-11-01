using System.ComponentModel.DataAnnotations;

namespace Quro.Grid.Sample.Data;

/// <summary>Product row for the grid cell type sample page (/cells).</summary>
public sealed class SampleProduct
{
    [Key]
    public int Id { get; set; }

    public string? Name { get; set; }

    /// <summary>Image URL (Image cell, GS-096).</summary>
    public string? Photo { get; set; }

    /// <summary>Quantity (EditOnSelect sample, GS-098).</summary>
    public decimal Qty { get; set; }

    /// <summary>Kind code (Combo, EditOnSelect sample, GS-098).</summary>
    public string? Kind { get; set; }
}
