using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-071~GS-073 헤더 필터 패널 (P1 5-4, Addendum A-31). 열기 1회 · 적용 1회, 패널 안 입력은 서버 호출 없음 (R2).
/// 샘플 품목코드 A1001·A1002·A1003, 수량 10·5·3. 대량 조회(?large=1)는 수량 = LineNo % 50 + 1.
/// </summary>
public class GS_071_073_FilterPanelTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private ILocator Panel => Page.Locator(".quro-filter-panel");

    private ILocator BodyRows => Page.Locator(".quro-body [role=row][data-row]");

    [Fact]
    public async Task GS_071_FilterButton_OpensPanel_TypingLocal_EnterApplies()
    {
        await OpenAsync();
        var before = InvokeFrames;

        await Header("ItemCode").Locator(".quro-filter-button").ClickAsync();
        await Expect(Panel).ToBeVisibleAsync();
        await Expect(Panel.Locator("[data-filter=text]")).ToBeFocusedAsync();
        Assert.Null(await Header("ItemCode").GetAttributeAsync("aria-sort"));   // 필터 버튼은 정렬이 아니다
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(1, InvokeFrames - before);

        await Page.Keyboard.TypeAsync("1002");
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(1, InvokeFrames - before);              // 입력 중에는 서버 호출 없음

        await Page.Keyboard.PressAsync("Enter");
        await Expect(BodyRows).ToHaveCountAsync(1);
        await Expect(Cell(2, "ItemCode")).ToHaveTextAsync("A1002");
        await Expect(Panel).ToHaveCountAsync(0);
        await Expect(Header("ItemCode")).ToHaveAttributeAsync("data-filtered", "true");
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(2, InvokeFrames - before);

        await Header("ItemCode").Locator(".quro-filter-button").ClickAsync();
        await Expect(Panel.Locator("[data-filter=text]")).ToHaveValueAsync("1002");   // 현재 조건
        await Panel.Locator("[data-filter-action=clear]").ClickAsync();
        await Expect(BodyRows).ToHaveCountAsync(3);
        await Expect(Header("ItemCode")).Not.ToHaveAttributeAsync("data-filtered", new Regex("."));
    }

    [Fact]
    public async Task GS_071_HeaderKeyboard_AltDown_OpensPanel_ApplyButton()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Grid).ToHaveAttributeAsync("data-active-header", "Qty");

        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(Panel.Locator("[data-filter=min]")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("4");
        await Panel.Locator("[data-filter-action=apply]").ClickAsync();

        await Expect(BodyRows).ToHaveCountAsync(2);
        await Expect(Page.Locator("[data-footer-field=Qty]")).ToHaveTextAsync("15");   // 보이는 행 합계
    }

    [Fact]
    public async Task GS_071_Escape_ClosesWithoutChange_FocusBackToGrid()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Header("ItemCode").Locator(".quro-filter-button").ClickAsync();
        await Expect(Panel).ToBeVisibleAsync();
        await Page.Keyboard.TypeAsync("zzz");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Panel).ToHaveCountAsync(0);
        await Expect(BodyRows).ToHaveCountAsync(3);
        await Page.Keyboard.PressAsync("ArrowDown");         // 키보드는 그리드로
        await ExpectActiveAsync(2, "Qty");
    }

    /// <summary>GS-073: 페이징 중에는 조건을 조회 요청에 담아 서버가 거른다. 건수·합계는 서버 결과 (수량 50인 행 200건)</summary>
    [Fact]
    public async Task GS_073_Paging_FilterGoesToServer_CountAndTotals()
    {
        await OpenAsync("?large=1");
        await Expect(Cell(1, "LineNo")).ToHaveTextAsync("1");

        await Header("Qty").Locator(".quro-filter-button").ClickAsync();
        await Page.Keyboard.TypeAsync("50");                 // 패널이 열리는 중에 친 키도 패널로
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Grid).ToHaveAttributeAsync("aria-rowcount", "202");                  // 헤더 + 200 + 합계
        await Expect(Page.Locator("[data-footer-field=Qty]")).ToHaveTextAsync("10,000");   // 50 × 200
        await Expect(Page.Locator(".quro-body [role=row][data-row] [data-field=LineNo]").First).ToHaveTextAsync("49");
    }
}
