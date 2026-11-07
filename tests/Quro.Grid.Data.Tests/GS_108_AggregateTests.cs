namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-108: column aggregates on the data set (A-76, Control Spec 2-13). Sum, Count, Average, Min and Max read the current values of the
/// rows that aren't deleted. Blanks are skipped (null and empty text), and so are values of the wrong kind, so a mismatch gives an empty
/// result and no exception. An empty result is 0 for Sum and Count and null for the others. An unknown field always throws.
/// </summary>
public class GS_108_AggregateTests
{
    public sealed class Row
    {
        public int? N { get; set; }
        public decimal? Amount { get; set; }
        public double? Ratio { get; set; }
        public string? Name { get; set; }
        public DateOnly? Due { get; set; }
        public DateTime? At { get; set; }
        public bool Done { get; set; }
    }

    private static QuroDataSet<Row> Rows(params Row[] rows)
    {
        var ds = new QuroDataSet<Row>();
        ds.Load(rows);
        return ds;
    }

    private static QuroDataSet<Row> Sample() => Rows(
        new Row { N = 3, Amount = 10m, Name = "b", Due = new DateOnly(2026, 3, 1), At = new DateTime(2026, 3, 1, 8, 0, 0) },
        new Row { N = null, Amount = null, Name = null, Due = null, At = null },
        new Row { N = 1, Amount = 20m, Name = "", Due = new DateOnly(2026, 1, 1), At = new DateTime(2026, 1, 1, 8, 0, 0) });

    [Fact]
    public void GS_108_Sum_AddsNumbers_SkipsBlanks()
    {
        Assert.Equal(30m, Sample().Sum("Amount"));
        Assert.Equal(4m, Sample().Sum("N"));
    }

    [Fact]
    public void GS_108_Count_CountsValues_NotBlanks()
    {
        Assert.Equal(2, Sample().Count("Amount"));
        Assert.Equal(1, Sample().Count("Name"));   // null and "" don't count
        Assert.Equal(2, Sample().Count("Due"));
        Assert.Equal(3, Sample().Count("Done"));   // a bool always has a value
        Assert.Equal(1, Rows(new Row { Name = " " }).Count("Name"));   // only truly empty text is blank
    }

    [Fact]
    public void GS_108_Average_SkipsBlanks_NotZero()
    {
        Assert.Equal(15m, Sample().Average("Amount"));
        Assert.Equal(2m, Sample().Average("N"));
    }

    [Fact]
    public void GS_108_MinMax_Numbers_KeepTheFieldsType()
    {
        Assert.Equal(1, Sample().Min("N"));
        Assert.Equal(3, Sample().Max("N"));
        Assert.Equal(10m, Sample().Min("Amount"));
        Assert.Equal(20m, Sample().Max("Amount"));
    }

    [Fact]
    public void GS_108_MinMax_Dates()
    {
        Assert.Equal(new DateOnly(2026, 1, 1), Sample().Min("Due"));
        Assert.Equal(new DateOnly(2026, 3, 1), Sample().Max("Due"));
        Assert.Equal(new DateTime(2026, 1, 1, 8, 0, 0), Sample().Min("At"));
        Assert.Equal(new DateTime(2026, 3, 1, 8, 0, 0), Sample().Max("At"));
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Done")]
    public void GS_108_WrongKind_GivesAnEmptyResult_NoException(string field)
    {
        var ds = Rows(new Row { Name = "12", Done = true });

        Assert.Equal(0m, ds.Sum(field));
        Assert.Null(ds.Average(field));
        Assert.Null(ds.Min(field));
        Assert.Null(ds.Max(field));
    }

    [Fact]
    public void GS_108_Average_OfDates_IsNull()
    {
        Assert.Null(Sample().Average("Due"));
        Assert.Equal(0m, Sample().Sum("Due"));
    }

    [Fact]
    public void GS_108_DoubleOutsideDecimalRange_IsSkipped_NoException()
    {
        var ds = Rows(new Row { Ratio = double.NaN }, new Row { Ratio = double.PositiveInfinity }, new Row { Ratio = 1e300 }, new Row { Ratio = 0.5 });

        Assert.Equal(0.5m, ds.Average("Ratio"));
        Assert.Equal(0.5m, ds.Sum("Ratio"));
        Assert.Equal(0.5, ds.Min("Ratio"));
        Assert.Equal(0.5, ds.Max("Ratio"));
        Assert.Equal(4, ds.Count("Ratio"));
    }

    [Fact]
    public void GS_108_NoRows_ZeroForSumAndCount_NullForTheRest()
    {
        var ds = Rows();

        Assert.Equal(0m, ds.Sum("Amount"));
        Assert.Equal(0, ds.Count("Amount"));
        Assert.Null(ds.Average("Amount"));
        Assert.Null(ds.Min("Amount"));
        Assert.Null(ds.Max("Amount"));
    }

    [Fact]
    public void GS_108_AllBlank_ZeroForSumAndCount_NullForTheRest()
    {
        var ds = Rows(new Row(), new Row());

        Assert.Equal(0m, ds.Sum("Amount"));
        Assert.Equal(0, ds.Count("Amount"));
        Assert.Null(ds.Average("Amount"));
        Assert.Null(ds.Min("Amount"));
        Assert.Null(ds.Max("Amount"));
    }

    [Fact]
    public void GS_108_DeletedRowsAreLeftOut_NewAndEditedRowsCount()
    {
        var ds = Sample();
        ds.DeleteRow(ds.RowKeys[0]);                    // 10 goes away
        ds.SetValue(ds.RowKeys[2], "Amount", 40m);      // 20 becomes 40
        ds.AddRow(new Row { Amount = 2m });

        Assert.Equal(42m, ds.Sum("Amount"));
        Assert.Equal(2, ds.Count("Amount"));
        Assert.Equal(21m, ds.Average("Amount"));
        Assert.Equal(2m, ds.Min("Amount"));
        Assert.Equal(40m, ds.Max("Amount"));
    }

    [Fact]
    public void GS_108_UnknownField_Throws_EvenWithNoRows()
    {
        var ds = Rows();

        Assert.Throws<ArgumentException>(() => ds.Sum("Nope"));
        Assert.Throws<ArgumentException>(() => ds.Count("Nope"));
        Assert.Throws<ArgumentException>(() => ds.Average("Nope"));
        Assert.Throws<ArgumentException>(() => ds.Min("Nope"));
        Assert.Throws<ArgumentException>(() => ds.Max("Nope"));
    }

    [Fact]
    public void GS_108_FieldNames_IgnoreCase_LikeGetValue()
    {
        Assert.Equal(30m, Sample().Sum("amount"));
    }

    [Fact]
    public void GS_108_Aggregates_DontChangeTheDataSet()
    {
        var ds = Sample();

        ds.Sum("Amount");
        ds.Count("Amount");
        ds.Average("Amount");
        ds.Min("Amount");
        ds.Max("Amount");

        Assert.False(ds.HasChanges);
        Assert.False(ds.CanUndo);
    }
}
