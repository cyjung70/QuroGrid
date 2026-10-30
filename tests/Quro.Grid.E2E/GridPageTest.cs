using System.Text;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// 샘플 구매오더 라인 화면(/po-lines)을 여는 E2E 기반. 행 키는 조회 순서대로 1, 2, 3.
/// 열: LineNo(읽기 전용) · ItemCode · Qty · UnitPrice · [DueDate: dates=1] · Amount(읽기 전용)
/// </summary>
[Collection(SampleCollection.Name)]
public abstract class GridPageTest(SampleServer server) : PageTest
{
    /// <summary>
    /// 브라우저 → 서버로 보낸 WebSocket(SignalR) 프레임 수. 활성 행 알림(<see cref="RowNotifyFrames"/>)은 빼고 센다:
    /// 행이 멈출 때마다 100ms 뒤 한 번 가는 알림이라(A-13·A-51) 사용자 동작의 서버 호출과 따로 검증한다.
    /// </summary>
    protected int SentFrames { get; private set; }

    /// <summary>그중 .NET 메서드 호출(JS → 서버 Interop) 프레임 수. 렌더 완료 통지 등은 제외 (R3 검증용). 활성 행 알림도 제외.</summary>
    protected int InvokeFrames { get; private set; }

    /// <summary>활성 행 알림(SelectionChanged) 호출 프레임 수 (GS-059·GS-085).</summary>
    protected int RowNotifyFrames { get; private set; }

    protected ILocator Grid => Page.Locator("[role=grid]");

    /// <summary>화면 언어를 영어로 고정한다 (PC 언어와 무관하게 안내 문구 확인).</summary>
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    protected async Task OpenAsync(string query = "")
    {
        Page.WebSocket += (_, ws) => ws.FrameSent += (_, f) =>
        {
            var payload = f.Binary is { } bytes ? Encoding.UTF8.GetString(bytes) : f.Text ?? "";
            var invoke = payload.Contains("BeginInvokeDotNetFromJS", StringComparison.Ordinal);
            if (invoke && payload.Contains("SelectionChanged", StringComparison.Ordinal))
            {
                RowNotifyFrames++;
                return;
            }
            SentFrames++;
            if (invoke)
                InvokeFrames++;
        };
        await Page.GotoAsync($"{server.BaseUrl}/po-lines{query}");
        // quro-grid.js가 등록되면 그리드 루트에 표시가 붙는다 (회로 연결 완료)
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid]");
    }

    protected ILocator Cell(int row, string field) =>
        Page.Locator($"[role=row][data-row='{row}'] [data-field='{field}']");

    protected ILocator Editor => Page.Locator(".quro-editor");

    protected ILocator Status(string id) => Page.Locator($"[data-testid={id}]");

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

    /// <summary>그리드에 클립보드 붙여넣기 (엑셀 형식 텍스트). 실제 Ctrl+V와 같은 paste 이벤트를 보낸다.</summary>
    protected Task PasteAsync(string text) =>
        Page.EvaluateAsync("""
            text => {
                const dt = new DataTransfer();
                dt.setData('text/plain', text);
                document.activeElement.dispatchEvent(
                    new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
            }
            """, text);

    /// <summary>서버 왕복이 있었다면 그 결과가 반영될 시간을 준 뒤 프레임 수를 확인한다.</summary>
    protected async Task<int> FramesAfterSettleAsync(int baseline)
    {
        await Page.WaitForTimeoutAsync(300);
        return SentFrames - baseline;
    }
}
