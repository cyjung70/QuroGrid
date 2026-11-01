using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-096: Image cell in the browser (A-56). Sample page /cells: row 1 Photo = img/box.svg (alt "Blue box"),
/// row 2 no photo. [data-testid=clicks] counts OnCellClick, [data-testid=clicked] = "rowKey:value".
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_096_ImageCellTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(int row, string field) => Page.Locator($"[role=row][data-row='{row}'] [data-field='{field}']");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync()
    {
        await Page.GotoAsync($"{server.BaseUrl}/cells");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    [Fact]
    public async Task GS_096_Image_FitsRowHeight_ClickRaisesOnCellClick()
    {
        await OpenAsync();
        var img = Cell(1, "Photo").Locator("img.quro-image");
        await Expect(img).ToHaveAttributeAsync("alt", "Blue box");
        await Expect(img).ToHaveJSPropertyAsync("complete", true);

        var cellBox = await Cell(1, "Photo").BoundingBoxAsync();
        var imgBox = await img.BoundingBoxAsync();
        Assert.True(imgBox!.Height <= cellBox!.Height, $"image {imgBox.Height}px taller than cell {cellBox.Height}px");
        Assert.InRange(imgBox.Width / imgBox.Height, 1.9, 2.1);   // 40×20 keeps its 2:1 ratio

        await img.ClickAsync();

        await Expect(Status("clicks")).ToHaveTextAsync("1");
        await Expect(Status("clicked")).ToHaveTextAsync("1:img/box.svg");
    }

    [Fact]
    public async Task GS_096_EnterAndSpace_RaiseOnCellClick_NoEditor()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");   // Photo cell, without clicking the image

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Status("clicks")).ToHaveTextAsync("1");
        await Page.Keyboard.PressAsync(" ");
        await Expect(Status("clicks")).ToHaveTextAsync("2");

        await Page.Keyboard.TypeAsync("x");
        await Page.Keyboard.PressAsync("F2");
        await Expect(Page.Locator(".quro-editor")).ToHaveCountAsync(0);   // not editable
    }

    [Fact]
    public async Task GS_096_ClickingEmptyImageCell_DoesNotRaise()
    {
        await OpenAsync();

        await Cell(2, "Photo").ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        await Expect(Status("clicks")).ToHaveTextAsync("0");
    }
}
