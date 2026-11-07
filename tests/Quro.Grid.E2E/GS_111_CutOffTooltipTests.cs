using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-111: tooltip for cut-off text. While the pointer is on a cell whose text is cut off, quro-grid.js gives it a title with the full
/// text and takes it away afterwards, with no server call. A mark's own tooltip wins. A MergeRepeated cell that looks blank always shows
/// its value. /cut-off has rows 1 to 3: long Name, short Name, long Name with a warning mark.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_111_CutOffTooltipTests(SampleServer server) : PageTest
{
    private const string LongName = "A very long product name that is cut off";

    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private async Task OpenAsync(string query = "")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            if (payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal))
                _invokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/cut-off{query}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid] [role=row][data-row]");
    }

    [Fact]
    public async Task GS_111_CutOffCell_GetsTheFullText_WhileThePointerIsOnIt()
    {
        await OpenAsync();
        await Expect(Cell(1, "Name")).Not.ToHaveAttributeAsync("title", LongName);

        await Cell(1, "Name").HoverAsync();
        await Expect(Cell(1, "Name")).ToHaveAttributeAsync("title", LongName);

        await Cell(2, "Name").HoverAsync();
        Assert.Null(await Cell(1, "Name").GetAttributeAsync("title"));
    }

    [Fact]
    public async Task GS_111_LeavingTheGrid_RemovesIt()
    {
        await OpenAsync();
        await Cell(1, "Name").HoverAsync();
        await Expect(Cell(1, "Name")).ToHaveAttributeAsync("title", LongName);

        await Page.Locator("h1").HoverAsync();

        await Expect(Cell(1, "Name")).Not.ToHaveAttributeAsync("title", LongName);
    }

    [Fact]
    public async Task GS_111_TextThatFits_GetsNothing()
    {
        await OpenAsync();

        await Cell(2, "Name").HoverAsync();
        await Cell(1, "Name").HoverAsync();     // proves the handler ran before the check
        await Cell(2, "Name").HoverAsync();

        Assert.Null(await Cell(2, "Name").GetAttributeAsync("title"));
    }

    [Fact]
    public async Task GS_111_Hovering_MakesNoServerCalls()
    {
        await OpenAsync();
        var before = _invokeFrames;

        await Cell(1, "Name").HoverAsync();
        await Expect(Cell(1, "Name")).ToHaveAttributeAsync("title", LongName);
        await Cell(1, "Qty").HoverAsync();
        await Header("Kind").HoverAsync();
        await Page.Locator("h1").HoverAsync();

        Assert.Equal(before, _invokeFrames);
    }

    [Fact]
    public async Task GS_111_UsesTheShownText()
    {
        await OpenAsync();

        await Cell(1, "Qty").HoverAsync();
        await Expect(Cell(1, "Qty")).ToHaveAttributeAsync("title", "1,234,567.00");

        await Cell(1, "Kind").HoverAsync();
        await Expect(Cell(1, "Kind")).ToHaveAttributeAsync("title", "A rather long option name");
    }

    [Fact]
    public async Task GS_111_HeaderAndFooter_AlsoGetIt()
    {
        await OpenAsync();

        await Header("Kind").HoverAsync();
        await Expect(Header("Kind")).ToHaveAttributeAsync("title", "A long column heading for a narrow column");

        var footer = Page.Locator("[data-footer-field=Qty]");
        await footer.HoverAsync();
        await Expect(footer).ToHaveAttributeAsync("title", "1,234,590.00");
    }

    [Fact]
    public async Task GS_111_Subtotal_AlsoGetsIt()
    {
        await OpenAsync();

        await Page.GetByTestId("group").ClickAsync();
        var subtotal = Page.Locator(".quro-subtotal-row [data-subtotal-field=Qty]").First;
        await Expect(subtotal).ToBeVisibleAsync();
        await subtotal.HoverAsync();

        await Expect(subtotal).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex(@"^1,234,\d{3}\.00$"));
    }

    [Fact]
    public async Task GS_111_MarkedCell_KeepsItsMessage()
    {
        await OpenAsync();
        var marked = Cell(3, "Name");
        await Expect(marked).ToHaveAttributeAsync("title", "Check this name");

        await marked.HoverAsync();
        await Expect(marked).ToHaveAttributeAsync("title", "Check this name");

        await Cell(1, "Name").HoverAsync();
        await Expect(marked).ToHaveAttributeAsync("title", "Check this name");
    }

    [Fact]
    public async Task GS_111_RepeatedCell_LooksBlank_AlwaysShowsItsValue()
    {
        await OpenAsync();

        await Cell(2, "Note").HoverAsync();
        await Expect(Cell(2, "Note")).ToHaveAttributeAsync("title", "ok");

        await Cell(1, "Note").HoverAsync();     // the first in the run shows its short value, so it gets nothing
        await Expect(Cell(2, "Note")).Not.ToHaveAttributeAsync("title", "ok");
        Assert.Null(await Cell(1, "Note").GetAttributeAsync("title"));
    }

    [Fact]
    public async Task GS_111_OpenEditor_And_OtherCellKinds_GetNothing()
    {
        await OpenAsync();

        await Cell(1, "Name").DblClickAsync();
        await Expect(Cell(1, "Name").Locator("input.quro-editor")).ToBeVisibleAsync();
        await Cell(2, "Active").HoverAsync();
        await Cell(1, "Name").HoverAsync();
        Assert.Null(await Cell(1, "Name").GetAttributeAsync("title"));
        await Page.Keyboard.PressAsync("Escape");

        await Cell(1, "Active").HoverAsync();
        Assert.Null(await Cell(1, "Active").GetAttributeAsync("title"));
        await Cell(1, "Go").HoverAsync();
        Assert.Null(await Cell(1, "Go").GetAttributeAsync("title"));
    }

    [Fact]
    public async Task GS_111_RowNumberColumn_GetsNothing()
    {
        await OpenAsync();

        var number = Page.Locator("[role=row][data-row='1'] .quro-row-number");
        await number.HoverAsync();

        Assert.Null(await number.GetAttributeAsync("title"));
    }

    [Fact]
    public async Task GS_111_AfterAnEdit_TheNextHoverUsesTheNewText()
    {
        await OpenAsync();

        await Cell(2, "Name").DblClickAsync();
        await Expect(Cell(2, "Name").Locator("input.quro-editor")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Control+a");
        await Page.Keyboard.TypeAsync("Now this name is long enough to be cut off");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Cell(2, "Name")).ToHaveTextAsync("Now this name is long enough to be cut off");

        await Cell(1, "Name").HoverAsync();
        await Cell(2, "Name").HoverAsync();

        await Expect(Cell(2, "Name")).ToHaveAttributeAsync("title", "Now this name is long enough to be cut off");
    }

    [Fact]
    public async Task GS_111_WideningAColumn_StopsTheTooltip()
    {
        await OpenAsync("?wide=true");

        await Cell(1, "Name").HoverAsync();
        await Cell(2, "Name").HoverAsync();
        await Cell(1, "Name").HoverAsync();

        Assert.Null(await Cell(1, "Name").GetAttributeAsync("title"));
    }
}
