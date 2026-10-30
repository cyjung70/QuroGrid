using System.ComponentModel.DataAnnotations;

namespace Quro.Grid.Sample.Data;

/// <summary>Customer header for the QuroUI sample pages (/textbox, /combo, /mask).</summary>
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

    public string? Country { get; set; }

    public string? City { get; set; }

    public string? Tags { get; set; }

    /// <summary>Business registration number, stored without the dashes (/mask).</summary>
    public string? BizNo { get; set; }

    /// <summary>Phone number, stored without the dashes (/mask).</summary>
    public string? Phone { get; set; }
}
