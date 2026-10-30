namespace Quro.Grid.Sample.Data;

/// <summary>Period record for the QuroUI date picker sample (/date).</summary>
public sealed class SamplePeriod
{
    public int Id { get; set; }

    public DateOnly? Due { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? CloseMonth { get; set; }
}
