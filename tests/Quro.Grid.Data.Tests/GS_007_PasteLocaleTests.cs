using System.Globalization;

namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-007: 붙여넣은 문자열을 사용자 로캘로 해석 (Spec 4.8).
/// 숫자와 날짜 모두 포함 (2026-10-28 결정).
/// </summary>
public class GS_007_PasteLocaleTests
{
    private static readonly CultureInfo De = new("de-DE");
    private static readonly CultureInfo En = new("en-US");

    [Fact]
    public void GS_007_PasteDeLocale_ParsesDecimal()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        Assert.True(ds.TrySetText(row, nameof(PoLine.Qty), "1.234,5", De));

        Assert.Equal(1234.5m, ds.GetValue(row, nameof(PoLine.Qty)));
        Assert.Equal(RowState.Updated, ds.GetState(row));
    }

    [Fact]
    public void GS_007_PasteEnLocale_ParsesDecimal()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        Assert.True(ds.TrySetText(row, nameof(PoLine.Qty), "1,234.5", En));

        Assert.Equal(1234.5m, ds.GetValue(row, nameof(PoLine.Qty)));
    }

    [Fact]
    public void GS_007_PasteDeLocale_ParsesDate()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        Assert.True(ds.TrySetText(row, nameof(PoLine.DueDate), "28.10.2026", De));

        Assert.Equal(new DateOnly(2026, 10, 28), ds.GetValue(row, nameof(PoLine.DueDate)));
    }

    [Fact]
    public void GS_007_PasteEmpty_NullableDate_BecomesNull()
    {
        var ds = TestData.TwoLines();
        var row = ds.AddRow(new PoLine { DueDate = new DateOnly(2026, 1, 1) });

        Assert.True(ds.TrySetText(row, nameof(PoLine.DueDate), "", De));

        Assert.Null(ds.GetValue(row, nameof(PoLine.DueDate)));
    }

    [Theory]
    [InlineData(nameof(PoLine.Qty), "abc")]
    [InlineData(nameof(PoLine.Qty), "")]          // decimal은 null 불가
    [InlineData(nameof(PoLine.DueDate), "2026-13-45")]
    public void GS_007_PasteInvalid_KeepsValue(string field, string text)
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        var before = ds.GetValue(row, field);

        Assert.False(ds.TrySetText(row, field, text, De));

        Assert.Equal(before, ds.GetValue(row, field));
        Assert.Equal(RowState.Normal, ds.GetState(row));
    }

    [Fact]
    public void GS_007_PasteText_StoredAsIs()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];

        Assert.True(ds.TrySetText(row, nameof(PoLine.ItemCode), "B2001", De));

        Assert.Equal("B2001", ds.GetValue(row, nameof(PoLine.ItemCode)));
    }
}
