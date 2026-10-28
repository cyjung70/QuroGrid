using System.Text.RegularExpressions;
using Quro.Grid.DefLint;

namespace Quro.Grid.Definition.Tests;

/// <summary>
/// GS-D16 (T-12): <c>quro-deflint &lt;folder&gt; [--catalog catalog.json]</c> checks every <c>*.screen.toon</c> under a
/// folder. Exit code 0 = no errors, 1 = errors, 2 = usage. One output line per error: <c>file:line: V-xx message</c>.
/// </summary>
public class GS_D16_DefLintTests
{
    private static readonly Regex ErrorLine = new(@"^.+\.screen\.toon:\d+: V-\d{2} .+$");

    private static (int Code, string[] Out, string Err) Lint(params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = DefLintCommand.Run(args, output, error);
        var lines = output.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return (code, lines, error.ToString());
    }

    [Fact]
    public void GS_D16_GoldenFolder_WithCatalog_ExitsZero()
    {
        var (code, output, error) = Lint(Golden.PathOf("sd"), "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(0, code);
        Assert.Empty(output);
        Assert.Contains("1", error);   // summary on stderr: files checked
    }

    [Fact]
    public void GS_D16_GoldenFolder_WithoutCatalog_ExitsZero()
    {
        Assert.Equal(0, Lint(Golden.PathOf("sd")).Code);
    }

    [Fact]
    public void GS_D16_BadFolder_ExitsOne_OneLinePerError()
    {
        var folder = Golden.PathOf("bad");

        var (code, output, _) = Lint(folder, "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(1, code);
        Assert.NotEmpty(output);
        Assert.All(output, line => Assert.Matches(ErrorLine, line));
        Assert.All(output, line => Assert.StartsWith(folder.Replace('\\', '/') + "/count-mismatch.screen.toon:", line));
        Assert.Contains(output, line => line.Contains(": V-01 ", StringComparison.Ordinal));
    }

    [Fact]
    public void GS_D16_SubFolders_AreIncluded()
    {
        var (code, output, _) = Lint(Golden.PathOf(""));

        Assert.Equal(1, code);
        Assert.All(output, line => Assert.Contains("/bad/count-mismatch.screen.toon:", line));
    }

    /// <summary>Catalog checks (V-19..V-32) run when the screen loads: a key missing from the catalog is reported</summary>
    [Fact]
    public void GS_D16_CatalogMissingKey_ExitsOne()
    {
        var catalog = Path.Combine(Path.GetTempPath(), $"quro-deflint-{Guid.NewGuid():N}.json");
        File.WriteAllText(catalog, Golden.Read("catalog.json").Replace("\"msg.qty.zero\",", ""));
        try
        {
            var (code, output, _) = Lint(Golden.PathOf("sd"), "--catalog", catalog);

            Assert.Equal(1, code);
            var line = Assert.Single(output);
            Assert.Matches(ErrorLine, line);
            Assert.Contains(": V-21 ", line);
            Assert.Contains("msg.qty.zero", line);
        }
        finally
        {
            File.Delete(catalog);
        }
    }

    [Theory]
    [InlineData]
    [InlineData("--catalog")]
    [InlineData("does-not-exist")]
    [InlineData("{sd}", "--unknown")]
    [InlineData("{sd}", "--catalog", "does-not-exist.json")]
    [InlineData("{sd}", "--catalog", "{sd}/sd.so.entry.screen.toon")]   // not JSON
    [InlineData("{sd}", "{sd}")]
    public void GS_D16_Usage_ExitsTwo(params string[] args)
    {
        var (code, output, error) = Lint(args.Select(a => a.Replace("{sd}", Golden.PathOf("sd"))).ToArray());

        Assert.Equal(2, code);
        Assert.Empty(output);
        Assert.Contains("quro-deflint", error);
    }
}
