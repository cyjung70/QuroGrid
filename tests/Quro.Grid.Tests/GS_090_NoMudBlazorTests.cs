using System.Text.RegularExpressions;

namespace Quro.Grid.Tests;

/// <summary>
/// GS-090 MudBlazor 없음 (A-67): 패키지·샘플·테스트 어디에도 MudBlazor 참조, Mud 컴포넌트, <c>--mud-*</c> 변수가 없다.
/// 셀 선택 목록(달력·Combo 목록)은 그리드가 직접 그리고, 색은 <c>--quro-*</c> 토큰과 기본값만 쓴다.
/// </summary>
public class GS_090_NoMudBlazorTests
{
    private static readonly Regex Mud = new(@"\bmud", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] Extensions = [".cs", ".razor", ".css", ".js", ".csproj", ".props", ".json", ".cshtml"];

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QuroGrid.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("저장소 루트를 찾지 못했습니다.");
    }

    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory.EnumerateFiles(Path.Combine(Root(), folder), "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f)))
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or "node_modules"));

    [Theory]
    [InlineData("src")]
    [InlineData("tests")]
    public void GS_090_NoMudBlazorReferences(string folder)
    {
        const string self = nameof(GS_090_NoMudBlazorTests) + ".cs";   // 이 파일은 검사 대상 이름을 담는다
        var offenders = SourceFiles(folder)
            .Where(f => Path.GetFileName(f) != self)
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetRelativePath(Root(), f), No: i + 1, Line: line)))
            .Where(x => Mud.IsMatch(x.Line))
            .Select(x => $"{x.File}:{x.No}: {x.Line.Trim()}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void GS_090_GridPackage_NoMudBlazorDependency()
    {
        var csproj = File.ReadAllText(Path.Combine(Root(), "src", "Quro.Grid", "Quro.Grid.csproj"));

        Assert.DoesNotContain("MudBlazor", csproj);
    }
}
