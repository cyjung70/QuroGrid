namespace Quro.Grid.Data;

/// <summary>
/// The aggregate rules shared by the data set and the grid (A-76). Blanks and values of the wrong kind are skipped, never an exception.
/// A floating-point value a decimal can't hold (NaN, infinity, very large) isn't treated as a number.
/// </summary>
internal static class ColumnAggregates
{
    /// <summary>Above this a double may not fit in a decimal.</summary>
    private const double DecimalLimit = 7.9e28;

    public static decimal Sum(IReadOnlyList<object?> values) => Numbers(values).Sum();

    public static int Count(IReadOnlyList<object?> values) => values.Count(v => v is not (null or ""));

    public static decimal? Average(IReadOnlyList<object?> values)
    {
        var numbers = Numbers(values).ToList();
        return numbers.Count == 0 ? null : numbers.Sum() / numbers.Count;
    }

    /// <summary>The smallest number, or the earliest date if there are no numbers, as its original value. Null if there's neither.</summary>
    public static object? Min(IReadOnlyList<object?> values) =>
        values.Where(v => ToNumber(v) is not null).MinBy(ToNumber) ?? values.Where(v => ToDate(v) is not null).MinBy(ToDate);

    /// <summary>The largest number, or the latest date if there are no numbers, as its original value. Null if there's neither.</summary>
    public static object? Max(IReadOnlyList<object?> values) =>
        values.Where(v => ToNumber(v) is not null).MaxBy(ToNumber) ?? values.Where(v => ToDate(v) is not null).MaxBy(ToDate);

    private static IEnumerable<decimal> Numbers(IEnumerable<object?> values) =>
        values.Select(ToNumber).Where(n => n is not null).Select(n => n!.Value);

    private static decimal? ToNumber(object? value) => value switch
    {
        sbyte or byte or short or ushort or int or uint or long or ulong or decimal => Convert.ToDecimal(value),
        float f when Math.Abs(f) < DecimalLimit => Convert.ToDecimal(f),   // false for NaN
        double d when Math.Abs(d) < DecimalLimit => Convert.ToDecimal(d),
        _ => null,
    };

    private static DateTime? ToDate(object? value) => value switch
    {
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTime d => d,
        DateTimeOffset d => d.UtcDateTime,
        _ => null,
    };
}
