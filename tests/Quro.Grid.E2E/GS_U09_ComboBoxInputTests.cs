using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-U09: QuroComboBox in the browser (Control Spec 9, R2, A-64). Sample page /combo: one customer row (KR, 01 Seoul)
/// with combos Country (empty item), City (linked to Country, code + name, extra column) and Tags (multi-select).
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_U09_ComboBoxInputTests(SampleServer server) : PageTest
{
    private int _sentFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Combo(string field) => Page.Locator($".quro-ui-combo[data-field='{field}']");

    private ILocator Box(string field) => Combo(field).Locator("input.quro-ui-input");

    private ILocator Shown(string field) => Combo(field).Locator(".quro-ui-combo-list li[data-index]:not([hidden])");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync()
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, _) => _sentFrames++;
        await Page.GotoAsync($"{server.BaseUrl}/combo");
        await Page.WaitForSelectorAsync(".quro-ui-combo[data-field='Tags'][data-quro-attached]");
    }

    [Fact]
    public async Task GS_U09_Typing_FiltersInBrowser_EnterChooses()
    {
        await OpenAsync();
        await Expect(Box("City")).ToHaveValueAsync("01 Seoul");
        await Box("City").FocusAsync();
        await Box("City").PressAsync("Control+A");
        await Page.WaitForTimeoutAsync(300);
        var before = _sentFrames;

        await Box("City").PressSequentiallyAsync("bu");
        await Expect(Shown("City")).ToHaveTextAsync([new System.Text.RegularExpressions.Regex("Busan")]);
        await Box("City").PressSequentiallyAsync("x");
        await Expect(Shown("City")).ToHaveCountAsync(0);
        await Box("City").PressAsync("Backspace");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _sentFrames);   // R2: filtering stays in the browser

        await Box("City").PressAsync("Enter");

        await Expect(Status("city-value")).ToHaveTextAsync("02");
        await Expect(Box("City")).ToHaveValueAsync("02 Busan");
        await Expect(Status("state")).ToHaveTextAsync("Updated");
        await Expect(Combo("City")).Not.ToHaveAttributeAsync("data-quro-open", "");
    }

    [Fact]
    public async Task GS_U09_SearchByCode_ArrowKeys()
    {
        await OpenAsync();
        await Box("City").FocusAsync();
        await Box("City").PressAsync("Control+A");

        await Box("City").PressSequentiallyAsync("0");   // codes 01, 02, 03
        await Expect(Shown("City")).ToHaveCountAsync(3);
        await Box("City").PressAsync("ArrowDown");
        await Box("City").PressAsync("ArrowDown");
        await Box("City").PressAsync("ArrowUp");
        await Box("City").PressAsync("Enter");

        await Expect(Status("city-value")).ToHaveTextAsync("02");
    }

    [Fact]
    public async Task GS_U09_EscapeAndBlur_RestoreLastChoice()
    {
        await OpenAsync();

        await Box("City").FocusAsync();
        await Box("City").PressAsync("Control+A");
        await Box("City").PressSequentiallyAsync("zz");
        await Box("City").PressAsync("Escape");
        await Expect(Box("City")).ToHaveValueAsync("01 Seoul");

        await Box("City").PressSequentiallyAsync("tok");
        await Box("Tags").FocusAsync();
        await Expect(Box("City")).ToHaveValueAsync("01 Seoul");
        await Expect(Status("city-value")).ToHaveTextAsync("01");
        await Expect(Status("state")).ToHaveTextAsync("Normal");
    }

    [Fact]
    public async Task GS_U09_MouseChoice_AndLinkedChildCleared()
    {
        await OpenAsync();

        await Box("Country").ClickAsync();
        await Combo("Country").Locator("li[data-index]", new() { HasText = "Japan" }).ClickAsync();

        await Expect(Status("country-value")).ToHaveTextAsync("JP");
        await Expect(Status("city-value")).ToHaveTextAsync("");   // Seoul left the list
        await Box("City").ClickAsync();
        await Expect(Shown("City")).ToHaveCountAsync(2);
        await Expect(Combo("City").Locator(".quro-ui-combo-head span")).ToHaveTextAsync(["Name", "Region"]);

        await Combo("City").Locator("li[data-index]", new() { HasText = "Osaka" }).ClickAsync();
        await Expect(Status("city-value")).ToHaveTextAsync("12");
    }

    [Fact]
    public async Task GS_U09_Multiple_SpaceAndClickToggle()
    {
        await OpenAsync();

        await Box("Tags").FocusAsync();
        await Box("Tags").PressAsync("ArrowDown");   // opens on the first item
        await Box("Tags").PressAsync(" ");
        await Box("Tags").PressAsync("ArrowDown");
        await Box("Tags").PressAsync("ArrowDown");
        await Box("Tags").PressAsync(" ");
        await Expect(Status("tags-value")).ToHaveTextAsync("01|03");
        await Expect(Combo("Tags")).ToHaveAttributeAsync("data-quro-open", "");   // stays open

        await Combo("Tags").Locator("li[data-index]", new() { HasText = "Seoul" }).ClickAsync();
        await Expect(Status("tags-value")).ToHaveTextAsync("03");
        await Expect(Box("Tags")).ToHaveValueAsync("Incheon");
    }
}
