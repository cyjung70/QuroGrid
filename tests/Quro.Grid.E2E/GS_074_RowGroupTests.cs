using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-074 그룹핑 키보드·마우스 (P1 5-5, Addendum A-32). 샘플 ?types=1&amp;groupby=Unit:
/// 단위 Box(행 1·3) · Each(행 2). ↑↓는 그룹 머리 행에도 서고(서버 호출 없음), 소계 행은 건너뛴다.
/// Space·Enter·클릭은 펼침·접기(서버 1회), ←는 접기, →는 펼치기.
/// </summary>
public class GS_074_RowGroupTests(SampleServer server) : GridPageTest(server)
{
    private ILocator GroupHead(string text) =>
        Page.Locator(".quro-body .quro-group-head-row", new() { HasTextString = text });

    private Task OpenGroupedAsync() => OpenAsync("?types=1&groupby=Unit");

    [Fact]
    public async Task GS_074_ArrowKeys_VisitGroupHeads_SkipSubtotals_NoServerCalls()
    {
        await OpenGroupedAsync();
        await Expect(GroupHead("Box")).ToBeVisibleAsync();
        var boxId = await GroupHead("Box").GetAttributeAsync("data-group");
        var eachId = await GroupHead("Each").GetAttributeAsync("data-group");

        await Cell(1, "Qty").ClickAsync();
        var before = InvokeFrames;

        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Grid).ToHaveAttributeAsync("data-active-group", boxId!);
        await Page.Keyboard.PressAsync("ArrowUp");                     // 첫 그룹 위는 컬럼 헤더 (GS-064)
        await Expect(Grid).ToHaveAttributeAsync("data-active-header", "Qty");
        await Expect(Grid).Not.ToHaveAttributeAsync("data-active-group", new System.Text.RegularExpressions.Regex("."));

        await Page.Keyboard.PressAsync("ArrowDown");                   // 헤더 ↓ = 본문 첫 행 (그룹 머리 행)
        await Expect(Grid).ToHaveAttributeAsync("data-active-group", boxId!);
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(1, "Qty");
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(3, "Qty");
        await Page.Keyboard.PressAsync("ArrowDown");                   // 소계 행을 건너뛰고 다음 그룹 머리 행
        await Expect(Grid).ToHaveAttributeAsync("data-active-group", eachId!);
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(2, "Qty");                              // 들어갈 때의 열을 기억한다

        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(0, InvokeFrames - before);
    }

    [Fact]
    public async Task GS_074_SpaceArrowsClick_ToggleGroup_OneCallEach()
    {
        await OpenGroupedAsync();
        await Cell(2, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Grid).ToHaveAttributeAsync("data-active-group", (await GroupHead("Each").GetAttributeAsync("data-group"))!);
        var before = InvokeFrames;

        await Page.Keyboard.PressAsync("Space");
        await Expect(GroupHead("Each")).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Cell(2, "Qty")).ToHaveCountAsync(0);
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(1, InvokeFrames - before);

        await Page.Keyboard.PressAsync("ArrowLeft");                   // 이미 접힘: 서버 호출 없음
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Cell(2, "Qty")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(2, InvokeFrames - before);

        await GroupHead("Box").ClickAsync();                           // 클릭: 그 그룹으로 옮기고 펼침·접기
        await Expect(GroupHead("Box")).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Cell(1, "Qty")).ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(1, "Qty")).ToBeVisibleAsync();
    }
}
