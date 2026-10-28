using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-055 ~ GS-058: 성능 (Spec 4.11 목표치 초안). 결과는 테스트 출력과 bin의 perf-results.md에 남긴다 (PR 첨부용).
/// 로컬(같은 PC) 측정이라 사내망 왕복은 포함하지 않는다. 느린 환경에서는 Category=Performance로 빼고 돌릴 수 있다.
/// </summary>
[Trait("Category", "Performance")]
public class GS_055_058_PerformanceTests(SampleServer server, ITestOutputHelper output) : GridPageTest(server)
{
    private const double OneFrameMs = 1000.0 / 60;
    private const double MaxCircuitMb = 100;   // GS-058 회로당 메모리 상한 (Addendum A-27)
    private readonly SampleServer _server = server;

    private void Report(string id, string metric, string value, string target)
    {
        var line = $"| {id} | {metric} | {value} | {target} |";
        output.WriteLine(line);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "perf-results.md"), line + Environment.NewLine);
    }

    private static double P95(IReadOnlyList<double> samples) =>
        samples.Order().ElementAt((int)Math.Ceiling(samples.Count * 0.95) - 1);

    private static string Ms(double ms) => ms.ToString("0.0", CultureInfo.InvariantCulture) + "ms";

    /// <summary>GS-055: 셀 이동 키 → 활성 셀 표시까지. 서버 왕복 없이 1프레임(16.7ms) 이내</summary>
    [Fact]
    public async Task GS_055_ArrowKey_ResponseWithinOneFrame_NoServerCall()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var frames = SentFrames;

        var times = await Grid.EvaluateAsync<double[]>("""
            async grid => {
                const times = [];
                for (let i = 0; i < 40; i++) {
                    const changed = new Promise(resolve => {
                        const mo = new MutationObserver(() => { mo.disconnect(); resolve(performance.now()); });
                        mo.observe(grid, { attributes: true, attributeFilter: ['data-active-field'] });
                    });
                    const t0 = performance.now();
                    grid.dispatchEvent(new KeyboardEvent('keydown', { key: i % 2 ? 'ArrowLeft' : 'ArrowRight', bubbles: true }));
                    times.push(await changed - t0);
                    await new Promise(r => requestAnimationFrame(r));
                }
                return times;
            }
            """);

        Assert.Equal(0, await FramesAfterSettleAsync(frames));   // 서버 호출 없음 (R1·R2)
        Report("GS-055", "셀 이동 키 반응 (p95 / 최대, 40회)", $"{Ms(P95(times))} / {Ms(times.Max())}", "1프레임(16.7ms) 이내");
        Assert.True(P95(times) < OneFrameMs, $"p95 {P95(times)}ms");
    }

    /// <summary>GS-056: 셀 편집 커밋 왕복 (Enter → 셀에 확정 값 표시). 150ms 이하</summary>
    [Fact]
    public async Task GS_056_CommitRoundTrip_Under150ms()
    {
        await OpenAsync();
        var times = new List<double>();

        for (var i = 0; i < 20; i++)
        {
            var row = i % 3 + 1;
            var value = (100 + i).ToString(CultureInfo.InvariantCulture);
            await Cell(row, "Qty").ClickAsync();
            await Page.Keyboard.TypeAsync(value);
            await Expect(Editor).ToHaveValueAsync(value);

            times.Add(await Grid.EvaluateAsync<double>("""
                (grid, [row, value]) => new Promise(resolve => {
                    const cell = () => grid.querySelector(`[role=row][data-row="${row}"] [data-field="Qty"]`);
                    const mo = new MutationObserver(() => {
                        const c = cell();
                        if (c && !c.querySelector('.quro-editor') && c.textContent.trim() === value) {
                            mo.disconnect();
                            resolve(performance.now() - t0);
                        }
                    });
                    mo.observe(grid, { subtree: true, childList: true, characterData: true });
                    const t0 = performance.now();
                    grid.querySelector('.quro-editor').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
                })
                """, new object[] { row, value }));
        }

        Report("GS-056", "셀 편집 커밋 왕복 (p95 / 최대, 20회, 로컬)", $"{Ms(P95(times))} / {Ms(times.Max())}", "150ms 이하 (사내망)");
        Assert.True(P95(times) <= 150, $"p95 {P95(times)}ms");
    }

    /// <summary>GS-057: 500행 × 10열 붙여넣기. 2초 이하, 서버 호출 1회 (R3)</summary>
    [Fact]
    public async Task GS_057_Paste500x10_Under2s_OneCall()
    {
        await OpenAsync("?wide=1");
        await Cell(1, "ItemCode").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var rows = Enumerable.Range(1, 500).Select(n => string.Join('\t',
            $"B{n:0000}", n, n * 10, $"R{n}", $"S{n}", $"M{n}", "KR", $"L{n}", $"W{n % 9}", "11/5/2026"));
        var invokes = InvokeFrames;

        var watch = Stopwatch.StartNew();
        await PasteAsync(string.Join("\r\n", rows));
        await Expect(Status("pastes")).ToHaveTextAsync("1", new() { Timeout = 10000 });
        await Expect(Status("changes")).ToHaveTextAsync("500", new() { Timeout = 10000 });
        await Expect(Page.Locator(".quro-body [data-field=Location]", new() { HasTextString = "W5" }).Last).ToBeVisibleAsync();
        watch.Stop();

        Report("GS-057", "500행 × 10열 붙여넣기 (화면 반영까지)", $"{watch.ElapsedMilliseconds}ms, 호출 {InvokeFrames - invokes}회", "2초 이하, 호출 1회");
        Assert.Equal(1, InvokeFrames - invokes);
        Assert.True(watch.ElapsedMilliseconds <= 2000, $"{watch.ElapsedMilliseconds}ms");
    }

    /// <summary>GS-058: 회로당 서버 메모리 100MB 이하 (A-27). 대량 조회 화면 5개</summary>
    [Fact]
    public async Task GS_058_MemoryPerCircuit_Under100MB()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_server.BaseUrl) };
        async Task<long> HeapAsync() => (await http.GetFromJsonAsync<MemoryInfo>("/_diag/memory"))!.HeapBytes;

        await Page.WaitForTimeoutAsync(6000);   // 앞 테스트의 끊긴 회로가 버려질 때까지 (보관 5초)
        var before = await HeapAsync();
        var pages = new List<Microsoft.Playwright.IPage>();
        for (var i = 0; i < 5; i++)
        {
            var page = await Context.NewPageAsync();
            await page.GotoAsync($"{_server.BaseUrl}/po-lines?large=1");
            await page.WaitForSelectorAsync("[role=grid][data-quro-grid] [role=row][data-row]");
            await page.Locator("[role=grid]").EvaluateAsync("g => g.scrollTop = 5000 * 28");
            await page.WaitForSelectorAsync("[data-field=LineNo]:text-is('5,000')");
            pages.Add(page);
        }
        var after = await HeapAsync();

        var perCircuit = (after - before) / 5.0 / 1024 / 1024;
        Report("GS-058", "회로당 관리 힙 증가 (대량 조회 화면, 5회로 평균)",
            perCircuit.ToString("0.00", CultureInfo.InvariantCulture) + "MB", "100MB 이하");
        Assert.True(after > before, $"before {before}, after {after}");
        Assert.True(perCircuit <= MaxCircuitMb, $"{perCircuit}MB");
        foreach (var page in pages)
            await page.CloseAsync();
    }

    private sealed record MemoryInfo(long HeapBytes);
}
