using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-064 헤더 키보드 정렬 · GS-065 경계 더블클릭 자동 맞춤 · GS-066 활성 셀·범위 aria-selected (P1 5-1).
/// 샘플 수량은 행 1·2·3 = 10·5·3, 기본 너비 120px.
/// </summary>
public class GS_064_066_HeaderA11yTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private async Task ExpectOrderAsync(params int[] rows)
    {
        var body = Page.Locator(".quro-body [role=row][data-row]");
        await Expect(body).ToHaveCountAsync(rows.Length);
        for (var i = 0; i < rows.Length; i++)
            await Expect(body.Nth(i)).ToHaveAttributeAsync("data-row", rows[i].ToString());
    }

    private Task ExpectHeaderAsync(string field) => Expect(Grid).ToHaveAttributeAsync("data-active-header", field);

    /// <summary>열 너비와, 헤더·셀 중 내용이 잘린 것이 있는지</summary>
    private Task<bool> OverflowsAsync(string field) =>
        Page.EvaluateAsync<bool>("""
            f => [...document.querySelectorAll(`[role=grid] [data-field="${f}"]`)]
                .some(el => el.scrollWidth > el.clientWidth)
            """, field);

    private async Task<int> WidthAsync(string field) =>
        (int)Math.Round((await Header(field).BoundingBoxAsync())!.Width);

    [Fact]
    public async Task GS_064_ArrowUpFromFirstRow_EnterSorts_ShiftEnterAdds()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        var before = InvokeFrames;

        await Page.Keyboard.PressAsync("ArrowUp");           // 첫 행에서 ↑ → 헤더
        await ExpectHeaderAsync("Qty");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await ExpectHeaderAsync("ItemCode");
        await Page.Keyboard.PressAsync("ArrowRight");
        await ExpectHeaderAsync("Qty");
        Assert.Equal(0, InvokeFrames - before);              // 헤더 이동은 서버 호출 없음 (R1)

        await Page.Keyboard.PressAsync("Enter");             // 클릭과 같은 순환: 오름
        await ExpectOrderAsync(3, 2, 1);
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "ascending");
        await ExpectHeaderAsync("Qty");                      // 정렬 뒤에도 헤더에 남는다

        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("Shift+Enter");       // 2차 정렬 추가
        await Expect(Header("ItemCode")).ToHaveAttributeAsync("aria-sort", "ascending");
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "ascending");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(2, InvokeFrames - before);

        await Page.Keyboard.PressAsync("ArrowDown");         // 헤더 → 본문 첫 행 같은 열
        await ExpectActiveAsync(3, "ItemCode");
        await Expect(Grid).Not.ToHaveAttributeAsync("data-active-header", new Regex("."));
    }

    [Fact]
    public async Task GS_064_CtrlArrowUp_FromAnyRow_HeaderIgnoresEditKeys()
    {
        await OpenAsync();
        await Cell(3, "Qty").ClickAsync();

        await Page.Keyboard.PressAsync("Control+ArrowUp");
        await ExpectHeaderAsync("Qty");

        var before = InvokeFrames;
        await Page.Keyboard.TypeAsync("7");                  // 헤더에서는 편집이 열리지 않는다
        await Page.Keyboard.PressAsync("F2");
        await Page.Keyboard.PressAsync("Insert");
        await Expect(Editor).ToHaveCountAsync(0);
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(0, InvokeFrames - before);
        await ExpectOrderAsync(1, 2, 3);

        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(1, "Qty");
    }

    [Fact]
    public async Task GS_064_ClickCell_LeavesHeader()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await ExpectHeaderAsync("Qty");

        await Cell(2, "ItemCode").ClickAsync();
        await ExpectActiveAsync(2, "ItemCode");
        await Expect(Grid).Not.ToHaveAttributeAsync("data-active-header", new Regex("."));
    }

    [Fact]
    public async Task GS_065_DoubleClickBorder_FitsContent_OneServerCall()
    {
        await OpenAsync();
        Assert.Equal(120, await WidthAsync("Qty"));
        var before = InvokeFrames;

        await Header("Qty").Locator(".quro-resize").DblClickAsync();   // 짧은 내용 → 줄어듦

        await Expect(Header("Qty")).Not.ToHaveAttributeAsync("style", new Regex("width:120px"));
        var width = await WidthAsync("Qty");
        Assert.InRange(width, 40, 119);
        Assert.False(await OverflowsAsync("Qty"));
        await Expect(Cell(2, "Qty")).ToHaveAttributeAsync("style", new Regex($"width:{width}px"));
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, InvokeFrames - before);              // 맞춘 너비를 1회 (OnColumnResized)
        Assert.Null(await Header("Qty").GetAttributeAsync("aria-sort"));   // 더블클릭은 정렬이 아니다
    }

    [Fact]
    public async Task GS_065_DoubleClickBorder_WidensForLongValue()
    {
        await OpenAsync();
        await Cell(2, "UnitPrice").ClickAsync();
        await Page.Keyboard.TypeAsync("123456789012345");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(2, "UnitPrice")).ToHaveTextAsync("123,456,789,012,345");
        Assert.True(await OverflowsAsync("UnitPrice"));

        await Header("UnitPrice").Locator(".quro-resize").DblClickAsync();

        await Expect(Header("UnitPrice")).Not.ToHaveAttributeAsync("style", new Regex("width:120px"));
        Assert.True(await WidthAsync("UnitPrice") > 120);
        Assert.False(await OverflowsAsync("UnitPrice"));
    }

    [Fact]
    public async Task GS_066_ActiveCellAndRange_AriaSelected()
    {
        await OpenAsync();
        var selected = Page.Locator(".quro-body [aria-selected=true]");

        await Cell(1, "ItemCode").ClickAsync();
        await Expect(selected).ToHaveCountAsync(1);
        await Expect(Cell(1, "ItemCode")).ToHaveAttributeAsync("aria-selected", "true");

        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Expect(selected).ToHaveCountAsync(4);          // 2행 × 2열
        await Expect(Cell(2, "Qty")).ToHaveAttributeAsync("aria-selected", "true");

        await Page.Keyboard.PressAsync("ArrowDown");         // 범위가 한 셀로 줄면 이전 표시를 지운다
        await Expect(selected).ToHaveCountAsync(1);
        await Expect(Cell(3, "Qty")).ToHaveAttributeAsync("aria-selected", "true");

        await Page.Keyboard.PressAsync("Control+ArrowUp");   // 헤더로 가면 본문 선택 표시 없음
        await Expect(selected).ToHaveCountAsync(0);
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-selected", "true");
    }
}
