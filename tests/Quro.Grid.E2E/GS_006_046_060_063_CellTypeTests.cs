using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-006 · GS-046 (코드도움), GS-060 ~ GS-063 (Date·Combo·Check·Button 셀) — Addendum A-16 ~ A-19.
/// 샘플 쿼리 types=1: ItemCode는 Popup(품목 A1001~A1003, B2001·B2002, C3001), Unit(Combo: Each/Box/Kilogram),
/// Urgent(Check: Y/N), DueDate(Date, 1행 2026-11-02), Detail(Button). 코드도움 팝업은 앱(샘플 다이얼로그)이 띄운다.
/// </summary>
public class GS_006_046_060_063_CellTypeTests(SampleServer server) : GridPageTest(server)
{
    private ILocator HelpItems => Page.Locator("[data-testid=help-item]");

    // ── GS-006 ──

    [Fact]
    public async Task GS_006_PopupTab_SingleMatch_AutoCommits()
    {
        await OpenAsync("?types=1");
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.TypeAsync("c3");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Cell(1, "ItemCode")).ToHaveTextAsync("C3001");
        await ExpectActiveAsync(1, "Qty");
        await Expect(HelpItems).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GS_006_PopupTab_NoMatch_NoPopup_KeepsEditing()
    {
        await OpenAsync("?types=1");
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.TypeAsync("zz");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Editor).ToHaveValueAsync("zz");
        await Expect(Editor).ToBeFocusedAsync();
        await ExpectActiveAsync(1, "ItemCode");
        await Expect(HelpItems).ToHaveCountAsync(0);
        await Expect(Status("changes")).ToHaveTextAsync("0");
    }

    [Fact]
    public async Task GS_006_PopupTab_MultipleMatches_PickFromPopup()
    {
        await OpenAsync("?types=1");
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.TypeAsync("b2");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(HelpItems).ToHaveTextAsync(new[] { "B2001", "B2002" });
        await HelpItems.Nth(1).ClickAsync();

        await Expect(Cell(1, "ItemCode")).ToHaveTextAsync("B2002");
        await Expect(HelpItems).ToHaveCountAsync(0);
        await ExpectActiveAsync(1, "Qty");
    }

    [Fact]
    public async Task GS_006_PopupTab_MultipleMatches_CancelKeepsEditing()
    {
        await OpenAsync("?types=1");
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.TypeAsync("b2");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(HelpItems).ToHaveCountAsync(2);

        await Status("help-cancel").ClickAsync();

        await Expect(HelpItems).ToHaveCountAsync(0);
        await Expect(Editor).ToHaveValueAsync("b2");
        await Expect(Editor).ToBeFocusedAsync();
        await Expect(Status("changes")).ToHaveTextAsync("0");
    }

    // ── GS-046 ──

    [Fact]
    public async Task GS_046_F4_OpensCodeHelpWithText()
    {
        await OpenAsync("?types=1");
        await Cell(2, "ItemCode").ClickAsync();
        await Page.Keyboard.TypeAsync("a1");

        await Page.Keyboard.PressAsync("F4");

        await Expect(Status("popup-text")).ToHaveTextAsync("a1");
        await Expect(HelpItems).ToHaveTextAsync(new[] { "A1001", "A1002", "A1003" });
        await HelpItems.Nth(2).ClickAsync();

        await Expect(Cell(2, "ItemCode")).ToHaveTextAsync("A1003");
        await Expect(Editor).ToHaveCountAsync(0);
        await ExpectActiveAsync(2, "ItemCode");                // F4 확정은 이동하지 않는다
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);   // 팝업이 다 닫힌 뒤 키를 누른다 (닫히는 중의 키는 팝업이 받음)
        await Page.Keyboard.PressAsync("ArrowRight");          // 키보드는 그리드로 돌아온다
        await ExpectActiveAsync(2, "Qty");
    }

    // ── GS-060 ──

    [Fact]
    public async Task GS_060_DatePicker_AltDown_PickCommits()
    {
        await OpenAsync("?types=1");
        await Cell(1, "DueDate").ClickAsync();
        await Expect(Cell(1, "DueDate")).ToHaveTextAsync("11/2/2026");

        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        var picker = Cell(1, "DueDate").Locator(".quro-picker");
        await Expect(picker).ToBeVisibleAsync();
        await picker.GetByRole(AriaRole.Button, new() { Name = "20", Exact = true }).ClickAsync();

        await Expect(Cell(1, "DueDate")).ToHaveTextAsync("11/20/2026");
        await Expect(Page.Locator(".quro-picker")).ToHaveCountAsync(0);
        await Expect(Status("json")).ToContainTextAsync("2026-11-20");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await ExpectActiveAsync(1, "Urgent");
    }

    /// <summary>GS-060 (A-67): 달력은 브라우저가 그린다. 달을 넘겨도 서버 호출이 없고, 날짜를 고를 때만 1회</summary>
    [Fact]
    public async Task GS_060_DatePicker_MonthNav_NoServerCall()
    {
        await OpenAsync("?types=1");
        await Cell(1, "DueDate").ClickAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        var picker = Cell(1, "DueDate").Locator(".quro-picker");
        await Expect(picker.Locator(".quro-cal-title")).ToHaveTextAsync("November 2026");
        await Expect(picker.Locator("[aria-selected=true]")).ToHaveTextAsync("2");
        var baseline = await FramesAfterSettleAsync(0);

        await picker.Locator("[data-nav='1']").ClickAsync();
        await Expect(picker.Locator(".quro-cal-title")).ToHaveTextAsync("December 2026");
        await picker.Locator("[data-nav='1']").ClickAsync();
        await Expect(picker.Locator(".quro-cal-title")).ToHaveTextAsync("January 2027");
        await Expect(Editor).ToBeFocusedAsync();                       // 달력을 눌러도 편집은 그대로
        Assert.Equal(0, await FramesAfterSettleAsync(baseline));

        await picker.Locator(".quro-cal-day:not([data-outside])", new() { HasText = "15" }).ClickAsync();
        await Expect(Cell(1, "DueDate")).ToHaveTextAsync("1/15/2027");
        await Expect(Status("json")).ToContainTextAsync("2027-01-15");
    }

    // ── GS-061 ──

    [Fact]
    public async Task GS_061_Combo_TypeOrPick()
    {
        await OpenAsync("?types=1");
        await Expect(Cell(1, "Unit")).ToHaveTextAsync("Each");

        await Cell(1, "Unit").ClickAsync();
        await Page.Keyboard.TypeAsync("box");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(1, "Unit")).ToHaveTextAsync("Box");

        await Cell(2, "Unit").ClickAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        await Cell(2, "Unit").Locator(".quro-picker [role=option]", new() { HasText = "Kilogram" }).ClickAsync();

        await Expect(Cell(2, "Unit")).ToHaveTextAsync("Kilogram");
        await Expect(Status("json")).ToContainTextAsync("\"KG\"");
    }

    // ── GS-062 ──

    [Fact]
    public async Task GS_062_Check_ClickAndSpaceToggle()
    {
        await OpenAsync("?types=1");
        var box = Cell(1, "Urgent").Locator("[role=checkbox]");
        await Expect(box).ToHaveAttributeAsync("aria-checked", "false");
        var baseline = InvokeFrames;

        await box.ClickAsync();
        await Expect(box).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Status("changes")).ToHaveTextAsync("1");
        await FramesAfterSettleAsync(0);
        Assert.Equal(1, InvokeFrames - baseline);              // 토글 1회 = 서버 호출 1회

        await Page.Keyboard.PressAsync(" ");
        await Expect(box).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(Status("changes")).ToHaveTextAsync("0");
        await Expect(Editor).ToHaveCountAsync(0);
    }

    // ── GS-063 ──

    [Fact]
    public async Task GS_063_Button_ClickAndEnter()
    {
        await OpenAsync("?types=1");

        await Cell(2, "Detail").Locator("[role=button]").ClickAsync();
        await Expect(Status("button-row")).ToHaveTextAsync("2");
        await ExpectActiveAsync(2, "Detail");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Status("button-row")).ToHaveTextAsync("3");
        await ExpectActiveAsync(3, "Detail");                  // Button 셀에서 Enter는 이동하지 않는다
        await Expect(Editor).ToHaveCountAsync(0);
    }
}
