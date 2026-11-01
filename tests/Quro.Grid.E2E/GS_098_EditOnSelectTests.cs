using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-098: EditOnSelect in the browser (A-58, 2026-11-01 decisions 1–5). Sample page /edit-select, columns
/// Id (Label) · Name (Text) · Qty (Number N0, row 1 = 1234.5 shown "1,235") · Kind (Combo A Alpha / B Beta).
/// The editor opens on the selected cell without a server call, text selected; typing replaces, arrows leave the cell,
/// F2 switches to caret mode. Leaving commits once and only when the text changed.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_098_EditOnSelectTests(SampleServer server) : PageTest
{
    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(int row, string field) => Page.Locator($"[role=grid] [role=row][data-row='{row}'] [data-field='{field}']");

    private ILocator Editor => Page.Locator("[role=grid] .quro-editor");

    private ILocator EditorIn(int row, string field) => Cell(row, field).Locator(".quro-editor");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync()
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            if (payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal)
                && !payload.Contains("SelectionChanged", StringComparison.Ordinal))
                _invokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/edit-select");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    private static Task<string> SelectedText(ILocator input) =>
        input.EvaluateAsync<string>("e => e.value.substring(e.selectionStart, e.selectionEnd)");

    [Fact]
    public async Task GS_098_Select_OpensEditor_TextSelected_NoServerCall()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var before = _invokeFrames;

        await Expect(EditorIn(1, "Name")).ToBeFocusedAsync();
        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Apple");
        Assert.Equal("Apple", await SelectedText(EditorIn(1, "Name")));

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(EditorIn(1, "Qty")).ToHaveValueAsync("1234.5");   // the stored value, not the shown "1,235"
        await Expect(Editor).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(EditorIn(1, "Kind")).ToHaveValueAsync("Alpha");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(EditorIn(2, "Kind")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(EditorIn(1, "Qty")).ToBeFocusedAsync();
        await Page.WaitForTimeoutAsync(300);

        Assert.Equal(before, _invokeFrames);   // R2: moving and opening editors stays in the browser
        await Expect(Status("qty-value")).ToHaveTextAsync("1234.5");   // unchanged text: nothing committed
    }

    [Fact]
    public async Task GS_098_Typing_Replaces_ArrowCommitsOnce()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var before = _invokeFrames;

        await Page.Keyboard.TypeAsync("Plum");
        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Plum");
        Assert.Equal(before, _invokeFrames);

        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Status("name-value")).ToHaveTextAsync("Plum");
        await Expect(EditorIn(2, "Name")).ToHaveValueAsync("Pear");
        await Expect(Cell(1, "Name")).ToHaveTextAsync("Plum");
        Assert.Equal(before + 1, _invokeFrames);   // CommitEdit once
    }

    [Fact]
    public async Task GS_098_F2_CaretMode_ArrowsStayInEditor()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Expect(EditorIn(1, "Name")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("F2");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.TypeAsync("x");

        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Applxe");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Status("name-value")).ToHaveTextAsync("Applxe");
        await Expect(EditorIn(1, "Qty")).ToBeFocusedAsync();   // Enter moves right (O-05) and opens the next editor
    }

    [Fact]
    public async Task GS_098_Escape_RestoresText_NoServerCall()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var before = _invokeFrames;

        await Page.Keyboard.TypeAsync("Plum");
        await Page.Keyboard.PressAsync("Escape");

        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Apple");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(EditorIn(1, "Qty")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _invokeFrames);
        await Expect(Status("name-value")).ToHaveTextAsync("Apple");
    }

    [Fact]
    public async Task GS_098_FailedCommit_KeepsEditor_DoesNotMove()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Expect(EditorIn(1, "Qty")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("abc");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.WaitForTimeoutAsync(300);

        await Expect(EditorIn(1, "Qty")).ToHaveValueAsync("abc");
        await Expect(EditorIn(1, "Qty")).ToBeFocusedAsync();
        await Expect(Editor).ToHaveCountAsync(1);
        await Expect(Status("qty-value")).ToHaveTextAsync("1234.5");
    }

    [Fact]
    public async Task GS_098_Range_ClosesEditor_LabelHasNone()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Expect(EditorIn(1, "Name")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Page.Locator("[role=grid]")).ToHaveAttributeAsync("data-selection", "1,Name,1,Qty");

        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(EditorIn(1, "Name")).ToBeVisibleAsync();   // single cell again
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Editor).ToHaveCountAsync(0);               // Id is a Label: no editor
        await Expect(Page.Locator("[role=grid]")).ToHaveAttributeAsync("data-active-field", "Id");
    }

    [Fact]
    public async Task GS_098_UndoInUnchangedEditor_UndoesGrid_EditorFollows()
    {
        await OpenAsync();
        await Cell(1, "Name").ClickAsync();
        await Page.Keyboard.TypeAsync("Plum");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Status("name-value")).ToHaveTextAsync("Plum");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Plum");

        await Page.Keyboard.PressAsync("Control+z");   // unchanged text: the grid's Undo, not the input's

        await Expect(Status("name-value")).ToHaveTextAsync("Apple");
        await Expect(EditorIn(1, "Name")).ToHaveValueAsync("Apple");   // the editor follows the cell
        Assert.Equal("Apple", await SelectedText(EditorIn(1, "Name")));
    }

    [Fact]
    public async Task GS_098_AltDown_OpensComboList_PickCommits()
    {
        await OpenAsync();
        await Cell(1, "Kind").ClickAsync();
        await Expect(EditorIn(1, "Kind")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(Page.Locator(".quro-picker .quro-option")).ToHaveCountAsync(2);
        await Expect(Editor).ToHaveCountAsync(1);

        await Page.Locator(".quro-picker .quro-option", new() { HasText = "Beta" }).ClickAsync();

        await Expect(Status("kind-value")).ToHaveTextAsync("B");
        await Expect(EditorIn(1, "Kind")).ToHaveValueAsync("Beta");   // the browser editor is back on the cell
    }
}
