using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Quro.Grid.Data;

namespace Quro.UI.Tests;

/// <summary>Header record used by the QuroUI tests.</summary>
public sealed class Customer
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string? Name { get; set; }

    public string? Zip { get; set; }

    public decimal Credit { get; set; }

    /// <summary>Two loaded customers: keys 1 (Kim, 04524) and 2 (Lee, no zip).</summary>
    public static QuroDataSet<Customer> Two()
    {
        var ds = new QuroDataSet<Customer>();
        ds.Load([new Customer { Id = 1, Name = "Kim", Zip = "04524" }, new Customer { Id = 2, Name = "Lee" }]);
        return ds;
    }
}

internal sealed class FakeLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] =>
        name == "cust.zip.hint" ? new(name, "Postal code") : new(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
