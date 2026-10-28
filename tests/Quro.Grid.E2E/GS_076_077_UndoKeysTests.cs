using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-076·GS-077 Ctrl+Z / Ctrl+Y (P1 5-6, Addendum A-33). 한 번 누르면 서버 1회.
/// 편집 중에는 입력란의 브라우저 기본 Undo (서버 호출 없음).
/// 샘플: 행 1 수량 10 · 단가 2,500 · 금액 25,000 (금액 = 수량 × 단가, OnCellValueChanged)
/// </summary>
public class GS_076_077_UndoKeysTests(SampleServer server) : GridPageTest(server)
{
    private ILocator BodyRows => Page.Locator(".quro-body [role=row][data-row]");

    [Fact]
    public async Task GS_076_CtrlZ_UndoesCommitAndRecalc_CtrlY_Redoes()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync("7");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("17,500");
        await Page.WaitForTimeoutAsync(300);
        var before = InvokeFrames;

        await Page.Keyboard.PressAsync("Control+z");

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("10");
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("25,000");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, InvokeFrames - before);

        await Page.Keyboard.PressAsync("Control+y");
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("7");
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("17,500");

        await Page.Keyboard.PressAsync("Control+z");
        await Page.Keyboard.PressAsync("Control+Shift+z");   // Ctrl+Shift+Z도 다시 하기
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("7");
    }

    [Fact]
    public async Task GS_077_PasteInsertDelete_EachUndoneInOneStep()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await PasteAsync("1\t2\r\n3\t4");
        await Expect(Cell(2, "UnitPrice")).ToHaveTextAsync("4");

        await Page.Keyboard.PressAsync("Insert");
        await Expect(BodyRows).ToHaveCountAsync(4);

        await Page.Keyboard.PressAsync("Control+z");          // 행 추가 취소
        await Expect(BodyRows).ToHaveCountAsync(3);

        await Page.Keyboard.PressAsync("Control+z");          // 붙여넣기 취소 (2행 × 2열 한 번에)
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("10");
        await Expect(Cell(2, "UnitPrice")).ToHaveTextAsync("1,200");

        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        await Page.Keyboard.PressAsync("Control+Delete");
        await Expect(BodyRows).ToHaveCountAsync(1);

        await Page.Keyboard.PressAsync("Control+z");          // 일괄 삭제 취소
        await Expect(BodyRows).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task GS_076_CtrlZ_WhileEditing_IsEditorUndo_NoServerCall()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        await Expect(Editor).ToHaveCountAsync(1);
        await Page.Keyboard.TypeAsync("Z");
        var before = InvokeFrames;

        await Page.Keyboard.PressAsync("Control+z");

        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(0, InvokeFrames - before);
        await Expect(Editor).ToHaveCountAsync(1);
        await Expect(Editor).ToHaveValueAsync("A1001");      // 입력란 기본 Undo
    }
}
