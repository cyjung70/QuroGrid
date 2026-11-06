using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-100: ShowRowNumbers in the browser (QuroERP request R7). Sample page /row-numbers, rows Cherry · Apple · Banana,
/// columns Name · Qty · Kind · Note, 300px wide so it scrolls sideways. ?frozen=1 freezes Name.
/// The number column takes no focus and is not copied; it renumbers without changing row state, also after Ctrl+Z.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_100_RowNumberTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Grid => Page.Locator("[role=grid]");

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private ILocator NumberOf(int row) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [role=rowheader]");

    private ILocator Numbers => Page.Locator("[role=grid] [role=rowheader]");

    private ILocator Names => Page.Locator("[role=grid] [role=gridcell][data-field=Name]");

    private async Task OpenAsync(bool frozen = false)
    {
        await Page.GotoAsync($"{server.BaseUrl}/row-numbers{(frozen ? "?frozen=1" : "")}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private static Task<double> Left(ILocator el) => el.EvaluateAsync<double>("e => e.getBoundingClientRect().left");

    private static Task<double> Right(ILocator el) => el.EvaluateAsync<double>("e => e.getBoundingClientRect().right");

    [Fact]
    public async Task GS_100_InsertDeleteUndo_Renumbers()
    {
        await OpenAsync();
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3"]);
        await Expect(Grid).Not.ToHaveAttributeAsync("data-dirty", "true");

        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Insert");
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3", "4"]);

        await Cell(2, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+Delete");
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3"]);

        await Page.Keyboard.PressAsync("Control+z");
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3", "4"]);

        await Page.Keyboard.PressAsync("Control+z");
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3"]);
        await Expect(Names).ToHaveTextAsync(["Cherry", "Apple", "Banana"]);
        await Expect(Grid).Not.ToHaveAttributeAsync("data-dirty", "true");
    }

    [Fact]
    public async Task GS_100_HeaderSort_KeepsOneToN_FromTheTop()
    {
        await OpenAsync();

        await Page.Locator("[role=columnheader][data-field=Name]").ClickAsync();

        await Expect(Names).ToHaveTextAsync(["Apple", "Banana", "Cherry"]);
        await Expect(Numbers).ToHaveTextAsync(["1", "2", "3"]);
    }

    [Fact]
    public async Task GS_100_Click_SelectsNothing_ArrowsAndTabSkipIt()
    {
        await OpenAsync();

        await NumberOf(1).ClickAsync();
        await Page.WaitForTimeoutAsync(200);
        Assert.Null(await Grid.GetAttributeAsync("data-active-row"));

        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Grid).ToHaveAttributeAsync("data-active-field", "Name");

        await Cell(2, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(Grid).ToHaveAttributeAsync("data-active-row", "1");
        await Expect(Grid).ToHaveAttributeAsync("data-active-field", "Note");   // the last column of the row above, not the number
    }

    [Fact]
    public async Task GS_100_ClickWhileEditing_KeepsEditor()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.TypeAsync("x");
        var editor = Cell(1, "Name").Locator(".quro-editor");
        await Expect(editor).ToBeFocusedAsync();

        await NumberOf(2).ClickAsync();

        await Expect(editor).ToBeFocusedAsync();
        await Expect(editor).ToHaveValueAsync("x");
    }

    [Fact]
    public async Task GS_100_Copy_LeavesNumberOut()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowRight");

        await Page.Keyboard.PressAsync("Control+c");

        Assert.Equal("Cherry\t10", await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()"));
    }

    [Fact]
    public async Task GS_100_SideScroll_NumberStays_CellsNotHiddenUnderIt()
    {
        await OpenAsync();
        var header = Page.Locator(".quro-head .quro-row-number");
        Assert.Equal(await Right(header), await Right(NumberOf(1)));   // header and body line up

        var start = await Left(NumberOf(1));
        await Cell(1, "Note").ClickAsync();
        Assert.True(await Grid.EvaluateAsync<double>("e => e.scrollLeft") > 0);
        Assert.Equal(start, await Left(NumberOf(1)));
        Assert.Equal(start, await Left(header));

        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Grid).ToHaveAttributeAsync("data-active-field", "Name");
        Assert.True(await Left(Cell(1, "Name")) >= await Right(NumberOf(1)) - 1);   // scrolled clear of the sticky column
    }

    [Fact]
    public async Task GS_100_FrozenColumn_SitsAfterNumber_WhileScrolled()
    {
        await OpenAsync(frozen: true);

        await Cell(1, "Note").ClickAsync();
        await Grid.EvaluateAsync("e => e.scrollLeft = e.scrollWidth");   // all the way: past the width of the grid itself
        Assert.True(await Grid.EvaluateAsync<double>("e => e.scrollLeft") > 150);

        Assert.Equal(await Right(NumberOf(1)), await Left(Cell(1, "Name")), tolerance: 1.0);
        Assert.Equal(await Right(Page.Locator(".quro-head .quro-row-number")), await Left(Page.Locator("[role=columnheader][data-field=Name]")), tolerance: 1.0);
    }

    [Fact]
    public async Task GS_100_ThreeDigits_FitWithoutClipping()
    {
        await OpenAsync();
        await Page.Locator("[data-testid=add-100]").ClickAsync();
        await Expect(Page.Locator("[role=grid] [role=rowheader]", new() { HasText = "103" })).ToBeVisibleAsync();

        var clipped = await Page.Locator("[role=grid] [role=rowheader]", new() { HasText = "103" })
            .EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth");
        Assert.False(clipped);
    }
}
