using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-070 다단 헤더 (P1 5-3). 샘플 ?groups=1: Qty·UnitPrice를 "주문(Order)" 그룹으로 묶는다.
/// 열 순서: LineNo(고정) · ItemCode · [Order: Qty · UnitPrice] · Amount
/// </summary>
public class GS_070_GroupHeaderTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private ILocator GroupHeader => Page.Locator(".quro-group-header");

    private async Task<string[]> LeavesAsync() =>
        await Page.Locator("[role=columnheader][data-field]").EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.field)");

    private async Task DragHeaderAsync(string field, string target, bool before)
    {
        var from = (await Header(field).BoundingBoxAsync())!;
        var to = (await Header(target).BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(from.X + 20, from.Y + from.Height / 2);
        await Page.Mouse.DownAsync();
        var x = before ? to.X + 10 : to.X + to.Width - 10;
        for (var i = 1; i <= 5; i++)
            await Page.Mouse.MoveAsync(from.X + 20 + (x - from.X - 20) * i / 5, to.Y + to.Height / 2);
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task GS_070_GroupHeader_SpansColumns_KeyboardAndRangeStillWork()
    {
        await OpenAsync("?groups=1");
        await Expect(GroupHeader).ToHaveTextAsync("Order");
        var group = (await GroupHeader.BoundingBoxAsync())!;
        var qty = (await Header("Qty").BoundingBoxAsync())!;
        var price = (await Header("UnitPrice").BoundingBoxAsync())!;
        Assert.Equal(Math.Round(qty.X), Math.Round(group.X));
        Assert.Equal(Math.Round(price.X + price.Width), Math.Round(group.X + group.Width));
        Assert.True(group.Y < qty.Y);                                         // 그룹 행이 위

        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");                    // 범위 선택은 행 번호(aria-rowindex)로
        await Expect(Page.Locator(".quro-body [aria-selected=true]")).ToHaveCountAsync(2);

        await Page.Keyboard.PressAsync("ArrowUp");                            // 범위 해제 후 1행
        await ExpectActiveAsync(1, "Qty");
        await Page.Keyboard.PressAsync("ArrowUp");                            // 첫 행에서 ↑ → 헤더
        await Expect(Grid).ToHaveAttributeAsync("data-active-header", "Qty");
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(1, "Qty");
    }

    [Fact]
    public async Task GS_070_GroupHeaderClick_DoesNothing()
    {
        await OpenAsync("?groups=1");
        var before = InvokeFrames;

        await GroupHeader.ClickAsync();

        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(0, InvokeFrames - before);
        Assert.Null(await Header("Qty").GetAttributeAsync("aria-sort"));
    }

    [Fact]
    public async Task GS_070_DragInsideGroup_Moves_DragIntoGroup_DoesNot()
    {
        await OpenAsync("?groups=1");

        await DragHeaderAsync("UnitPrice", "Qty", before: true);
        await Expect(Page.Locator("[role=columnheader][data-field]").Nth(2)).ToHaveAttributeAsync("data-field", "UnitPrice");
        Assert.Equal(["LineNo", "ItemCode", "UnitPrice", "Qty", "Amount", "AmountWithTax"], await LeavesAsync());

        await DragHeaderAsync("ItemCode", "Qty", before: true);               // 그룹을 가르는 자리: 옮기지 않음
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(["LineNo", "ItemCode", "UnitPrice", "Qty", "Amount", "AmountWithTax"], await LeavesAsync());
        await Expect(GroupHeader).ToHaveCountAsync(1);
    }
}
