using System.Text.RegularExpressions;

namespace Quro.Grid.Tests;

/// <summary>
/// GS-080 테마 토큰 (P1 5-8): 그리드 색은 CSS 변수로만 정의한다. 앱은 <c>--quro-*</c>를 그리드나 그 바깥 요소에 두어 바꾸고,
/// 두지 않으면 MudBlazor 팔레트(<c>--mud-palette-*</c>), 그것도 없으면 기본값을 쓴다.
/// 그리드 안에서는 이를 풀어 둔 <c>--qg-*</c>만 쓴다.
/// </summary>
public class GS_080_ThemeTokenTests
{
    private static readonly Regex ColorLiteral = new(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(", RegexOptions.Compiled);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QuroGrid.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("저장소 루트를 찾지 못했습니다.");
    }

    private static string GridFile(string name) => File.ReadAllText(Path.Combine(Root(), "src", "Quro.Grid", name));

    /// <summary>토큰 정의 줄: --qg-이름: var(--quro-이름, ...) — 색 기본값은 여기에만</summary>
    private static readonly Regex TokenLine = new(@"^\s*--qg-([\w-]+):\s*var\(--quro-([\w-]+),", RegexOptions.Compiled);

    [Fact]
    public void GS_080_Css_ColorLiteralsOnlyInTokenDefinitions()
    {
        var lines = GridFile("wwwroot/quro-grid.css").Split('\n');
        var offenders = lines.Select((l, i) => (Line: l.Trim(), No: i + 1))
            .Where(x => ColorLiteral.IsMatch(x.Line) && !TokenLine.IsMatch(x.Line))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void GS_080_Tokens_ResolveFromPublicName_SameName()
    {
        var tokens = GridFile("wwwroot/quro-grid.css").Split('\n')
            .Select(l => TokenLine.Match(l)).Where(m => m.Success).ToList();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, m => Assert.Equal(m.Groups[1].Value, m.Groups[2].Value));   // --qg-x ← --quro-x
        var names = tokens.Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var required in new[] { "bg", "text", "border", "head-bg", "active", "selected", "error", "error-line", "warning", "warning-line" })
            Assert.Contains(required, names);
    }

    [Fact]
    public void GS_080_Css_UsesOnlyResolvedTokens()
    {
        // --quro-*(색)는 토큰 정의에서만 읽는다. --quro-row-height는 그리드가 RowHeight로 넣는 배치 값 (색 아님)
        var outside = GridFile("wwwroot/quro-grid.css").Split('\n')
            .Where(l => !TokenLine.IsMatch(l))
            .SelectMany(l => Regex.Matches(l, @"var\(--quro-([\w-]+)").Select(m => m.Groups[1].Value))
            .ToList();

        Assert.Equal(["row-height"], outside);
    }

    [Fact]
    public void GS_080_JsAndMarkup_NoColorLiterals_UseResolvedTokens()
    {
        var js = GridFile("wwwroot/quro-grid.js");
        Assert.DoesNotMatch(ColorLiteral, js);
        Assert.DoesNotMatch(new Regex(@"var\(--quro-"), js);
        Assert.Matches(new Regex(@"var\(--qg-active\)"), js);

        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src", "Quro.Grid"), "*.razor"))
            Assert.DoesNotMatch(ColorLiteral, File.ReadAllText(file));
    }
}
