namespace Quro.Grid.Sample.Data;

/// <summary>Code row for the QuroUI combo sample (/combo): countries and cities.</summary>
public sealed class SampleCode
{
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Parent { get; set; }

    public string? Region { get; set; }
}
