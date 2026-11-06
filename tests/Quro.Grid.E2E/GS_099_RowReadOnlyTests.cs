using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-099: RowReadOnly in the browser (QuroERP request R6). Sample page /row-readonly, rows 1 Apple · 2 Pear (locked) ·
/// 3 Plum, columns Id (Label) · Name · Qty · Active (Check) · Go (Button). ?select=true turns EditOnSelect on.
/// The button [data-testid=lock-first] flips the lock of row 1 and calls Refresh.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_099_RowReadOnlyTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Grid => Page.Locator("[role=grid]");

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private ILocator Editor => Page.Locator("[role=grid] .quro-editor");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync(bool select = false)
    {
        await Page.GotoAsync($"{server.BaseUrl}/row-readonly{(select ? "?select=true" : "")}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private static Task<string> Background(ILocator cell) =>
        cell.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");

    [Fact]
    public async Task GS_099_LockedRow_IsGrey_AriaReadOnly()
    {
        await OpenAsync();

        await Expect(Cell(2, "Name")).ToHaveAttributeAsync("aria-readonly", "true");
        Assert.NotEqual(await Background(Cell(1, "Name")), await Background(Cell(2, "Name")));
        Assert.Equal(await Background(Cell(2, "Name")), await Background(Cell(2, "Qty")));
    }

    [Fact]
    public async Task GS_099_LockedRow_TypingF2DoubleClick_NoEditor()
    {
        await OpenAsync();

        await Cell(2, "Name").ClickAsync();
        await Expect(Grid).ToHaveAttributeAsync("data-active-row", "2");   // a locked cell still takes focus
        await Page.Keyboard.TypeAsync("x");
        await Page.Keyboard.PressAsync("F2");
        await Cell(2, "Name").DblClickAsync();
        await Page.WaitForTimeoutAsync(300);

        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(2, "Name")).ToHaveTextAsync("Pear");
    }

    [Fact]
    public async Task GS_099_Tab_SkipsLockedRow()
    {
        await OpenAsync();

        await Cell(1, "Active").ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Grid).ToHaveAttributeAsync("data-active-row", "3");
        await Expect(Grid).ToHaveAttributeAsync("data-active-field", "Name");
    }

    [Fact]
    public async Task GS_099_Check_ClickAndSpace_Refused()
    {
        await OpenAsync();

        await Cell(2, "Active").Locator(".quro-check").ClickAsync();
        await Page.Keyboard.PressAsync("Space");
        await Page.WaitForTimeoutAsync(300);

        await Expect(Cell(2, "Active").Locator(".quro-check")).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(Status("has-changes")).ToHaveTextAsync("False");
    }

    [Fact]
    public async Task GS_099_Button_StillClicks_CopyStillWorks()
    {
        await OpenAsync();
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);

        await Cell(2, "Go").Locator(".quro-button").ClickAsync();
        await Expect(Status("go-row")).ToHaveTextAsync("Pear");

        await Cell(2, "Name").ClickAsync();
        await Page.Keyboard.PressAsync("Control+c");
        Assert.Equal("Pear", await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()"));
    }

    [Fact]
    public async Task GS_099_EditOnSelect_NoEditorOnLockedRow()
    {
        await OpenAsync(select: true);

        await Cell(1, "Name").ClickAsync();
        await Expect(Cell(1, "Name").Locator(".quro-editor")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Grid).ToHaveAttributeAsync("data-active-row", "2");
        await Expect(Editor).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Cell(3, "Name").Locator(".quro-editor")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task GS_099_LockArrives_ClosesSelectEditor_UnlockReopens()
    {
        await OpenAsync(select: true);
        await Cell(1, "Name").ClickAsync();
        await Expect(Cell(1, "Name").Locator(".quro-editor")).ToBeVisibleAsync();

        await Status("lock-first").ClickAsync();
        await Expect(Cell(1, "Name")).ToHaveAttributeAsync("aria-readonly", "true");
        await Expect(Editor).ToHaveCountAsync(0);

        await Cell(1, "Name").ClickAsync();
        await Page.WaitForTimeoutAsync(200);
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Status("has-changes")).ToHaveTextAsync("False");   // the lock is not row state

        await Status("lock-first").ClickAsync();
        await Expect(Cell(1, "Name")).Not.ToHaveAttributeAsync("aria-readonly", "true");
        await Cell(1, "Qty").ClickAsync();
        await Expect(Cell(1, "Qty").Locator(".quro-editor")).ToBeFocusedAsync();
    }
}
