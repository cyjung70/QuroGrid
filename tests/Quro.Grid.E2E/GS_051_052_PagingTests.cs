using System.Diagnostics;
using System.Globalization;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-051 · GS-052: 서버측 페이징 (R6, O-04, Addendum A-24). 샘플 ?large=1 = 10,000행, PageSize 100, 높이 480px.
/// 행 키는 조회 순서대로 적재되므로 첫 페이지의 행 1 = LineNo 1 (수량 = LineNo % 50 + 1).
/// </summary>
public class GS_051_052_PagingTests(SampleServer server) : GridPageTest(server)
{
    private static readonly CultureInfo En = new("en-US");

    /// <summary>샘플 상태를 다시 그리게 한 뒤 읽는다 (그리드 안의 조회는 페이지를 다시 그리지 않는다).</summary>
    private async Task<int> StatusNumberAsync(string id)
    {
        var refreshes = int.Parse(await Status("loaded-rows").GetAttributeAsync("data-refresh") ?? "0");
        await Status("refresh-status").ClickAsync();
        await Expect(Status("loaded-rows")).ToHaveAttributeAsync("data-refresh", (refreshes + 1).ToString());
        return int.Parse(await Status(id).TextContentAsync() ?? "", NumberStyles.Any, En);
    }

    private ILocator RowWithLineNo(int lineNo) =>
        Page.Locator(".quro-body [role=row]", new() { Has = Page.Locator($"[data-field=LineNo]:text-is('{lineNo}')") });

    private Task ScrollAsync(string where) =>
        Grid.EvaluateAsync(where == "end" ? "g => g.scrollTop = g.scrollHeight" : "g => g.scrollTop = 0");

    private async Task<decimal> FooterQtyAsync() =>
        decimal.Parse(await Page.Locator("[data-footer-field=Qty]").TextContentAsync() ?? "", NumberStyles.Any, En);

    [Fact]
    public async Task GS_051_10000Rows_FirstScreenUnder1s_LoadsWindowOnly()
    {
        var watch = Stopwatch.StartNew();
        await OpenAsync("?large=1");
        await Expect(Cell(1, "LineNo")).ToHaveTextAsync("1");
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 1000, $"첫 화면 {watch.ElapsedMilliseconds}ms");
        Assert.InRange(await Page.Locator(".quro-body [role=row][data-row]").CountAsync(), 1, 99);   // 보이는 범위만 그린다
        Assert.InRange(await StatusNumberAsync("loaded-rows"), 1, 300);                                 // DataSet 적재 범위
        Assert.InRange(await StatusNumberAsync("provider-calls"), 1, 3);
    }

    [Fact]
    public async Task GS_051_ScrollToEnd_ShowsLastRow_ReleasesFirstPages()
    {
        await OpenAsync("?large=1");
        await Expect(Cell(1, "LineNo")).ToHaveTextAsync("1");

        await ScrollAsync("end");

        await Expect(Page.Locator(".quro-body [data-field=LineNo]", new() { HasTextString = "10,000" })).ToBeVisibleAsync();
        Assert.InRange(await StatusNumberAsync("loaded-rows"), 1, 300);
        await Expect(Cell(1, "LineNo")).ToHaveCountAsync(0);   // 첫 페이지는 놓았다
    }

    [Fact]
    public async Task GS_052_EditScrollAwayAndBack_ChangeKept_TotalAdjusted()
    {
        await OpenAsync("?large=1");
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("2");
        var total = await FooterQtyAsync();

        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync("999");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("999");

        await ScrollAsync("end");
        await Expect(Cell(1, "Qty")).ToHaveCountAsync(0);
        await ScrollAsync("top");

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("999");
        await Expect(Status("changes")).ToHaveTextAsync("1");
        Assert.Equal(total + 997, await FooterQtyAsync());           // 서버 합계 + (999 - 2)
        Assert.InRange(await StatusNumberAsync("loaded-rows"), 1, 301);
    }

    /// <summary>놓았다가 다시 적재한 행도 같은 행 키라 활성 셀·키보드 이동이 이어진다</summary>
    [Fact]
    public async Task GS_052_ScrollAwayAndBack_ActiveCellKept()
    {
        await OpenAsync("?large=1");
        await Cell(1, "Qty").ClickAsync();

        await ScrollAsync("end");
        await Expect(Cell(1, "Qty")).ToHaveCountAsync(0);
        await ScrollAsync("top");
        await Expect(Cell(1, "Qty")).ToBeVisibleAsync();

        await ExpectActiveAsync(1, "Qty");
        await Grid.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(2, "Qty");
    }

    /// <summary>GS-047 + 페이징: 행 1 선택 → 스크롤 → Shift+클릭 행 600 → Ctrl+Delete = 600행 삭제 (A-25)</summary>
    [Fact]
    public async Task GS_047_Paging_ShiftClickBeyondScreen_CtrlDelete()
    {
        await OpenAsync("?large=1");
        await Cell(1, "Qty").ClickAsync();

        await Grid.EvaluateAsync("g => g.scrollTop = 590 * 28");
        var row600 = RowWithLineNo(600);
        await row600.Locator("[data-field=Qty]").ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Shift] });
        await Page.Keyboard.PressAsync("Control+Delete");

        await Expect(Status("changes")).ToHaveTextAsync("600");
        await Expect(row600).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("quro-deleted"));
        await ScrollAsync("top");
        await Expect(Page.Locator("[role=row][data-row='1']")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("quro-deleted"));
    }

    /// <summary>툴바 일괄 삭제도 같은 위치 범위 (A-25)</summary>
    [Fact]
    public async Task GS_047_Paging_ToolbarDelete_UsesRange()
    {
        await OpenAsync("?large=1");
        await Cell(1, "Qty").ClickAsync();
        await Grid.EvaluateAsync("g => g.scrollTop = 290 * 28");
        var row300 = RowWithLineNo(300);
        await row300.Locator("[data-field=Qty]").ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Shift] });

        await Status("delete-rows").ClickAsync();

        await Expect(Status("changes")).ToHaveTextAsync("300");
    }

    [Fact]
    public async Task GS_052_Paste300Rows_LoadsNeededRange_OneCall()
    {
        await OpenAsync("?large=1");
        await Cell(1, "Qty").ClickAsync();

        await PasteAsync(string.Join("\r\n", Enumerable.Repeat("777", 300)));

        await Expect(Status("pastes")).ToHaveTextAsync("1");
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("777");
        await Expect(Status("changes")).ToHaveTextAsync("300");      // 적재되지 않았던 행까지 채운다
        Assert.InRange(await StatusNumberAsync("loaded-rows"), 300, 600);
    }
}
