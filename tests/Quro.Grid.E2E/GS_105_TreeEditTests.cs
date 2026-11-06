using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-105: adding and deleting in a tree, in the browser (A-61). Sample page /tree, shown order
/// Services · Repair · Goods · Tools · Hammer · Paint. Insert adds a sibling, Ctrl+Insert a child, Ctrl+Delete a row with its
/// descendants. Each is one server call, and Ctrl+Z undoes it in one step.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_105_TreeEditTests(SampleServer server) : PageTest
{
    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Names => Page.Locator("[role=treegrid] [role=gridcell][data-field=Name]");

    private ILocator Rows => Page.Locator("[role=treegrid] [role=row][data-row]");

    private ILocator Cell(string name, string field) => Rows.Filter(new() { HasText = name }).Locator($"[data-field={field}]");

    private async Task OpenAsync()
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            if (payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal)
                && !payload.Contains("SelectionChanged", StringComparison.Ordinal))
                _invokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/tree");
        await Page.WaitForSelectorAsync("[role=treegrid][data-quro-grid]");
    }

    [Fact]
    public async Task GS_105_CtrlInsert_AddsAChild_AndMovesToIt()
    {
        await OpenAsync();
        await Cell("Paint", "Name").ClickAsync();
        var before = _invokeFrames;

        await Page.Keyboard.PressAsync("Control+Insert");

        await Expect(Rows).ToHaveCountAsync(7);
        await Expect(Rows.Nth(6)).ToHaveAttributeAsync("aria-level", "3");
        await Expect(Rows.Nth(5)).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Rows.Nth(6).Locator("[data-field=Name]")).ToHaveCSSAsync("outline-style", "solid");
        Assert.Equal(1, _invokeFrames - before);
    }

    [Fact]
    public async Task GS_105_CtrlInsert_OnACollapsedRow_OpensIt()
    {
        await OpenAsync();
        await Cell("Goods", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+ArrowLeft");
        await Expect(Rows).ToHaveCountAsync(3);

        await Page.Keyboard.PressAsync("Control+Insert");

        await Expect(Rows).ToHaveCountAsync(7);
        await Expect(Rows.Nth(6)).ToHaveAttributeAsync("aria-level", "2");
    }

    [Fact]
    public async Task GS_105_CtrlInsert_OnARowWithoutAKey_AddsNothing()
    {
        await OpenAsync();
        await Cell("Paint", "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Insert");   // the new row has no Code yet
        await Expect(Rows).ToHaveCountAsync(7);

        await Page.Keyboard.PressAsync("Control+Insert");
        await Page.GetByTestId("expand-all").ClickAsync();   // a later server round trip: anything added would be drawn by now

        await Expect(Rows).ToHaveCountAsync(7);
    }

    [Fact]
    public async Task GS_105_Insert_AddsASibling_BelowTheRowsChildren()
    {
        await OpenAsync();
        await Cell("Tools", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("Insert");

        await Expect(Rows).ToHaveCountAsync(7);
        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods", "Tools", "Hammer", "", "Paint"]);
        await Expect(Rows.Nth(5)).ToHaveAttributeAsync("aria-level", "2");
    }

    [Fact]
    public async Task GS_105_CtrlDelete_DeletesTheSubtree_OneCall_CtrlZRestoresIt()
    {
        await OpenAsync();
        await Cell("Goods", "Name").ClickAsync();
        var before = _invokeFrames;

        await Page.Keyboard.PressAsync("Control+Delete");

        await Expect(Names).ToHaveTextAsync(["Services", "Repair"]);
        Assert.Equal(1, _invokeFrames - before);

        await Page.Keyboard.PressAsync("Control+z");

        await Expect(Names).ToHaveTextAsync(["Services", "Repair", "Goods", "Tools", "Hammer", "Paint"]);
    }

    [Fact]
    public async Task GS_105_AfterDeletingTheActiveSubtree_TheActiveCellIsOnAShownRow()
    {
        await OpenAsync();
        await Cell("Goods", "Name").ClickAsync();

        await Page.Keyboard.PressAsync("Control+Delete");
        await Expect(Names).ToHaveCountAsync(2);

        await Expect(Page.Locator("[role=treegrid] [role=gridcell][data-field=Name]").Last).ToHaveCSSAsync("outline-style", "solid");
    }
}
