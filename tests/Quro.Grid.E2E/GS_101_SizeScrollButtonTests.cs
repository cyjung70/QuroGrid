using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-101: Width and ScrollButtons in the browser. Sample page /scroll-buttons: 300x150 grid, 30 rows, four 120px columns.
/// ?buttons=false turns the buttons off, ?rows=2 starts with two rows, ?size=false leaves the size unset, ?frozen=1 freezes Name.
/// A button shows only where there is more to scroll. One press moves one row or column, holding repeats, no server call.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_101_SizeScrollButtonTests(SampleServer server) : PageTest
{
    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Grid => Page.Locator("[role=grid]");

    private ILocator Button(string dir) => Page.Locator($".quro-scroll-button[data-scroll={dir}]");

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private async Task OpenAsync(string query = "")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            if (payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal)
                && !payload.Contains("SelectionChanged", StringComparison.Ordinal))
                _invokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/scroll-buttons{query}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private Task<double> ScrollTop() => Grid.EvaluateAsync<double>("e => e.scrollTop");

    private Task<double> ScrollLeft() => Grid.EvaluateAsync<double>("e => e.scrollLeft");

    private Task<double> RowHeight() => Page.Locator("[role=grid] .quro-body [role=row]").First.EvaluateAsync<double>("e => e.offsetHeight");

    private async Task PressAsync(ILocator button)
    {
        await button.HoverAsync();
        await Page.Mouse.DownAsync();
        await Page.Mouse.UpAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?buttons=false")]
    public async Task GS_101_Size_IsWhatWasGiven(string query)
    {
        await OpenAsync(query);

        var box = await Grid.BoundingBoxAsync();
        Assert.Equal(300, box!.Width, tolerance: 0.5);
        Assert.Equal(150, box.Height, tolerance: 0.5);
        Assert.True(await Grid.EvaluateAsync<bool>("e => e.scrollHeight > e.clientHeight && e.scrollWidth > e.clientWidth"));
    }

    [Fact]
    public async Task GS_101_Buttons_HaveTheGridColors()
    {
        await OpenAsync();
        await Expect(Button("down")).ToBeVisibleAsync();

        var look = await Button("down").EvaluateAsync<string[]>("e => { const c = getComputedStyle(e); return [c.backgroundColor, c.borderTopColor]; }");
        var header = await Page.Locator("[role=columnheader][data-field=Name]").EvaluateAsync<string[]>("e => { const c = getComputedStyle(e); return [c.backgroundColor, c.borderRightColor]; }");

        Assert.Equal(header, look);   // the --qg-* tokens reach the frame, which is outside the grid
    }

    [Fact]
    public async Task GS_101_Off_NoButtons_NativeScrollStillWorks()
    {
        await OpenAsync("?buttons=false");

        await Expect(Page.Locator(".quro-scroll-button")).ToHaveCountAsync(0);
        await Grid.EvaluateAsync("e => e.scrollTop = 40");
        Assert.Equal(40, await ScrollTop());
    }

    [Fact]
    public async Task GS_101_Shown_OnlyWhereThereIsMoreToScroll()
    {
        await OpenAsync();

        await Expect(Button("down")).ToBeVisibleAsync();
        await Expect(Button("right")).ToBeVisibleAsync();
        await Expect(Button("up")).ToBeHiddenAsync();
        await Expect(Button("left")).ToBeHiddenAsync();

        await Grid.EvaluateAsync("e => { e.scrollTop = e.scrollHeight; e.scrollLeft = e.scrollWidth; }");

        await Expect(Button("up")).ToBeVisibleAsync();
        await Expect(Button("left")).ToBeVisibleAsync();
        await Expect(Button("down")).ToBeHiddenAsync();
        await Expect(Button("right")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task GS_101_NoOverflow_NoButtons()
    {
        await OpenAsync("?size=false&rows=2");

        await Expect(Grid).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(200);
        foreach (var dir in new[] { "up", "down", "left", "right" })
            await Expect(Button(dir)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task GS_101_Grows_ButtonAppears()
    {
        await OpenAsync("?rows=2");
        await Expect(Button("right")).ToBeVisibleAsync();
        await Expect(Button("down")).ToBeHiddenAsync();

        await Page.Locator("[data-testid=add-rows]").ClickAsync();

        await Expect(Button("down")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GS_101_UpDown_OneRow()
    {
        await OpenAsync();
        var row = await RowHeight();

        await PressAsync(Button("down"));
        await Expect(Button("up")).ToBeVisibleAsync();
        Assert.Equal(row, await ScrollTop(), tolerance: 1);

        await PressAsync(Button("down"));
        Assert.Equal(row * 2, await ScrollTop(), tolerance: 1);

        await PressAsync(Button("up"));
        Assert.Equal(row, await ScrollTop(), tolerance: 1);
    }

    [Fact]
    public async Task GS_101_LeftRight_OneColumn_ToTheEnd()
    {
        await OpenAsync();

        await PressAsync(Button("right"));
        Assert.Equal(120, await ScrollLeft(), tolerance: 1);
        await Expect(Button("left")).ToBeVisibleAsync();

        await PressAsync(Button("left"));
        Assert.Equal(0, await ScrollLeft(), tolerance: 1);
        await Expect(Button("left")).ToBeHiddenAsync();

        await PressAsync(Button("right"));
        await Expect(Button("left")).ToBeVisibleAsync();
        await PressAsync(Button("right"));   // 480px of columns in 300px: the second press reaches the end
        await Expect(Button("right")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task GS_101_Frozen_ColumnStopsAfterFrozenCells()
    {
        await OpenAsync("?frozen=1");

        await PressAsync(Button("right"));

        var frozenRight = await Cell(1, "Name").EvaluateAsync<double>("e => e.getBoundingClientRect().right");
        var next = await Cell(1, "Kind").EvaluateAsync<double>("e => e.getBoundingClientRect().left");
        Assert.Equal(frozenRight, next, tolerance: 1);
    }

    [Fact]
    public async Task GS_101_Hold_Repeats_ReleaseStops()
    {
        await OpenAsync();
        var row = await RowHeight();

        await Button("down").HoverAsync();
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(900);
        await Page.Mouse.UpAsync();

        var stopped = await ScrollTop();
        Assert.True(stopped > row * 2);

        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(stopped, await ScrollTop());
    }

    [Fact]
    public async Task GS_101_KeepsEditorOpen_NoServerCall()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.TypeAsync("x");
        var editor = Cell(1, "Name").Locator(".quro-editor");
        await Expect(editor).ToBeFocusedAsync();
        await Page.WaitForTimeoutAsync(300);
        var before = _invokeFrames;

        await PressAsync(Button("down"));
        await PressAsync(Button("right"));
        await Page.WaitForTimeoutAsync(300);

        Assert.True(await ScrollTop() > 0);
        await Expect(editor).ToBeFocusedAsync();
        await Expect(editor).ToHaveValueAsync("x");
        Assert.Equal(before, _invokeFrames);
    }

    [Fact]
    public async Task GS_101_KeyboardScroll_UpdatesButtons()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();

        for (var i = 0; i < 10; i++)
            await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Button("up")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GS_101_Buttons_ClearOfNativeScrollbars()
    {
        await OpenAsync();
        await Expect(Button("down")).ToBeVisibleAsync();

        var inner = await Grid.EvaluateAsync<double[]>("e => { const r = e.getBoundingClientRect(); return [r.left + e.clientLeft + e.clientWidth, r.top + e.clientTop + e.clientHeight]; }");

        Assert.True((await Button("down").BoundingBoxAsync())!.X + (await Button("down").BoundingBoxAsync())!.Width <= inner[0] + 0.5);
        Assert.True((await Button("right").BoundingBoxAsync())!.Y + (await Button("right").BoundingBoxAsync())!.Height <= inner[1] + 0.5);
    }
}
