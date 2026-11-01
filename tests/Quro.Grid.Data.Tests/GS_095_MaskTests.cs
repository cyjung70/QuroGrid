using Quro.Grid.Data;

namespace Quro.Grid.Data.Tests;

/// <summary>GS-095: QuroMask is a public Quro.Grid.Data type shared by the grid Mask cell and QuroMaskBox (A-56, A-69).</summary>
public class GS_095_MaskTests
{
    [Fact]
    public void GS_095_Mask_ReadsWithOrWithoutFixedCharacters()
    {
        var mask = QuroMask.Parse("000-00-00000");

        Assert.Equal(12, mask.Length);
        Assert.True(mask.TryRead("123-45-67890", out var a));
        Assert.Equal("1234567890", a);
        Assert.True(mask.TryRead("1234567890", out var b));
        Assert.Equal("1234567890", b);
    }

    [Theory]
    [InlineData("12345")]          // short
    [InlineData("123456789012")]   // long
    [InlineData("12A-45-67890")]   // letter in a digit position
    public void GS_095_Mask_RejectsMisfits(string text) =>
        Assert.False(QuroMask.Parse("000-00-00000").TryRead(text, out _));

    [Fact]
    public void GS_095_Mask_FormatsStoredValue()
    {
        var mask = QuroMask.Parse(@"AA\-000");

        Assert.Equal("KR-123", mask.Format("KR123"));
        Assert.Equal("K1", mask.Format("K1"));   // doesn't fit: as it is
    }

    [Fact]
    public void GS_095_Mask_WithoutInputPosition_Throws()
    {
        var e = Assert.Throws<ArgumentException>(() => QuroMask.Parse("--", "Mask"));
        Assert.Equal("Mask", e.ParamName);
        Assert.Throws<ArgumentException>(() => QuroMask.Parse(null));
    }
}
