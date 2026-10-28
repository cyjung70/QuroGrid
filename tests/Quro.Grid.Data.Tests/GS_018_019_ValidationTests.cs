namespace Quro.Grid.Data.Tests;

/// <summary>GS-018 ~ GS-019: 필수·범위 검증 (Spec 4.4·4.5·4.6). 규칙은 SetRule로 등록.</summary>
public class GS_018_019_ValidationTests
{
    /// <summary>GS-018: 필수 필드가 빈 행 → 해당 셀 오류, 결과 실패</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GS_018_Validate_RequiredEmpty_Fails(string? itemCode)
    {
        var ds = TestData.TwoLines();
        ds.SetRule(nameof(PoLine.ItemCode), new FieldRule { Required = true });
        var row = ds.AddRow();
        ds.SetValue(row, nameof(PoLine.ItemCode), itemCode);

        var result = ds.Validate();

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(row, error.RowKey);
        Assert.Equal(nameof(PoLine.ItemCode), error.Field);
        Assert.Equal(ValidationKeys.Required, error.MessageKey);
    }

    [Fact]
    public void GS_018_Validate_RequiredFilled_Passes()
    {
        var ds = TestData.TwoLines();
        ds.SetRule(nameof(PoLine.ItemCode), new FieldRule { Required = true });
        var row = ds.AddRow();
        ds.SetValue(row, nameof(PoLine.ItemCode), "A1003");

        Assert.True(ds.Validate().IsValid);
    }

    /// <summary>GS-019: Min·Max 범위 밖 숫자 → 해당 셀 오류</summary>
    [Theory]
    [InlineData(-1, ValidationKeys.Min)]
    [InlineData(1001, ValidationKeys.Max)]
    public void GS_019_Validate_OutOfRange_Fails(int qty, string expectedKey)
    {
        var ds = TestData.TwoLines();
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { Min = 0, Max = 1000 });
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), (decimal)qty);

        var result = ds.Validate();

        var error = Assert.Single(result.Errors);
        Assert.Equal(row, error.RowKey);
        Assert.Equal(nameof(PoLine.Qty), error.Field);
        Assert.Equal(expectedKey, error.MessageKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void GS_019_Validate_OnBoundary_Passes(int qty)
    {
        var ds = TestData.TwoLines();
        ds.SetRule(nameof(PoLine.Qty), new FieldRule { Min = 0, Max = 1000 });
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), (decimal)qty);

        Assert.True(ds.Validate().IsValid);
    }
}
