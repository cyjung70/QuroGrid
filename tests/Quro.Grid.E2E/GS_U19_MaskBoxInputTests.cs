using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-U19: QuroMaskBox input in the browser (Control Spec 11, A-69, R2). Sample page /mask: one customer row with
/// boxes business number (000-00-00000, stored 1234567890, AutoSelect) and phone (000-0000-0000, empty).
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_U19_MaskBoxInputTests(SampleServer server) : PageTest
{
    private int _sentFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Box(string field) => Page.Locator($".quro-ui-mask[data-field='{field}'] input");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync()
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, _) => _sentFrames++;
        await Page.GotoAsync($"{server.BaseUrl}/mask");
        // quro-ui.js marks a box when it is attached (circuit connected)
        await Page.WaitForSelectorAsync(".quro-ui-mask[data-field='Phone'][data-quro-attached]");
    }

    [Fact]
    public async Task GS_U19_Typing_AddsFixedCharacters_DropsWrong_NoServerCallUntilBlur()
    {
        await OpenAsync();
        await Box("Phone").FocusAsync();
        await Page.WaitForTimeoutAsync(300);   // let focus-related frames settle
        var before = _sentFrames;

        await Box("Phone").PressSequentiallyAsync("010x1234");
        await Expect(Box("Phone")).ToHaveValueAsync("010-1234");   // dash added, letter dropped
        await Box("Phone").PressSequentiallyAsync("56789");
        await Expect(Box("Phone")).ToHaveValueAsync("010-1234-5678");   // stops at the end of the mask
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _sentFrames);   // R2: typing stays in the browser

        await Box("Phone").PressAsync("Tab");
        await Expect(Status("phone-value")).ToHaveTextAsync("01012345678");
        await Expect(Status("state")).ToHaveTextAsync("Updated");
    }

    [Fact]
    public async Task GS_U19_Paste_WithoutDashes_EnterCommits()
    {
        await OpenAsync();

        await Box("Phone").FillAsync("01098765432");   // fill = one input event with the whole text, like a paste
        await Expect(Box("Phone")).ToHaveValueAsync("010-9876-5432");
        await Box("Phone").PressAsync("Enter");

        await Expect(Status("phone-value")).ToHaveTextAsync("01098765432");
    }

    [Fact]
    public async Task GS_U19_Incomplete_RevertsOnBlur()
    {
        await OpenAsync();

        await Box("BizNo").FillAsync("22081");
        await Expect(Box("BizNo")).ToHaveValueAsync("220-81");
        await Box("BizNo").PressAsync("Tab");

        await Expect(Box("BizNo")).ToHaveValueAsync("123-45-67890");
        await Expect(Status("biz-value")).ToHaveTextAsync("1234567890");
        await Expect(Status("state")).ToHaveTextAsync("Normal");
    }

    [Fact]
    public async Task GS_U19_EditInMiddle_KeepsCaret()
    {
        await OpenAsync();
        await Box("Phone").FillAsync("0101234");
        await Expect(Box("Phone")).ToHaveValueAsync("010-1234");

        // put the caret after "010-12" and type a digit: the rest shifts, caret stays after the new digit
        await Box("Phone").EvaluateAsync("i => i.setSelectionRange(6, 6)");
        await Box("Phone").PressSequentiallyAsync("9");

        await Expect(Box("Phone")).ToHaveValueAsync("010-1293-4");
        Assert.Equal(7, await Box("Phone").EvaluateAsync<int>("i => i.selectionStart"));
    }

    [Fact]
    public async Task GS_U19_StaticText_ShowsMasked()
    {
        await OpenAsync();
        await Expect(Page.Locator(".quro-ui-static")).ToHaveTextAsync("123-45-67890");

        await Box("BizNo").FillAsync("2208162517");
        await Box("BizNo").PressAsync("Tab");

        await Expect(Page.Locator(".quro-ui-static")).ToHaveTextAsync("220-81-62517");
    }
}
