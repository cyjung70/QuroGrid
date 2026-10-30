using System.ComponentModel.DataAnnotations;

namespace Quro.Grid.Sample.Data;

/// <summary>Customer header for the QuroUI sample page (/textbox).</summary>
public sealed class SampleCustomer
{
    [Key]
    public int Id { get; set; }

    [MaxLength(5)]
    public string? Zip { get; set; }

    [Required]
    public string? Name { get; set; }

    public string? Code { get; set; }

    public string? Pin { get; set; }
}
