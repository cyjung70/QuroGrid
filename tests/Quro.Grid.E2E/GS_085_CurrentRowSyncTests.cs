namespace Quro.Grid.E2E;

/// <summary>
/// GS-085: 그리드 활성 행 ↔ DataSet 현재 행 (Addendum A-51).
/// 샘플: [data-testid=move-last]는 DataSet.MoveTo(마지막 행), [data-testid=current-row]는 CurrentRowKey (상태 갱신 버튼으로 다시 그림).
/// </summary>
public class GS_085_CurrentRowSyncTests(SampleServer server) : GridPageTest(server)
{
    /// <summary>GS-085: 그리드에서 다른 행으로 옮기면 DataSet 현재 행이 그 행 (OnSelectionChanged 구독 없이)</summary>
    [Fact]
    public async Task GS_085_GridClick_MovesDataSetCurrentRow()
    {
        await OpenAsync();
        await Cell(2, "Qty").ClickAsync();
        await Page.WaitForTimeoutAsync(300);   // 활성 행 알림은 100ms 뒤 (A-13)

        await Status("refresh-status").ClickAsync();

        await Expect(Status("current-row")).ToHaveTextAsync("2");
    }

    /// <summary>GS-085: 앱이 MoveTo하면 그리드 활성 셀이 그 행의 같은 열로. 포커스는 가져오지 않는다</summary>
    [Fact]
    public async Task GS_085_AppMoveTo_MovesGridActiveCell_KeepsFocus()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        var last = await Page.Locator("[role=row][data-row]").Last.GetAttributeAsync("data-row");

        await Status("move-last").ClickAsync();

        await ExpectActiveAsync(int.Parse(last!), "ItemCode");
        Assert.Equal("move-last", await Page.EvaluateAsync<string?>("() => document.activeElement?.dataset.testid"));
    }
}
