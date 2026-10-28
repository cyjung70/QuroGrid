namespace Quro.Grid.E2E;

/// <summary>GS-037 ~ GS-042: 셀 이동·편집 키 (Spec 4.7, R1·R2)</summary>
public class GS_037_042_KeyboardTests(SampleServer server) : GridPageTest(server)
{
    /// <summary>GS-037: 화살표 이동은 서버 왕복 없이 (SignalR 전송 0건)</summary>
    [Fact]
    public async Task GS_037_ArrowKeys_MoveWithoutServer()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await ExpectActiveAsync(1, "ItemCode");
        var baseline = SentFrames;

        await Page.Keyboard.PressAsync("ArrowRight");
        await ExpectActiveAsync(1, "Qty");
        await Page.Keyboard.PressAsync("ArrowDown");
        await ExpectActiveAsync(2, "Qty");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await ExpectActiveAsync(2, "ItemCode");
        await Page.Keyboard.PressAsync("ArrowUp");
        await ExpectActiveAsync(1, "ItemCode");

        Assert.Equal(0, await FramesAfterSettleAsync(baseline));
    }

    /// <summary>GS-038: 편집 중 키 입력은 서버로 보내지 않고, 셀 이탈 때 커밋</summary>
    [Fact]
    public async Task GS_038_Typing_NoServerUntilCommit()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        await Expect(Editor).ToBeFocusedAsync();
        var baseline = SentFrames;

        await Page.Keyboard.TypeAsync("123");
        Assert.Equal(0, await FramesAfterSettleAsync(baseline));

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("10,123");
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("25,307,500");   // 금액 재계산 (OnCellValueChanged)
        Assert.True(SentFrames > baseline);
    }

    /// <summary>GS-039: Tab / Shift+Tab은 편집 가능 셀만, 행 끝에서 다음 행 첫 셀</summary>
    [Fact]
    public async Task GS_039_Tab_SkipsReadOnly_WrapsRows()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.PressAsync("Tab");
        await ExpectActiveAsync(1, "Qty");
        await Page.Keyboard.PressAsync("Tab");
        await ExpectActiveAsync(1, "UnitPrice");
        await Page.Keyboard.PressAsync("Tab");               // Amount(읽기 전용)·LineNo(읽기 전용) 건너뜀
        await ExpectActiveAsync(2, "ItemCode");
        await Page.Keyboard.PressAsync("Shift+Tab");
        await ExpectActiveAsync(1, "UnitPrice");
    }

    /// <summary>GS-039: 편집 중 Tab은 커밋 후 이동</summary>
    [Fact]
    public async Task GS_039_TabWhileEditing_CommitsAndMoves()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.TypeAsync("Z9");

        await Page.Keyboard.PressAsync("Tab");

        await ExpectActiveAsync(1, "Qty");
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(1, "ItemCode")).ToHaveTextAsync("Z9");
    }

    /// <summary>GS-040: F2는 기존 값 유지, 커서는 끝</summary>
    [Fact]
    public async Task GS_040_F2_KeepsValue_CaretAtEnd()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.PressAsync("F2");

        await Expect(Editor).ToBeFocusedAsync();
        await Expect(Editor).ToHaveValueAsync("A1001");
        Assert.Equal(5, await Editor.EvaluateAsync<int>("e => e.selectionStart"));
        Assert.Equal(5, await Editor.EvaluateAsync<int>("e => e.selectionEnd"));
    }

    /// <summary>GS-041: 문자 입력으로 편집 시작 → 기존 값 덮어쓰기 (빠른 입력도 유실 없음)</summary>
    [Fact]
    public async Task GS_041_TypeChar_Overwrites()
    {
        await OpenAsync();
        await Cell(2, "ItemCode").ClickAsync();

        await Page.Keyboard.TypeAsync("B77");
        await Expect(Editor).ToHaveValueAsync("B77");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Cell(2, "ItemCode")).ToHaveTextAsync("B77");
    }

    /// <summary>GS-041: 읽기 전용 셀에서는 문자 입력으로 편집이 시작되지 않음</summary>
    [Fact]
    public async Task GS_041_TypeOnReadOnly_NoEditor()
    {
        await OpenAsync();
        await Cell(1, "Amount").ClickAsync();

        await Page.Keyboard.TypeAsync("5");

        await Page.WaitForTimeoutAsync(300);
        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("25,000");
    }

    /// <summary>GS-042: Esc는 원래 값 복원, 값 전송 없음</summary>
    [Fact]
    public async Task GS_042_Esc_RestoresValue()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync("55");
        await Expect(Editor).ToHaveValueAsync("55");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Editor).ToHaveCountAsync(0);
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("10");
        await Expect(Page.Locator("[data-testid=changes]")).ToHaveTextAsync("0");
        await ExpectActiveAsync(1, "Qty");
        await Expect(Grid).ToBeFocusedAsync();
    }
}
