using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-U13: QuroDatePicker calendar in the browser (Control Spec 10, R2, A-65). Sample page /date: one row with Due
/// (2026-10-30), From ~ To (2026-10-05 ~ 2026-10-20) and CloseMonth (2026-10); ?culture=en-US switches the format.
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_U13_DatePickerInputTests(SampleServer server) : PageTest
{
    private int _sentFrames;

    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private ILocator Picker(string field) => Page.Locator($".quro-ui-date[data-field='{field}']");

    private ILocator Box(string field, string part = "from") => Picker(field).Locator($"input[data-part='{part}']");

    private ILocator Pop(string field) => Picker(field).Locator(".quro-ui-date-pop");

    private ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

    private async Task OpenAsync(string culture = "en-US")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, _) => _sentFrames++;
        await Page.GotoAsync($"{server.BaseUrl}/date?culture={culture}");
        await Page.WaitForSelectorAsync(".quro-ui-date[data-field='CloseMonth'][data-quro-attached]");
    }

    [Fact]
    public async Task GS_U13_Calendar_MonthNavigationInBrowser_PickSetsOnce()
    {
        await OpenAsync();
        await Expect(Box("Due")).ToHaveValueAsync("10/30/2026");

        await Box("Due").ClickAsync();
        await Expect(Pop("Due").Locator(".quro-ui-cal-title")).ToHaveTextAsync("October 2026");
        await Expect(Pop("Due").Locator(".quro-ui-cal-dow").First).ToHaveTextAsync("Sun");
        await Expect(Pop("Due").Locator("[aria-selected=true]")).ToHaveAttributeAsync("data-value", "2026-10-30");
        await Page.WaitForTimeoutAsync(300);
        var before = _sentFrames;

        await Pop("Due").Locator("[data-nav='1']").ClickAsync();
        await Pop("Due").Locator("[data-nav='1']").ClickAsync();
        await Pop("Due").Locator("[data-nav='-1']").ClickAsync();
        await Expect(Pop("Due").Locator(".quro-ui-cal-title")).ToHaveTextAsync("November 2026");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(before, _sentFrames);   // R2: moving between months stays in the browser

        await Pop("Due").Locator("[data-value='2026-11-15']").ClickAsync();

        await Expect(Status("due-value")).ToHaveTextAsync("2026-11-15");
        await Expect(Box("Due")).ToHaveValueAsync("11/15/2026");
        await Expect(Picker("Due")).Not.ToHaveAttributeAsync("data-quro-open", "");
        await Expect(Status("state")).ToHaveTextAsync("Updated");
    }

    [Fact]
    public async Task GS_U13_KoreanCulture_TitleAndWeekdays()
    {
        await OpenAsync("ko-KR");
        await Expect(Box("Due")).ToHaveValueAsync("2026. 10. 30.");

        await Box("Due").ClickAsync();

        await Expect(Pop("Due").Locator(".quro-ui-cal-title")).ToHaveTextAsync("2026년 10월");
        await Expect(Pop("Due").Locator(".quro-ui-cal-dow").First).ToHaveTextAsync("일");   // ko-KR week starts on Sunday
    }

    [Fact]
    public async Task GS_U13_TypedDate_AndSameDayPickShowsFormat()
    {
        await OpenAsync();

        await Box("Due").FillAsync("12252026");
        await Box("Due").PressAsync("Enter");
        await Expect(Status("due-value")).ToHaveTextAsync("2026-12-25");
        await Expect(Box("Due")).ToHaveValueAsync("12/25/2026");

        await Box("Due").FillAsync("not a date");
        await Box("Due").PressAsync("Tab");
        await Expect(Box("Due")).ToHaveValueAsync("12/25/2026");

        await Box("Due").ClickAsync();
        await Pop("Due").Locator("[data-value='2026-12-25']").ClickAsync();   // same day: box goes back to the culture format
        await Expect(Box("Due")).ToHaveValueAsync("12/25/2026");
    }

    [Fact]
    public async Task GS_U13_Range_EndBeforeStartRefused()
    {
        await OpenAsync();

        await Box("From", "to").ClickAsync();
        await Expect(Pop("From").Locator("[aria-selected=true]")).ToHaveAttributeAsync("data-value", "2026-10-20");
        await Pop("From").Locator("[data-value='2026-10-03']").ClickAsync();

        await Expect(Box("From", "to")).ToHaveValueAsync("10/20/2026");
        await Expect(Status("to-value")).ToHaveTextAsync("2026-10-20");

        await Box("From", "to").ClickAsync();
        await Pop("From").Locator("[data-value='2026-10-25']").ClickAsync();
        await Expect(Status("to-value")).ToHaveTextAsync("2026-10-25");
    }

    [Fact]
    public async Task GS_U13_MonthMode_TwelveMonths_EscCloses()
    {
        await OpenAsync();
        await Expect(Box("CloseMonth")).ToHaveValueAsync("10/2026");

        await Box("CloseMonth").ClickAsync();
        await Expect(Pop("CloseMonth").Locator(".quro-ui-cal-month")).ToHaveCountAsync(12);
        await Expect(Pop("CloseMonth").Locator(".quro-ui-cal-title")).ToHaveTextAsync("2026");
        await Box("CloseMonth").PressAsync("Escape");
        await Expect(Picker("CloseMonth")).Not.ToHaveAttributeAsync("data-quro-open", "");

        await Box("CloseMonth").ClickAsync();
        await Pop("CloseMonth").Locator("[data-nav='1']").ClickAsync();
        await Pop("CloseMonth").Locator("[data-value='2027-03']").ClickAsync();

        await Expect(Status("month-value")).ToHaveTextAsync("2027-03");
        await Expect(Box("CloseMonth")).ToHaveValueAsync("3/2027");
    }
}
