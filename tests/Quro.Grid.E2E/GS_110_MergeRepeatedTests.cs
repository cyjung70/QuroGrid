using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-110: MergeRepeated in the browser. /merge-repeated has merged Kind and Name columns over rows 1 to 7:
/// Fruit Apple, Fruit Apple, Fruit Pear, Veg Pear, Veg Onion, Veg blank, Veg blank. Hidden text comes back in the active cell, a selected
/// range and an open editor. Copy still gives the real values, and keys move through hidden cells as usual.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_110_MergeRepeatedTests(SampleServer server) : PageTest
{
    private const string Transparent = "rgba(0, 0, 0, 0)";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        Locale = "en-US",
        Permissions = ["clipboard-read", "clipboard-write"],
    };

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private async Task OpenAsync(string query = "")
    {
        await Page.GotoAsync($"{server.BaseUrl}/merge-repeated{query}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid] [role=row][data-row]");
    }

    private static Task<string> Color(ILocator cell) => cell.EvaluateAsync<string>("e => getComputedStyle(e).color");

    [Fact]
    public async Task GS_110_RepeatedText_IsHidden_FirstInARunShows()
    {
        await OpenAsync();

        Assert.NotEqual(Transparent, await Color(Cell(1, "Kind")));
        Assert.Equal(Transparent, await Color(Cell(2, "Kind")));
        Assert.Equal(Transparent, await Color(Cell(3, "Kind")));
        Assert.NotEqual(Transparent, await Color(Cell(4, "Kind")));
    }

    [Fact]
    public async Task GS_110_ChangeOnTheLeft_ShowsTheValueAgain()
    {
        await OpenAsync();

        Assert.Equal("Pear", (await Cell(4, "Name").InnerTextAsync()).Trim());
        Assert.NotEqual(Transparent, await Color(Cell(4, "Name")));
    }

    [Fact]
    public async Task GS_110_NoLineInsideARun_LineAtItsEnd()
    {
        await OpenAsync();

        await Expect(Cell(1, "Kind")).ToHaveCSSAsync("border-bottom-color", Transparent);
        await Expect(Cell(2, "Kind")).ToHaveCSSAsync("border-bottom-color", Transparent);
        Assert.NotEqual(Transparent, await Cell(3, "Kind").EvaluateAsync<string>("e => getComputedStyle(e).borderBottomColor"));
    }

    [Fact]
    public async Task GS_110_ActiveCell_ShowsItsValue_UntilYouLeave()
    {
        await OpenAsync();

        await Cell(2, "Kind").ClickAsync();
        await Expect(Cell(2, "Kind")).Not.ToHaveCSSAsync("color", Transparent);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Cell(2, "Kind")).ToHaveCSSAsync("color", Transparent);
        await Expect(Cell(3, "Kind")).Not.ToHaveCSSAsync("color", Transparent);
    }

    [Fact]
    public async Task GS_110_SelectedRange_ShowsItsValues()
    {
        await OpenAsync();

        await Cell(1, "Kind").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Expect(Cell(2, "Kind")).Not.ToHaveCSSAsync("color", Transparent);
        await Expect(Cell(3, "Kind")).Not.ToHaveCSSAsync("color", Transparent);
    }

    [Fact]
    public async Task GS_110_Copy_GivesTheRealValues()
    {
        await OpenAsync();

        await Cell(1, "Kind").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Page.Keyboard.PressAsync("Control+c");

        var copied = (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).Replace("\r\n", "\n").Trim();
        Assert.Equal("Fruit\nFruit\nFruit", copied);
    }

    [Fact]
    public async Task GS_110_Editor_ShowsTheValue()
    {
        await OpenAsync();

        await Cell(2, "Kind").DblClickAsync();

        var editor = Cell(2, "Kind").Locator("input.quro-editor");
        await Expect(editor).ToHaveValueAsync("Fruit");
        await Expect(editor).Not.ToHaveCSSAsync("color", Transparent);
    }

    [Fact]
    public async Task GS_110_SelectEditor_ShowsTheValue()
    {
        await OpenAsync("?select=true");

        await Cell(2, "Kind").ClickAsync();

        var editor = Cell(2, "Kind").Locator("input.quro-select-editor");
        await Expect(editor).ToHaveValueAsync("Fruit");
        await Expect(editor).Not.ToHaveCSSAsync("color", Transparent);
    }

    [Fact]
    public async Task GS_110_EditingAValue_SplitsTheRun()
    {
        await OpenAsync();
        await Expect(Cell(2, "Name")).ToHaveCSSAsync("color", Transparent);

        await Cell(1, "Name").DblClickAsync();
        await Page.Keyboard.PressAsync("Control+a");
        await Page.Keyboard.TypeAsync("Plum");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Cell(1, "Name")).ToContainTextAsync("Plum");
        await Expect(Cell(2, "Name")).Not.ToHaveCSSAsync("color", Transparent);
    }

    [Fact]
    public async Task GS_110_DeletedRow_ShowsItsValue()
    {
        await OpenAsync();

        await Cell(2, "Kind").ClickAsync();
        await Page.Keyboard.PressAsync("Control+Delete");

        await Expect(Page.Locator("[role=row][data-row='2'].quro-deleted")).ToBeVisibleAsync();
        Assert.DoesNotContain("quro-merge-repeat", await Cell(2, "Kind").GetAttributeAsync("class") ?? "");
        Assert.DoesNotContain("quro-merge-repeat", await Cell(3, "Kind").GetAttributeAsync("class") ?? "");
    }

    [Fact]
    public async Task GS_110_BlankValues_KeepTheirLines()
    {
        await OpenAsync();

        Assert.NotEqual(Transparent, await Cell(6, "Name").EvaluateAsync<string>("e => getComputedStyle(e).borderBottomColor"));
    }
}
