using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-U04: QuroTextBox input in the browser (Control Spec 8, R2). Sample page /textbox: one customer row with
/// boxes zip (Digits, max 5, AutoSelect), name (Alpha), code (AlphaNumeric), pin (Password).
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_U04_TextBoxInputTests(SampleServer server) : PageTest
{
    private int _sentFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Box(string field) => Page.Locator($".quro-ui-textbox[data-field='{field}'] input");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync()
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, _) => _sentFrames++;
        await Page.GotoAsync($"{server.BaseUrl}/textbox");
        // quro-ui.js marks a box when it is attached (circuit connected)
        await Page.WaitForSelectorAsync(".quro-ui-textbox[data-field='Zip'][data-quro-attached]");
    }

    [Fact]
    public async Task GS_U04_Digits_FiltersTyping_NoServerCallUntilBlur()
    {
        await OpenAsync();
        await Box("Zip").FocusAsync();
        await Box("Zip").PressAsync("Control+A");
        await Page.WaitForTimeoutAsync(300);   // let focus-related frames settle
        var before = _sentFrames;

        await Box("Zip").PressSequentiallyAsync("12a3b");
        await Expect(Box("Zip")).ToHaveValueAsync("123");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _sentFrames);   // R2: typing stays in the browser

        await Box("Zip").PressAsync("Tab");
        await Expect(Status("zip-value")).ToHaveTextAsync("123");
        await Expect(Status("state")).ToHaveTextAsync("Updated");
    }

    [Fact]
    public async Task GS_U04_Digits_FiltersPaste_AndEnterCommits()
    {
        await OpenAsync();

        await Box("Zip").FillAsync("9x8y7");   // fill = one input event with the whole text, like a paste
        await Expect(Box("Zip")).ToHaveValueAsync("987");
        await Box("Zip").PressAsync("Enter");

        await Expect(Status("zip-value")).ToHaveTextAsync("987");
    }

    [Fact]
    public async Task GS_U04_MaxLength_StopsTyping()
    {
        await OpenAsync();
        await Box("Zip").FillAsync("");

        await Box("Zip").PressSequentiallyAsync("1234567");

        await Expect(Box("Zip")).ToHaveValueAsync("12345");
    }

    [Fact]
    public async Task GS_U04_AlphaAndAlphaNumeric()
    {
        await OpenAsync();

        await Box("Name").FillAsync("");
        await Box("Name").PressSequentiallyAsync("Ab1-c");
        await Expect(Box("Name")).ToHaveValueAsync("Abc");

        await Box("Code").FillAsync("");
        await Box("Code").PressSequentiallyAsync("a-1_B 2");
        await Expect(Box("Code")).ToHaveValueAsync("a1B2");
    }

    [Fact]
    public async Task GS_U04_AutoSelect_OnTabAndClick()
    {
        await OpenAsync();

        await Box("Name").FocusAsync();
        await Box("Name").PressAsync("Shift+Tab");   // box order: Zip, Name, Code, Pin
        Assert.Equal(new[] { 0, 5 }, await SelectionAsync("Zip"));   // "04524"

        await Box("Name").ClickAsync();
        await Box("Zip").ClickAsync();
        Assert.Equal(new[] { 0, 5 }, await SelectionAsync("Zip"));

        await Box("Name").ClickAsync();   // no AutoSelect: caret only
        var name = await SelectionAsync("Name");
        Assert.Equal(name[0], name[1]);
    }

    [Fact]
    public async Task GS_U04_Password_IsMasked()
    {
        await OpenAsync();

        Assert.Equal("password", await Box("Pin").GetAttributeAsync("type"));
    }

    private Task<int[]> SelectionAsync(string field) =>
        Box(field).EvaluateAsync<int[]>("e => [e.selectionStart, e.selectionEnd]");
}
