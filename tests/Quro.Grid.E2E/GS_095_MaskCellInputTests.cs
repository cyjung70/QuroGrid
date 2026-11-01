using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-095: Mask cell editor in the browser (A-56, R2). Sample page /mask: a grid on the same customer data set with
/// Mask columns BizNo (000-00-00000, stored 1234567890) and Phone (000-0000-0000, empty). Row key 1.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_095_MaskCellInputTests(SampleServer server) : PageTest
{
    private int _invokeFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Cell(string field) => Page.Locator($"[role=grid] [role=row][data-row='1'] [data-field='{field}']");

    private ILocator Editor => Page.Locator("[role=grid] .quro-editor");

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
        await Page.GotoAsync($"{server.BaseUrl}/mask");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    [Fact]
    public async Task GS_095_Typing_AddsFixedCharacters_DropsWrong_OneCallOnCommit()
    {
        await OpenAsync();
        await Expect(Cell("BizNo")).ToHaveTextAsync("123-45-67890");
        await Cell("Phone").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        await Expect(Editor).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(300);
        var before = _invokeFrames;

        await Page.Keyboard.TypeAsync("010x1234");
        await Expect(Editor).ToHaveValueAsync("010-1234");   // dash added, letter dropped
        await Page.Keyboard.TypeAsync("56789");
        await Expect(Editor).ToHaveValueAsync("010-1234-5678");   // stops at the end of the mask
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _invokeFrames);   // R2: typing stays in the browser

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Status("phone-value")).ToHaveTextAsync("01012345678");
        await Expect(Cell("Phone")).ToHaveTextAsync("010-1234-5678");
        Assert.Equal(before + 1, _invokeFrames);   // CommitEdit once
    }

    [Fact]
    public async Task GS_095_TypingToStartEdit_DropsMisfitFirstCharacter()
    {
        await OpenAsync();
        await Cell("Phone").ClickAsync();

        await Page.Keyboard.TypeAsync("x0");

        await Expect(Editor).ToHaveValueAsync("0");
    }

    [Fact]
    public async Task GS_095_Incomplete_KeepsEditing()
    {
        await OpenAsync();
        await Cell("BizNo").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        await Editor.FillAsync("22081");
        await Expect(Editor).ToHaveValueAsync("220-81");

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Editor).ToBeVisibleAsync();   // not committed: the edit stays open
        await Expect(Status("biz-value")).ToHaveTextAsync("1234567890");
    }
}
