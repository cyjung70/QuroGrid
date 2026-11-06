using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-106: ShowRowState in the browser (A-74). Sample page /row-state, rows Cherry · Apple · Banana, columns Name · Qty · Kind · Note,
/// 300px wide so it scrolls sideways, deleted rows shown with a strikethrough. ?numbers=true adds row numbers, ?frozen=1 freezes Name.
/// The column takes no focus and isn't copied. The symbol follows the row state, also after Ctrl+Z and after accepting changes.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_106_RowStateTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        Locale = "en-US",
        Permissions = ["clipboard-read", "clipboard-write"],
    };

    private ILocator Grid => Page.Locator("[role=grid]");

    private ILocator Row(string name) => Page.Locator("[role=grid] [role=row][data-row]").Filter(new() { HasText = name });

    private ILocator Cell(string name, string field) => Row(name).Locator($"[data-field={field}]");

    private ILocator StateOf(string name) => Row(name).Locator(".quro-row-state");

    private async Task OpenAsync(string query = "")
    {
        await Page.GotoAsync($"{server.BaseUrl}/row-state{query}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private static Task<string> Symbol(ILocator cell) =>
        cell.EvaluateAsync<string>("e => getComputedStyle(e, '::before').content");

    private static Task<double> Left(ILocator el) => el.EvaluateAsync<double>("e => e.getBoundingClientRect().left");

    private static Task<double> Right(ILocator el) => el.EvaluateAsync<double>("e => e.getBoundingClientRect().right");

    private Task ExpectActive(ILocator cell) => Expect(cell).ToHaveCSSAsync("outline-style", "solid");

    private async Task EditNote(string name, string text)
    {
        await Cell(name, "Note").ClickAsync();
        await Page.Keyboard.TypeAsync(text);
        await Page.Keyboard.PressAsync("Enter");
    }

    [Fact]
    public async Task GS_106_Edit_ShowsTheSymbol_CtrlZClearsIt()
    {
        await OpenAsync();
        await Expect(StateOf("Cherry")).Not.ToHaveAttributeAsync("data-state", "updated");

        await EditNote("Cherry", "x");

        await Expect(StateOf("Cherry")).ToHaveAttributeAsync("data-state", "updated");
        Assert.Equal("\"●\"", await Symbol(StateOf("Cherry")));
        await Expect(StateOf("Cherry")).ToHaveAttributeAsync("title", "Edited");

        await Page.Keyboard.PressAsync("Control+z");

        await Expect(StateOf("Cherry")).Not.ToHaveAttributeAsync("data-state", "updated");
    }

    [Fact]
    public async Task GS_106_Insert_And_Delete_ShowTheirSymbols()
    {
        await OpenAsync();
        await Cell("Banana", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("Insert");

        var added = Page.Locator("[role=grid] .quro-row-state[data-state=inserted]");
        await Expect(added).ToHaveCountAsync(1);
        Assert.Equal("\"+\"", await Symbol(added));

        await Cell("Apple", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+Delete");

        await Expect(StateOf("Apple")).ToHaveAttributeAsync("data-state", "deleted");
        Assert.Equal("\"−\"", await Symbol(StateOf("Apple")));
        await Expect(StateOf("Apple")).ToHaveCSSAsync("opacity", "1");   // the row fades, its mark doesn't
    }

    [Fact]
    public async Task GS_106_Symbols_UseTheirColors()
    {
        await OpenAsync();
        await EditNote("Cherry", "x");
        await Cell("Banana", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Insert");
        await Cell("Apple", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+Delete");
        await Expect(StateOf("Apple")).ToHaveAttributeAsync("data-state", "deleted");

        await Expect(StateOf("Cherry")).ToHaveCSSAsync("color", "rgb(46, 107, 230)");
        await Expect(Page.Locator("[role=grid] .quro-row-state[data-state=inserted]")).ToHaveCSSAsync("color", "rgb(7, 148, 85)");
        await Expect(StateOf("Apple")).ToHaveCSSAsync("color", "rgb(217, 45, 32)");
    }

    [Fact]
    public async Task GS_106_DeletedRow_FrozenCellsStayOpaque_SoScrolledCellsDontShowThrough()
    {
        await OpenAsync("?numbers=true&frozen=1");
        await Cell("Apple", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+Delete");
        await Expect(StateOf("Apple")).ToHaveAttributeAsync("data-state", "deleted");

        await Expect(Cell("Apple", "Name")).ToHaveCSSAsync("opacity", "1");
        await Expect(Row("Apple").Locator(".quro-row-number")).ToHaveCSSAsync("opacity", "1");
        await Expect(Cell("Apple", "Qty")).ToHaveCSSAsync("opacity", "0.6");   // an ordinary cell still fades
        await Expect(Cell("Apple", "Name")).ToHaveCSSAsync("text-decoration-line", "line-through");
    }

    [Fact]
    public async Task GS_106_Accept_ClearsTheSymbols()
    {
        await OpenAsync();
        await EditNote("Cherry", "x");
        await Expect(StateOf("Cherry")).ToHaveAttributeAsync("data-state", "updated");

        await Page.GetByTestId("accept").ClickAsync();

        await Expect(Page.Locator("[role=grid] .quro-row-state[data-state]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GS_106_Click_TakesNoFocus_OpenEditorStays()
    {
        await OpenAsync();
        await Cell("Apple", "Name").ClickAsync();
        await ExpectActive(Cell("Apple", "Name"));
        await Page.Keyboard.PressAsync("F2");
        await Expect(Grid.Locator("input.quro-editor")).ToBeFocusedAsync();

        await StateOf("Banana").ClickAsync();

        await Expect(Grid.Locator("input.quro-editor")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task GS_106_ArrowLeft_StopsAtTheFirstColumn()
    {
        await OpenAsync();
        await Cell("Apple", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("ArrowLeft");

        await ExpectActive(Cell("Apple", "Name"));
    }

    [Fact]
    public async Task GS_106_Copy_LeavesItOut()
    {
        await OpenAsync();
        await EditNote("Apple", "x");
        await Cell("Apple", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("Control+c");

        Assert.Equal("Apple", (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).Trim());
    }

    [Fact]
    public async Task GS_106_StaysOnTheLeft_WhenScrolling_WithNumbersAndFrozenColumn()
    {
        await OpenAsync("?numbers=true&frozen=1");
        var number = Row("Apple").Locator(".quro-row-number");
        var state = StateOf("Apple");
        var name = Cell("Apple", "Name");
        var start = await Left(state);

        await Grid.EvaluateAsync("e => e.scrollLeft = e.scrollWidth");

        Assert.Equal(start, await Left(state), 1);
        Assert.Equal(await Right(number), await Left(state), 1);
        Assert.Equal(await Right(state), await Left(name), 1);
    }

    [Fact]
    public async Task GS_106_KeyboardScrolling_KeepsTheActiveCellClearOfTheGutter()
    {
        await OpenAsync("?numbers=true");
        await Cell("Apple", "Note").ClickAsync();
        await Grid.EvaluateAsync("e => e.scrollLeft = e.scrollWidth");

        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await ExpectActive(Cell("Apple", "Name"));

        Assert.True(await Left(Cell("Apple", "Name")) >= await Right(StateOf("Apple")) - 1);
    }

    [Fact]
    public async Task GS_106_HeaderCell_LinesUpWithTheBody()
    {
        await OpenAsync();

        var head = Page.Locator("[role=grid] .quro-head .quro-row-state");
        Assert.Equal(await Left(head), await Left(StateOf("Apple")), 1);
        Assert.Equal(await Right(head), await Right(StateOf("Apple")), 1);
    }
}
