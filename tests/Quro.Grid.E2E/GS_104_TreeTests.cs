using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-104: tree display in the browser (A-61). Sample page /tree, shown order
/// Services · Repair · Goods · Tools · Hammer · Paint, columns Name · Code · Amount. ?select=true turns EditOnSelect on.
/// Clicking the toggle or Ctrl+←→ expands and collapses with one server call; plain ←→ still move cells.
/// The toggle adds no text to the cell, so editors and copy see only the value.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_104_TreeTests(SampleServer server) : PageTest
{
    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        Locale = "en-US",
        Permissions = ["clipboard-read", "clipboard-write"],
    };

    private ILocator Grid => Page.Locator("[role=treegrid]");

    private ILocator Names => Page.Locator("[role=treegrid] [role=gridcell][data-field=Name]");

    private ILocator Row(string name) => Page.Locator("[role=treegrid] [role=row][data-row]").Filter(new() { HasText = name });

    private ILocator Cell(string name, string field) => Row(name).Locator($"[data-field={field}]");

    private ILocator Toggle(string name) => Row(name).Locator(".quro-tree-toggle");

    private async Task OpenAsync(string query = "")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            if (payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal)
                && !payload.Contains("SelectionChanged", StringComparison.Ordinal))
                _invokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/tree{query}");
        await Page.WaitForSelectorAsync("[role=treegrid][data-quro-grid]");
    }

    private static Task<double> Left(ILocator el) => el.EvaluateAsync<double>("e => e.getBoundingClientRect().left");

    private static Task<double> TextLeft(ILocator cell) =>
        cell.EvaluateAsync<double>("e => { const r = document.createRange(); r.selectNodeContents(e.lastChild); return r.getBoundingClientRect().left; }");

    private Task ExpectActive(ILocator cell) =>
        Expect(cell).ToHaveCSSAsync("outline-style", "solid");

    [Fact]
    public async Task GS_104_ToggleClick_CollapsesAndExpands_OneServerCallEach()
    {
        await OpenAsync();
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods", "Tools", "Hammer", "Paint"]);

        await Toggle("Goods").ClickAsync();
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods"]);
        await Expect(Row("Goods")).ToHaveAttributeAsync("aria-expanded", "false");
        Assert.Equal(1, _invokeFrames);

        await Toggle("Goods").ClickAsync();
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods", "Tools", "Hammer", "Paint"]);
        Assert.Equal(2, _invokeFrames);
    }

    [Fact]
    public async Task GS_104_CtrlArrows_WorkOnAnyCellOfTheRow_PlainArrowsMoveCells()
    {
        await OpenAsync();
        await Cell("Goods", "Amount").ClickAsync();

        await Page.Keyboard.PressAsync("Control+ArrowLeft");
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods"]);
        await ExpectActive(Cell("Goods", "Amount"));   // stayed on the cell

        await Page.Keyboard.PressAsync("Control+ArrowRight");
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods", "Tools", "Hammer", "Paint"]);
        await ExpectActive(Cell("Goods", "Amount"));

        await Page.Keyboard.PressAsync("ArrowLeft");
        await ExpectActive(Cell("Goods", "Code"));
        await Expect(Names).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task GS_104_CtrlArrows_OnALeaf_DoNothing()
    {
        await OpenAsync();
        await Cell("Paint", "Name").ClickAsync();
        var before = _invokeFrames;

        await Page.Keyboard.PressAsync("Control+ArrowLeft");
        await Page.Keyboard.PressAsync("Control+ArrowRight");
        await ExpectActive(Cell("Paint", "Name"));

        Assert.Equal(before, _invokeFrames);
    }

    [Fact]
    public async Task GS_104_ArrowDown_SkipsCollapsedRows()
    {
        await OpenAsync();
        await Toggle("Services").ClickAsync();
        await Expect(Names).ToHaveCountAsync(5);
        await ExpectActive(Cell("Services", "Name"));

        await Page.Keyboard.PressAsync("ArrowDown");

        await ExpectActive(Cell("Goods", "Name"));
    }

    [Fact]
    public async Task GS_104_CollapsingOverTheActiveCell_MovesItToAShownRow()
    {
        await OpenAsync();
        await Cell("Hammer", "Code").ClickAsync();

        await Page.GetByTestId("collapse-all").ClickAsync();

        await Expect(Names).ToHaveTextAsync(["Services", "Goods"]);
        await Grid.FocusAsync();
        await Page.Keyboard.PressAsync("Control+c");
        var copied = await Page.EvaluateAsync<string>("navigator.clipboard.readText()");
        Assert.Contains(copied.Trim(), new[] { "A", "B" });
    }

    [Fact]
    public async Task GS_104_Indent_GrowsWithLevel_AndTextStaysClearOfTheToggle()
    {
        await OpenAsync();

        var root = await TextLeft(Cell("Goods", "Name"));
        var second = await TextLeft(Cell("Tools", "Name"));
        var third = await TextLeft(Cell("Hammer", "Name"));

        Assert.True(second > root && third > second, $"{root} {second} {third}");
        Assert.Equal(second - root, third - second, 1);
        var toggle = await Toggle("Goods").BoundingBoxAsync();
        Assert.True(toggle!.X + toggle.Width <= root + 0.5, $"{toggle.X + toggle.Width} {root}");
    }

    [Fact]
    public async Task GS_104_Copy_HasOnlyTheValue()
    {
        await OpenAsync();
        await Cell("Goods", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("Control+c");

        Assert.Equal("Goods", (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).Trim());
    }

    [Fact]
    public async Task GS_104_F2Edit_StartsWithTheValue_AndCommitKeepsTheTree()
    {
        await OpenAsync();
        await Cell("Goods", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("F2");
        var editor = Grid.Locator("input.quro-editor");   // the row no longer has the text while its editor is open
        await Expect(editor).ToHaveValueAsync("Goods");
        await Page.Keyboard.TypeAsync("X");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "GoodsX", "Tools", "Hammer", "Paint"]);
        await Expect(Toggle("GoodsX")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GS_104_EditOnSelect_EditorHasTheValue_ToggleStaysClickable()
    {
        await OpenAsync("?select=true");
        await Cell("Goods", "Name").ClickAsync();
        await Expect(Cell("Goods", "Name").Locator("input.quro-select-editor")).ToHaveValueAsync("Goods");

        await Toggle("Goods").ClickAsync();

        await Expect(Names).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task GS_104_EditOnSelect_CtrlArrows_Toggle()
    {
        await OpenAsync("?select=true");
        await Cell("Goods", "Code").ClickAsync();

        await Page.Keyboard.PressAsync("Control+ArrowLeft");

        await Expect(Names).ToHaveCountAsync(3);
        await Expect(Cell("Goods", "Code").Locator("input.quro-select-editor")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task GS_104_ExpandAllAndCollapseAll_Buttons()
    {
        await OpenAsync();

        await Page.GetByTestId("collapse-all").ClickAsync();
        await Expect(Names).ToHaveTextAsync(["Services", "Goods"]);

        await Page.GetByTestId("expand-all").ClickAsync();
        await Expect(Names).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task GS_104_FooterSum_IncludesCollapsedRows()
    {
        await OpenAsync();
        var footer = Page.Locator("[data-footer-field=Amount]");
        await Expect(footer).ToHaveTextAsync("111,111");

        await Toggle("Goods").ClickAsync();
        await Expect(Names).ToHaveCountAsync(3);

        await Expect(footer).ToHaveTextAsync("111,111");
    }
}
