using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// 샘플 구매오더 라인 화면(/po-lines)을 여는 E2E 기반. 행 키는 조회 순서대로 1, 2, 3.
/// 열: LineNo(읽기 전용) · ItemCode · Qty · UnitPrice · Amount(읽기 전용)
/// </summary>
[Collection(SampleCollection.Name)]
public abstract class GridPageTest(SampleServer server) : PageTest
{
    /// <summary>브라우저 → 서버로 보낸 WebSocket(SignalR) 프레임 수.</summary>
    protected int SentFrames { get; private set; }

    protected ILocator Grid => Page.Locator("[role=grid]");

    protected async Task OpenAsync(string query = "")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, _) => SentFrames++;
        await Page.GotoAsync($"{server.BaseUrl}/po-lines{query}");
        // quro-grid.js가 등록되면 그리드 루트에 표시가 붙는다 (회로 연결 완료)
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    protected ILocator Cell(int row, string field) =>
        Page.Locator($"[role=row][data-row='{row}'] [data-field='{field}']");

    protected ILocator Editor => Page.Locator(".quro-editor");

    protected async Task<(int Row, string Field)> ActiveAsync()
    {
        var row = await Grid.GetAttributeAsync("data-active-row");
        var field = await Grid.GetAttributeAsync("data-active-field");
        return (int.Parse(row!), field!);
    }

    protected async Task ExpectActiveAsync(int row, string field)
    {
        await Expect(Grid).ToHaveAttributeAsync("data-active-row", row.ToString());
        await Expect(Grid).ToHaveAttributeAsync("data-active-field", field);
    }

    /// <summary>서버 왕복이 있었다면 그 결과가 반영될 시간을 준 뒤 프레임 수를 확인한다.</summary>
    protected async Task<int> FramesAfterSettleAsync(int baseline)
    {
        await Page.WaitForTimeoutAsync(300);
        return SentFrames - baseline;
    }
}
