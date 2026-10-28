namespace Quro.Grid.E2E;

/// <summary>GS-043 · GS-044: 범위 선택·복사 (Spec 4.7). 선택 상태는 JS 소유 (R1).</summary>
public class GS_043_044_RangeCopyTests(SampleServer server) : GridPageTest(server)
{
    /// <summary>선택 범위: 그리드 루트의 data-selection = "위 행,왼쪽 필드,아래 행,오른쪽 필드"</summary>
    private Task ExpectSelectionAsync(string value) =>
        Expect(Grid).ToHaveAttributeAsync("data-selection", value);

    /// <summary>GS-043: Shift+화살표로 범위 선택, 서버 호출 없음</summary>
    [Fact]
    public async Task GS_043_ShiftArrows_SelectsRange()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await ExpectSelectionAsync("1,ItemCode,1,ItemCode");
        var baseline = SentFrames;

        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await ExpectSelectionAsync("1,ItemCode,2,UnitPrice");
        await ExpectActiveAsync(2, "UnitPrice");
        Assert.Equal(0, await FramesAfterSettleAsync(baseline));
    }

    /// <summary>GS-043: 위·왼쪽으로 넓혀도 정규화된 사각형</summary>
    [Fact]
    public async Task GS_043_ShiftArrows_UpLeft_Normalized()
    {
        await OpenAsync();
        await Cell(3, "UnitPrice").ClickAsync();

        await Page.Keyboard.PressAsync("Shift+ArrowUp");
        await Page.Keyboard.PressAsync("Shift+ArrowLeft");

        await ExpectSelectionAsync("2,Qty,3,UnitPrice");
    }

    /// <summary>GS-043: 마우스 드래그로 범위 선택</summary>
    [Fact]
    public async Task GS_043_MouseDrag_SelectsRange()
    {
        await OpenAsync();
        var from = await Cell(1, "Qty").BoundingBoxAsync();
        var to = await Cell(3, "UnitPrice").BoundingBoxAsync();

        await Page.Mouse.MoveAsync(from!.X + 5, from.Y + 5);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to!.X + 5, to.Y + 5, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        await ExpectSelectionAsync("1,Qty,3,UnitPrice");
    }

    /// <summary>GS-043: Shift 없이 이동하면 선택이 한 셀로 줄어든다</summary>
    [Fact]
    public async Task GS_043_PlainArrow_CollapsesSelection()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await ExpectSelectionAsync("1,ItemCode,2,ItemCode");

        await Page.Keyboard.PressAsync("ArrowRight");

        await ExpectSelectionAsync("2,Qty,2,Qty");
    }

    /// <summary>GS-044: 범위 선택 후 Ctrl+C → 클립보드에 탭·줄바꿈 형식 (표시 값)</summary>
    [Fact]
    public async Task GS_044_CtrlC_CopiesTabSeparated()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Page.Keyboard.PressAsync("Control+c");

        var text = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Equal("A1001\t10\r\nA1002\t5", text);
    }
}
