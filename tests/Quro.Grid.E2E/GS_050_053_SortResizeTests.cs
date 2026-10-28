using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-050 헤더 클릭 정렬 · GS-053 컬럼 경계 드래그 리사이즈 (Addendum A-22·A-23).
/// 샘플 수량은 행 1·2·3 = 10·5·3, 기본 너비 120px.
/// </summary>
public class GS_050_053_SortResizeTests(SampleServer server) : GridPageTest(server)
{
    private ILocator Header(string field) => Page.Locator($"[role=columnheader][data-field='{field}']");

    private async Task ExpectOrderAsync(params int[] rows)
    {
        var body = Page.Locator(".quro-body [role=row][data-row]");
        await Expect(body).ToHaveCountAsync(rows.Length);
        for (var i = 0; i < rows.Length; i++)
            await Expect(body.Nth(i)).ToHaveAttributeAsync("data-row", rows[i].ToString());
    }

    [Fact]
    public async Task GS_050_HeaderClick_Sorts_OneServerCallEach()
    {
        await OpenAsync();
        var before = InvokeFrames;

        await Header("Qty").ClickAsync();
        await ExpectOrderAsync(3, 2, 1);
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "ascending");
        Assert.Equal(1, InvokeFrames - before);

        await Header("Qty").ClickAsync();
        await ExpectOrderAsync(1, 2, 3);
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "descending");

        await Header("ItemCode").ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });   // 2차 정렬 추가
        await Expect(Header("ItemCode")).ToHaveAttributeAsync("aria-sort", "ascending");
        await Expect(Header("Qty")).ToHaveAttributeAsync("aria-sort", "descending");

        await Header("Qty").ClickAsync();                    // Shift 없이: 그 컬럼만 (내림 → 해제)
        await ExpectOrderAsync(1, 2, 3);
        await Expect(Header("Qty")).Not.ToHaveAttributeAsync("aria-sort", new Regex("."));
        await Expect(Header("ItemCode")).Not.ToHaveAttributeAsync("aria-sort", new Regex("."));
        Assert.Equal(4, InvokeFrames - before);
    }

    [Fact]
    public async Task GS_050_ActiveCell_FollowsRow_ArrowsUseScreenOrder()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();

        await Header("Qty").ClickAsync();                    // 3, 2, 1
        await ExpectOrderAsync(3, 2, 1);
        await ExpectActiveAsync(1, "Qty");

        await Page.Keyboard.PressAsync("ArrowUp");
        await ExpectActiveAsync(2, "Qty");
        await Page.Keyboard.PressAsync("ArrowUp");
        await ExpectActiveAsync(3, "Qty");
    }

    [Fact]
    public async Task GS_050_SortWhileEditing_CommitsFirst()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync("1");                  // 편집 중 (커밋 전)

        await Header("Qty").ClickAsync();

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("1");
        await ExpectOrderAsync(1, 3, 2);                     // 1, 3, 5
        await Expect(Editor).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GS_053_DragBorder_Resizes_OneServerCall()
    {
        await OpenAsync();
        var handle = Header("Qty").Locator(".quro-resize");
        var box = (await handle.BoundingBoxAsync())!;
        var before = InvokeFrames;

        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        for (var dx = 10; dx <= 60; dx += 10)                // 드래그 중에는 서버 호출 없음 (R1)
            await Page.Mouse.MoveAsync(box.X + box.Width / 2 + dx, box.Y + box.Height / 2);
        Assert.Equal(0, InvokeFrames - before);
        Assert.Equal(180, Math.Round((await Header("Qty").BoundingBoxAsync())!.Width));   // 드래그 중에도 보인다
        await Page.Mouse.UpAsync();

        await Expect(Header("Qty")).ToHaveAttributeAsync("style", new Regex("width:180px"));
        await Expect(Cell(2, "Qty")).ToHaveAttributeAsync("style", new Regex("width:180px"));
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, InvokeFrames - before);              // 끝날 때 1회
        Assert.Null(await Header("Qty").GetAttributeAsync("aria-sort"));   // 리사이즈는 정렬이 아니다
    }
}
