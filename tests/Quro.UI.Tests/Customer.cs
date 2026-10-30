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

    public string? Country { get; set; }

    [Required]
    public string? City { get; set; }

    public string? Tags { get; set; }

    public int Level { get; set; }

    /// <summary>Two loaded customers: keys 1 (Kim, 04524) and 2 (Lee, no zip).</summary>
    public static QuroDataSet<Customer> Two()
    {
        var ds = new QuroDataSet<Customer>();
        ds.Load([new Customer { Id = 1, Name = "Kim", Zip = "04524" }, new Customer { Id = 2, Name = "Lee" }]);
        return ds;
    }
}

/// <summary>Code row used as a combo list.</summary>
public sealed class CityCode
{
    [Key]
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Country { get; set; }

    public int Population { get; set; }

    /// <summary>Four cities: 01 Seoul, 02 Busan (KR), 03 Tokyo, 04 Osaka (JP).</summary>
    public static QuroDataSet<CityCode> Four()
    {
        var ds = new QuroDataSet<CityCode>();
        ds.Load([
            new CityCode { Code = "01", Name = "Seoul", Country = "KR", Population = 940 },
            new CityCode { Code = "02", Name = "Busan", Country = "KR", Population = 330 },
            new CityCode { Code = "03", Name = "Tokyo", Country = "JP", Population = 1400 },
            new CityCode { Code = "04", Name = "Osaka", Country = "JP", Population = 270 },
        ]);
        return ds;
    }
}

internal sealed class FakeLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] =>
        name switch
        {
            "cust.zip.hint" => new(name, "Postal code"),
            "common.select" => new(name, "(Select)"),
            "code.name" => new(name, "Name"),
            "code.pop" => new(name, "Population"),
            _ => new(name, name, resourceNotFound: true),
        };

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
