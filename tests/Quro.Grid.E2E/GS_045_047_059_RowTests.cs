namespace Quro.Grid.E2E;

/// <summary>
/// GS-045 · GS-047 · GS-059: 행 추가·삭제 단축키, 일괄 삭제·행 복사 (R3), 선택 변경 이벤트 (Addendum A-12·A-13).
/// 샘플: 툴바 [data-testid=copy-rows] · [data-testid=delete-rows], 쿼리 selection=1이면 OnSelectionChanged 구독.
/// </summary>
public class GS_045_047_059_RowTests(SampleServer server) : GridPageTest(server)
{
    private async Task<int[]> RowKeysAsync()
    {
        var keys = await Page.Locator("[role=row][data-row]").EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.row)");
        return keys.Select(int.Parse).ToArray();
    }

    /// <summary>GS-045: Insert → 활성 행 바로 아래에 행 추가 (A-15), 새 행 첫 편집 가능 셀로</summary>
    [Fact]
    public async Task GS_045_Insert_AddsRowBelowActive()
    {
        await OpenAsync();
        await Cell(1, "Qty").ClickAsync();

        await Page.Keyboard.PressAsync("Insert");

        Assert.Equal(new[] { 1, -1, 2, 3 }, await RetryRowKeysAsync(4));
        await ExpectActiveAsync(-1, "ItemCode");
        await Expect(Cell(-1, "LineNo")).ToHaveTextAsync("4");          // 샘플이 OnRowAdding에서 행 번호 부여
    }

    /// <summary>GS-045: Ctrl+Delete → 활성 행 삭제, 같은 열의 다음 행으로</summary>
    [Fact]
    public async Task GS_045_CtrlDelete_DeletesRow()
    {
        await OpenAsync();
        await Cell(2, "Qty").ClickAsync();

        await Page.Keyboard.PressAsync("Control+Delete");

        Assert.Equal(new[] { 1, 3 }, await RetryRowKeysAsync(2));
        await ExpectActiveAsync(3, "Qty");
        await Expect(Status("changes")).ToHaveTextAsync("1");
    }

    /// <summary>GS-047: 여러 행 선택 후 Ctrl+Delete → 서버 호출 1회</summary>
    [Fact]
    public async Task GS_047_BulkDelete_OneServerCall()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");
        var baseline = InvokeFrames;

        await Page.Keyboard.PressAsync("Control+Delete");

        Assert.Equal(new[] { 3 }, await RetryRowKeysAsync(1));
        await ExpectActiveAsync(3, "ItemCode");
        Assert.Equal(1, InvokeFrames - baseline);
    }

    /// <summary>GS-047: 행 복사 — 각 원래 행 바로 아래 (A-12)</summary>
    [Fact]
    public async Task GS_047_CopyRows_InsertsBelowOriginals()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Status("copy-rows").ClickAsync();

        Assert.Equal(new[] { 1, -1, 2, -2, 3 }, await RetryRowKeysAsync(5));
        await Expect(Cell(-1, "ItemCode")).ToHaveTextAsync("A1001");
        await Expect(Cell(-2, "ItemCode")).ToHaveTextAsync("A1002");
        await Expect(Cell(-1, "LineNo")).ToHaveTextAsync("4");
        await Expect(Cell(-2, "LineNo")).ToHaveTextAsync("5");
        await Expect(Status("changes")).ToHaveTextAsync("2");
    }

    /// <summary>GS-047: 툴바 일괄 삭제도 선택 범위 기준</summary>
    [Fact]
    public async Task GS_047_ToolbarDelete_UsesSelection()
    {
        await OpenAsync();
        await Cell(2, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Status("delete-rows").ClickAsync();

        Assert.Equal(new[] { 1 }, await RetryRowKeysAsync(1));
    }

    /// <summary>GS-059: 활성 행이 바뀔 때만 OnSelectionChanged (같은 행 셀 이동은 서버 호출 없음)</summary>
    [Fact]
    public async Task GS_059_SelectionChanged_OnlyOnRowChange()
    {
        await OpenAsync("?selection=1");
        await Cell(1, "ItemCode").ClickAsync();
        await Expect(Status("selected-row")).ToHaveTextAsync("1");
        await Page.WaitForTimeoutAsync(300);
        var baseline = RowNotifyFrames;

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(0, RowNotifyFrames - baseline);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Status("selected-row")).ToHaveTextAsync("2");
        Assert.Equal(1, RowNotifyFrames - baseline);
    }

    /// <summary>GS-059: 빠르게 여러 행을 지나가면 멈춘 행만 보낸다</summary>
    [Fact]
    public async Task GS_059_FastMoves_SendsLastRowOnly()
    {
        await OpenAsync("?selection=1");
        await Cell(1, "ItemCode").ClickAsync();
        await Expect(Status("selected-row")).ToHaveTextAsync("1");
        await Page.WaitForTimeoutAsync(300);
        var baseline = RowNotifyFrames;

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Status("selected-row")).ToHaveTextAsync("3");
        await Page.WaitForTimeoutAsync(300);
        Assert.Equal(1, RowNotifyFrames - baseline);
    }

    /// <summary>행 수가 기대값이 될 때까지 기다린 뒤 행 키 목록</summary>
    private async Task<int[]> RetryRowKeysAsync(int count)
    {
        await Expect(Page.Locator("[role=row][data-row]")).ToHaveCountAsync(count);
        return await RowKeysAsync();
    }
}
