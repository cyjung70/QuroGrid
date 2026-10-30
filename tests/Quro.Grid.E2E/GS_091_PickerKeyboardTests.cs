using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-091 선택 목록 키보드 (A-68). 열린 달력·Combo 목록에서 화살표로 강조를 옮기고 Enter로 고른다.
/// 강조 이동은 브라우저에서만(서버 호출 없음, R2), 고를 때 1회. Esc는 편집 취소. 글자 입력은 에디터로.
/// 샘플 쿼리 types=1: DueDate(Date, 1행 2026-11-02), Unit(Combo: Each/Box/Kilogram, 1행 Each).
/// </summary>
public class GS_091_PickerKeyboardTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Picker(int row, string field) => Cell(row, field).Locator(".quro-picker");

    [Fact]
    public async Task GS_091_Calendar_ArrowsMoveDay_EnterPicks()
    {
        await OpenAsync("?types=1");
        await Cell(1, "DueDate").ClickAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        var picker = Picker(1, "DueDate");
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("2");   // 강조는 현재 값에서 시작
        var baseline = await FramesAfterSettleAsync(0);

        await Page.Keyboard.PressAsync("ArrowRight");                          // +1일
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("3");
        await Page.Keyboard.PressAsync("ArrowDown");                           // +7일
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("10");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("ArrowLeft");                           // 10-27: 달을 넘어간다
        await Expect(picker.Locator(".quro-cal-title")).ToHaveTextAsync("October 2026");
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("26");
        await Expect(Editor).ToBeFocusedAsync();
        Assert.Equal(0, await FramesAfterSettleAsync(baseline));

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Cell(1, "DueDate")).ToHaveTextAsync("10/26/2026");
        await Expect(Page.Locator(".quro-picker")).ToHaveCountAsync(0);
        await Expect(Status("json")).ToContainTextAsync("2026-10-26");
        await ExpectActiveAsync(1, "DueDate");                                 // 목록에서 고르면 이동하지 않는다
    }

    [Fact]
    public async Task GS_091_ComboList_ArrowsMoveItem_EnterPicks()
    {
        await OpenAsync("?types=1");
        await Cell(1, "Unit").ClickAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        var picker = Picker(1, "Unit");
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("Each");
        var baseline = await FramesAfterSettleAsync(0);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");                           // 끝에서 멈춘다
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("Kilogram");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(picker.Locator("[data-active]")).ToHaveTextAsync("Box");
        Assert.Equal(0, await FramesAfterSettleAsync(baseline));

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Cell(1, "Unit")).ToHaveTextAsync("Box");
        await Expect(Page.Locator(".quro-picker")).ToHaveCountAsync(0);
        await Expect(Status("json")).ToContainTextAsync("\"BOX\"");
    }

    [Fact]
    public async Task GS_091_Esc_CancelsWithoutChange()
    {
        await OpenAsync("?types=1");
        await Cell(1, "Unit").ClickAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(".quro-picker")).ToHaveCountAsync(0);
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(1, "Unit")).ToHaveTextAsync("Each");
        await Expect(Status("changes")).ToHaveTextAsync("0");
    }

    [Fact]
    public async Task GS_091_Typing_GoesToEditor()
    {
        await OpenAsync("?types=1");
        await Cell(1, "Unit").ClickAsync();
        await Page.Keyboard.TypeAsync("k");                                    // 덮어쓰기 편집 시작
        await Page.Keyboard.PressAsync("Alt+ArrowDown");

        await Page.Keyboard.TypeAsync("g");

        await Expect(Editor).ToHaveValueAsync("kg");
        await Expect(Picker(1, "Unit").Locator("[data-active]")).ToHaveTextAsync("Each");
    }
}
