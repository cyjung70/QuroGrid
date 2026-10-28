using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-010 · GS-048 · GS-049: 연결 끊김 중 입력 잠금·재연결, 회로 소멸 후 안내, 페이지 이탈 경고 (R7, Addendum A-20·A-21).
/// 끊김은 SignalR WebSocket을 가로채 닫고, 재연결 요청(negotiate)을 붙잡아 두는 방식으로 만든다.
/// 샘플 서버는 끊긴 회로를 5초만 보관한다 (SampleServer: Circuit__RetentionSeconds).
/// </summary>
public class GS_010_048_049_ConnectionTests(SampleServer server) : GridPageTest(server)
{
    private readonly List<(IWebSocketRoute Page, IWebSocketRoute Server)> _sockets = [];
    private TaskCompletionSource _gate = Completed();

    private static TaskCompletionSource Completed()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }

    private ILocator Offline => Page.Locator("[role=grid][data-offline]");

    /// <summary>SignalR 연결을 가로챈 뒤 화면을 연다.</summary>
    private async Task OpenRoutedAsync(string query = "")
    {
        await Page.RouteAsync("**/_blazor/negotiate*", async route =>
        {
            await _gate.Task;   // 끊김 중에는 재연결 시도를 붙잡아 둔다
            await route.ContinueAsync();
        });
        await Page.RouteWebSocketAsync(new Regex("/_blazor"), ws => _sockets.Add((ws, ws.ConnectToServer())));
        await OpenAsync(query);
    }

    /// <summary>연결을 끊고 재연결을 막는다.</summary>
    private async Task DisconnectAsync()
    {
        _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (page, toServer) = _sockets[^1];
        await toServer.CloseAsync();
        await page.CloseAsync();
    }

    private void AllowReconnect() => _gate.SetResult();

    private async Task CommitQtyAsync(int row, string text)
    {
        await Cell(row, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync(text);
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Cell(row, "Qty")).ToHaveTextAsync(text);
    }

    // ── GS-010 ──

    [Fact]
    public async Task GS_010_Reconnect_LocksInput_KeepsChanges()
    {
        await OpenRoutedAsync();
        await CommitQtyAsync(2, "8");                      // 저장 전 변경 1건
        await Cell(1, "Qty").ClickAsync();
        await Page.Keyboard.TypeAsync("7");                // 편집 중 (커밋 전)
        await Expect(Editor).ToHaveValueAsync("7");        // 편집 시작(서버 왕복)이 끝난 뒤 끊는다

        await DisconnectAsync();

        await Expect(Offline).ToHaveCountAsync(1);
        await Expect(Page.Locator(".quro-notice-offline")).ToBeVisibleAsync();
        await Expect(Page.Locator(".quro-notice-offline .quro-notice-unsaved")).ToBeVisibleAsync();   // 미저장 변경 경고
        await Expect(Editor).Not.ToBeEditableAsync();      // 입력 잠금
        await Expect(Editor).ToHaveValueAsync("7");

        AllowReconnect();

        await Expect(Offline).ToHaveCountAsync(0);
        await Expect(Page.Locator(".quro-notice-offline")).ToBeHiddenAsync();
        await Expect(Editor).ToBeEditableAsync();
        await Expect(Editor).ToHaveValueAsync("7");        // 편집 중이던 값 유지
        await Editor.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");             // 같은 회로: 커밋이 그대로 된다

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("7");
        await Expect(Cell(2, "Qty")).ToHaveTextAsync("8"); // 끊기기 전 변경 유지
        await Expect(Status("changes")).ToHaveTextAsync("2");
    }

    // ── GS-048 ──

    [Fact]
    public async Task GS_048_CircuitGone_ShowsChangesLost()
    {
        await OpenRoutedAsync();
        await CommitQtyAsync(2, "8");

        await DisconnectAsync();
        await Expect(Page.Locator(".quro-notice-offline .quro-notice-unsaved")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(6500);             // 서버가 끊긴 회로를 버린다 (5초)
        AllowReconnect();

        // 새 회로로 다시 열린 화면: 변경은 사라지고 그 사실을 알린다 (O-03: 데이터는 보관하지 않음)
        var lost = Page.Locator(".quro-notice-lost");
        await Expect(lost).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(Cell(2, "Qty")).ToHaveTextAsync("5");
        await Expect(Status("changes")).ToHaveTextAsync("0");

        await lost.ClickAsync();                           // 눌러서 닫는다
        await Expect(lost).ToBeHiddenAsync();
    }

    [Fact]
    public async Task GS_048_NoChanges_NoLostNotice()
    {
        await OpenRoutedAsync();

        await DisconnectAsync();
        await Expect(Offline).ToHaveCountAsync(1);
        await Expect(Page.Locator(".quro-notice-offline .quro-notice-unsaved")).ToBeHiddenAsync();
        AllowReconnect();

        await Expect(Offline).ToHaveCountAsync(0);
        await Expect(Page.Locator(".quro-notice-lost")).ToBeHiddenAsync();
    }

    // ── GS-049 ──

    [Fact]
    public async Task GS_049_Reload_WithChanges_BrowserWarns()
    {
        await OpenAsync();
        await CommitQtyAsync(2, "8");
        string? dialog = null;
        Page.Dialog += async (_, d) => { dialog = d.Type; await d.DismissAsync(); };

        await Page.EvaluateAsync("() => location.reload()");
        await Page.WaitForTimeoutAsync(1000);

        Assert.Equal("beforeunload", dialog);
        await Expect(Status("changes")).ToHaveTextAsync("1");   // 같은 화면에 머문다
    }

    [Fact]
    public async Task GS_049_InternalLink_WithChanges_Confirms()
    {
        await OpenAsync();
        await CommitQtyAsync(2, "8");
        var messages = new List<string>();
        var accept = false;
        Page.Dialog += async (_, d) =>
        {
            messages.Add(d.Message);
            if (accept) await d.AcceptAsync(); else await d.DismissAsync();
        };

        await Status("home-link").ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        Assert.Equal("You have unsaved changes. Leave this page?", Assert.Single(messages));
        Assert.EndsWith("/po-lines", Page.Url);

        accept = true;
        await Status("home-link").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/$"));
    }

    [Fact]
    public async Task GS_049_NoChanges_LeavesWithoutPrompt()
    {
        await OpenAsync();
        var dialogs = 0;
        Page.Dialog += async (_, d) => { dialogs++; await d.DismissAsync(); };

        await Status("home-link").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/$"));
        Assert.Equal(0, dialogs);
    }
}
