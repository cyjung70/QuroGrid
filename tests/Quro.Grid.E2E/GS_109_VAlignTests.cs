using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-109: vertical cell alignment in the browser. /valign is a paged grid with 56px rows: Name has no VAlign, Kind is Top, Note is Middle
/// with long text, Qty is Bottom and right-aligned, Active is a Middle Check. The grid's own CSS has to place the text, and it must keep
/// the ellipsis, the horizontal alignment and the place of an open editor.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_109_VAlignTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(string field) => Page.Locator($"[role=grid] [role=row][data-row] [data-field='{field}']").First;

    private async Task OpenAsync()
    {
        await Page.GotoAsync($"{server.BaseUrl}/valign");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid] [role=row][data-row]");
    }

    /// <summary>Gaps between the cell's text and the cell's top and bottom edges.</summary>
    private static Task<double[]> TextGaps(ILocator cell) => cell.EvaluateAsync<double[]>(
        """
        e => {
            const range = document.createRange();
            range.selectNodeContents(e);
            const t = range.getBoundingClientRect(), c = e.getBoundingClientRect();
            return [t.top - c.top, c.bottom - t.bottom];
        }
        """);

    private static Task<double[]> ChildGaps(ILocator cell, string child) => cell.EvaluateAsync<double[]>(
        """
        (e, child) => {
            const t = e.querySelector(child).getBoundingClientRect(), c = e.getBoundingClientRect();
            return [t.top - c.top, c.bottom - t.bottom];
        }
        """, child);

    private static void AssertTop(double[] gaps) => Assert.True(gaps[0] < 6 && gaps[1] > 20, $"top {gaps[0]}, bottom {gaps[1]}");

    private static void AssertMiddle(double[] gaps) => Assert.True(Math.Abs(gaps[0] - gaps[1]) < 3, $"top {gaps[0]}, bottom {gaps[1]}");

    private static void AssertBottom(double[] gaps) => Assert.True(gaps[1] < 6 && gaps[0] > 20, $"top {gaps[0]}, bottom {gaps[1]}");

    [Fact]
    public async Task GS_109_RowsAreTallEnoughToShowIt()
    {
        await OpenAsync();

        Assert.Equal(56, await Cell("Name").EvaluateAsync<double>("e => e.getBoundingClientRect().height"));
    }

    [Fact]
    public async Task GS_109_NoVAlign_AndTop_StayAtTheTop()
    {
        await OpenAsync();

        AssertTop(await TextGaps(Cell("Name")));
        AssertTop(await TextGaps(Cell("Kind")));
    }

    [Fact]
    public async Task GS_109_Middle_And_Bottom_MoveTheText()
    {
        await OpenAsync();

        AssertMiddle(await TextGaps(Cell("Note")));
        AssertBottom(await TextGaps(Cell("Qty")));
    }

    [Fact]
    public async Task GS_109_Middle_MovesACheckBox()
    {
        await OpenAsync();

        AssertMiddle(await ChildGaps(Cell("Active"), ".quro-check"));
    }

    [Fact]
    public async Task GS_109_CutOffText_KeepsItsEllipsis()
    {
        await OpenAsync();

        var note = Cell("Note");
        Assert.True(await note.EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth"));
        Assert.Equal("ellipsis", await note.EvaluateAsync<string>("e => getComputedStyle(e).textOverflow"));
        Assert.Equal("nowrap", await note.EvaluateAsync<string>("e => getComputedStyle(e).whiteSpace"));
        Assert.Equal("block", await note.EvaluateAsync<string>("e => getComputedStyle(e).display"));
    }

    [Fact]
    public async Task GS_109_HorizontalAlignment_StillApplies()
    {
        await OpenAsync();

        Assert.Equal("right", await Cell("Qty").EvaluateAsync<string>("e => getComputedStyle(e).textAlign"));
    }

    [Fact]
    public async Task GS_109_OpenEditor_StaysWhereTheValueIs()
    {
        await OpenAsync();

        await Cell("Note").DblClickAsync();
        await Expect(Cell("Note").Locator("input.quro-editor")).ToBeVisibleAsync();
        AssertMiddle(await ChildGaps(Cell("Note"), "input.quro-editor"));
        await Page.Keyboard.PressAsync("Escape");

        await Cell("Qty").DblClickAsync();
        await Expect(Cell("Qty").Locator("input.quro-editor")).ToBeVisibleAsync();
        AssertBottom(await ChildGaps(Cell("Qty"), "input.quro-editor"));
    }

    [Fact]
    public async Task GS_109_Footer_FollowsTheColumn()
    {
        await OpenAsync();

        var footer = Page.Locator("[data-footer-field=Qty]");
        await Expect(footer).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("quro-valign-bottom"));
        await Expect(footer).ToHaveTextAsync("465");
    }
}
