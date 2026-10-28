using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-067 컬럼 숨김 · GS-068 헤더 드래그 컬럼 이동 (P1 5-2). LineNo는 고정 열(FrozenColumns=1).
/// 기본 열 순서: LineNo · ItemCode · Qty · UnitPrice · Amount
/// </summary>
public class GS_067_068_ColumnMoveTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private async Task<string[]> HeadersAsync() =>
        (await Page.Locator("[role=columnheader]").EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.field)"));

    /// <summary>헤더를 잡아 다른 헤더의 왼쪽(before) 또는 오른쪽 절반 위에 놓는다</summary>
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
    public async Task GS_067_HideColumn_KeyboardSkipsIt()
    {
        await OpenAsync();
        await Status("toggle-price").ClickAsync();
        await Expect(Header("UnitPrice")).ToHaveCountAsync(0);
        await Expect(Cell(1, "UnitPrice")).ToHaveCountAsync(0);

        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await ExpectActiveAsync(1, "Amount");

        await Status("toggle-price").ClickAsync();
        await Expect(Cell(1, "UnitPrice")).ToHaveTextAsync("2,500");
        Assert.Equal(["LineNo", "ItemCode", "Qty", "UnitPrice", "Amount"], await HeadersAsync());
    }

    [Fact]
    public async Task GS_067_HideActiveColumn_MovesActiveCellInRow()
    {
        await OpenAsync();
        await Cell(2, "UnitPrice").ClickAsync();
        await Status("toggle-price").ClickAsync();

        await Expect(Grid).ToHaveAttributeAsync("data-active-row", "2");
        await Expect(Grid).Not.ToHaveAttributeAsync("data-active-field", "UnitPrice");
    }

    [Fact]
    public async Task GS_068_DragHeader_MovesColumn_OneServerCall_NoSort()
    {
        await OpenAsync();
        var before = InvokeFrames;

        await DragHeaderAsync("Qty", "ItemCode", before: true);

        await Expect(Page.Locator("[role=columnheader]").Nth(1)).ToHaveAttributeAsync("data-field", "Qty");
        Assert.Equal(["LineNo", "Qty", "ItemCode", "UnitPrice", "Amount"], await HeadersAsync());
        var cells = await Page.Locator("[role=row][data-row='1'] [role=gridcell]").EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.field)");
        Assert.Equal(["LineNo", "Qty", "ItemCode", "UnitPrice", "Amount"], cells);
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, InvokeFrames - before);              // 놓을 때 1회 (드래그 중에는 없음)
        Assert.Null(await Header("Qty").GetAttributeAsync("aria-sort"));

        await Cell(1, "Qty").ClickAsync();                   // 키보드 이동도 화면 순서
        await Page.Keyboard.PressAsync("ArrowRight");
        await ExpectActiveAsync(1, "ItemCode");
    }

    [Fact]
    public async Task GS_068_DragAfterHalf_And_FrozenBoundary()
    {
        await OpenAsync();

        await DragHeaderAsync("ItemCode", "UnitPrice", before: false);    // 오른쪽 절반 → 뒤로
        await Expect(Page.Locator("[role=columnheader]").Nth(3)).ToHaveAttributeAsync("data-field", "ItemCode");
        Assert.Equal(["LineNo", "Qty", "UnitPrice", "ItemCode", "Amount"], await HeadersAsync());

        var before = InvokeFrames;
        await DragHeaderAsync("Amount", "LineNo", before: true);          // 고정 열 앞으로는 못 감 → 일반 영역 맨 앞
        await Expect(Page.Locator("[role=columnheader]").Nth(1)).ToHaveAttributeAsync("data-field", "Amount");
        Assert.Equal("LineNo", (await HeadersAsync())[0]);
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, InvokeFrames - before);
    }

    [Fact]
    public async Task GS_068_SmallMove_IsStillAClickSort()
    {
        await OpenAsync();
        var box = (await Header("Qty").BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + 20, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + 22, box.Y + box.Height / 2);   // 문턱(5px) 안
        await Page.Mouse.UpAsync();

        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "ascending");
        Assert.Equal(["LineNo", "ItemCode", "Qty", "UnitPrice", "Amount"], await HeadersAsync());
        await Expect(Header("Qty")).Not.ToHaveAttributeAsync("class", new Regex("quro-dragging"));
    }
}
