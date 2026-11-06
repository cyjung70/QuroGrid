using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-102: default cell alignment in the browser. /edit-select has Id (Label over an int) · Name · Qty (Number) · Kind (Combo);
/// /row-readonly has Active (Check) · Go (Button). Numbers are right, text left, Check·Combo·Button centered.
/// Editors follow their cell; the Combo list stays left. Headers stay left.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_102_CellAlignTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private async Task OpenAsync(string path)
    {
        await Page.GotoAsync($"{server.BaseUrl}/{path}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private static Task<string> TextAlign(ILocator el) => el.EvaluateAsync<string>("e => getComputedStyle(e).textAlign");

    /// <summary>Distance between the centers of an element and its parent cell.</summary>
    private static Task<double> OffCenter(ILocator el) => el.EvaluateAsync<double>(
        "e => { const a = e.getBoundingClientRect(), b = e.closest('[role=gridcell]').getBoundingClientRect(); return Math.abs((a.left + a.right) / 2 - (b.left + b.right) / 2); }");

    [Fact]
    public async Task GS_102_Cells_AlignByType()
    {
        await OpenAsync("edit-select");

        Assert.Equal("start", await TextAlign(Cell(1, "Name")));
        Assert.Equal("right", await TextAlign(Cell(1, "Qty")));
        Assert.Equal("right", await TextAlign(Cell(1, "Id")));       // Label over a numeric field
        Assert.Equal("center", await TextAlign(Cell(1, "Kind")));
        Assert.Equal("start", await TextAlign(Page.Locator("[role=columnheader][data-field=Qty]")));   // decision 6
    }

    [Fact]
    public async Task GS_102_CheckAndButton_SitInTheMiddleOfTheCell()
    {
        await OpenAsync("row-readonly");

        Assert.True(await OffCenter(Cell(1, "Active").Locator(".quro-check")) < 1.5);
        Assert.True(await OffCenter(Cell(1, "Go").Locator(".quro-button")) < 1.5);
    }

    [Fact]
    public async Task GS_102_SelectEditor_FollowsCell_ComboListStaysLeft()
    {
        await OpenAsync("edit-select");

        await Cell(1, "Qty").ClickAsync();
        Assert.Equal("right", await TextAlign(Cell(1, "Qty").Locator(".quro-editor")));

        await Cell(1, "Kind").ClickAsync();
        var editor = Cell(1, "Kind").Locator(".quro-editor");
        await Expect(editor).ToBeFocusedAsync();
        Assert.Equal("center", await TextAlign(editor));

        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        var option = Page.Locator(".quro-picker .quro-option").First;
        await Expect(option).ToBeVisibleAsync();
        Assert.Equal("left", await TextAlign(option));
    }

    [Fact]
    public async Task GS_102_ServerEditor_FollowsCell()
    {
        await OpenAsync("row-readonly");

        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        var editor = Cell(1, "Qty").Locator(".quro-editor");
        await Expect(editor).ToBeFocusedAsync();

        Assert.Equal("right", await TextAlign(editor));
    }
}
