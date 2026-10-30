namespace Quro.Grid.E2E;

/// <summary>
/// GS-081 다크 모드 (P1 5-8, A-67). 샘플 ?dark=1이면 화면이 다크 값의 --quro-* 토큰을 둔다.
/// 그리드 색은 앱 토큰을 따르고(없으면 기본값), 글자와 배경(일반·헤더·선택·오류·경고)의 명암비는 4.5:1 이상.
/// </summary>
public class GS_081_DarkModeTests(SampleServer server) : GridPageTest(server)
{
    /// <summary>
    /// 그리드 안에 임시 요소를 두고 CSS 값을 계산된 sRGB로 읽는다 (color-mix·투명도 포함).
    /// 결과: 이름 → [r, g, b] (0~255). 글자색은 배경 위에 합성한 값.
    /// </summary>
    private const string Measure = """
        () => {
            const grid = document.querySelector('[role=grid]');
            const probe = document.createElement('div');
            grid.appendChild(probe);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 1;
            const ctx = canvas.getContext('2d', { willReadFrequently: true });
            const rgb = (css, under = null) => {
                ctx.clearRect(0, 0, 1, 1);
                if (under) { ctx.fillStyle = under; ctx.fillRect(0, 0, 1, 1); }
                ctx.fillStyle = css; ctx.fillRect(0, 0, 1, 1);
                return [...ctx.getImageData(0, 0, 1, 1).data.slice(0, 3)];
            };
            const read = v => { probe.style.color = `var(${v})`; return getComputedStyle(probe).color; };
            const bg = read('--qg-bg');
            const out = {};
            for (const n of ['bg', 'head-bg', 'selected', 'error', 'warning']) out[n] = rgb(read(`--qg-${n}`), bg);
            out.text = rgb(read('--qg-text'), bg);
            out.appBg = rgb(getComputedStyle(grid).getPropertyValue('--quro-bg').trim() || 'transparent', '#808080');
            out.gridBg = rgb(getComputedStyle(grid).backgroundColor, bg);
            out.cellText = rgb(getComputedStyle(grid.querySelector('.quro-body [role=gridcell]')).color, bg);
            probe.remove();
            return out;
        }
        """;

    private static double Luminance(int[] c)
    {
        double Channel(int v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c[0]) + 0.7152 * Channel(c[1]) + 0.0722 * Channel(c[2]);
    }

    private static double Contrast(int[] a, int[] b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private async Task<Dictionary<string, int[]>> ColorsAsync() =>
        await Page.EvaluateAsync<Dictionary<string, int[]>>(Measure);

    private static void AssertReadable(Dictionary<string, int[]> c)
    {
        foreach (var background in new[] { "bg", "head-bg", "selected", "error", "warning" })
            Assert.True(Contrast(c["text"], c[background]) >= 4.5,
                $"{background}: 명암비 {Contrast(c["text"], c[background]):0.00}");
        Assert.Equal(c["text"], c["cellText"]);              // 셀 글자는 토큰 색
        Assert.Equal(c["bg"], c["gridBg"]);
    }

    [Fact]
    public async Task GS_081_DarkMode_FollowsAppTokens_Readable()
    {
        await OpenAsync("?dark=1");

        var c = await ColorsAsync();

        Assert.Equal(c["appBg"], c["bg"]);                   // 배경 = 앱이 둔 다크 --quro-bg
        Assert.True(Luminance(c["bg"]) < 0.1, "다크 배경");
        Assert.True(Luminance(c["text"]) > 0.4, "밝은 글자");
        AssertReadable(c);
    }

    [Fact]
    public async Task GS_081_LightMode_Defaults_Readable()
    {
        await OpenAsync();

        var c = await ColorsAsync();

        Assert.Equal([128, 128, 128], c["appBg"]);           // 앱 토큰 없음
        Assert.Equal([255, 255, 255], c["bg"]);              // 기본값
        Assert.True(Luminance(c["bg"]) > 0.8, "밝은 배경");
        AssertReadable(c);
    }

    [Fact]
    public async Task GS_081_AppOverridesToken_OnAncestor()
    {
        await OpenAsync();
        await Page.EvaluateAsync("() => document.body.style.setProperty('--quro-bg', 'rgb(250, 240, 200)')");

        var c = await ColorsAsync();

        Assert.Equal([250, 240, 200], c["bg"]);
        Assert.Equal([250, 240, 200], c["gridBg"]);
    }
}
