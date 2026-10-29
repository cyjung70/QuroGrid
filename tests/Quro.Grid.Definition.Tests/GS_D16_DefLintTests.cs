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

    // ── L-3: *.lookup.toon in the same folder (Addendum A-48, lookup design v0.3 chapter 8) ──

    private static readonly Regex LookupErrorLine = new(@"^.+\.lookup\.toon:\d+: V-\d{2} .+$");

    /// <summary>A copy of the golden sd folder to change; deleted by <see cref="IDisposable.Dispose"/>.</summary>
    private sealed class SdCopy : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"quro-deflint-{Guid.NewGuid():N}");

        public SdCopy()
        {
            Directory.CreateDirectory(Folder);
            foreach (var file in Directory.GetFiles(Golden.PathOf("sd")))
                File.Copy(file, Path.Combine(Folder, Path.GetFileName(file)));
        }

        public string Shown(string name) => Folder.Replace('\\', '/') + "/" + name;

        public void Change(string name, string from, string to)
        {
            var path = Path.Combine(Folder, name);
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.Contains(from, text);
            File.WriteAllText(path, text.Replace(from, to));
        }

        public void Dispose() => Directory.Delete(Folder, recursive: true);
    }

    [Fact]
    public void GS_D16_Lookups_AreCounted()
    {
        var (code, _, error) = Lint(Golden.PathOf("sd"), "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(0, code);
        Assert.Contains("1 screen(s), 4 lookup(s), 0 error(s)", error);
    }

    /// <summary>Lookup load errors (V-01..V-03, V-33) are reported for the lookup file, even without a catalog</summary>
    [Fact]
    public void GS_D16_LookupLoadError()
    {
        using var sd = new SdCopy();
        sd.Change("discount.lookup.toon", "  key: dcCode", "  key: code");

        var (code, output, _) = Lint(sd.Folder);

        Assert.Equal(1, code);
        var line = Assert.Single(output);
        Assert.Matches(LookupErrorLine, line);
        Assert.StartsWith(sd.Shown("discount.lookup.toon") + ":", line);
        Assert.Contains(": V-33 ", line);
    }

    /// <summary>With a catalog, lookups get the catalog checks (V-34)</summary>
    [Fact]
    public void GS_D16_LookupCatalogError()
    {
        using var sd = new SdCopy();
        sd.Change("item.lookup.toon", "  title: mm.item.lookup.title", "  title: mm.item.popup.title");

        var (code, output, _) = Lint(sd.Folder, "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(1, code);
        var line = Assert.Single(output);
        Assert.StartsWith(sd.Shown("item.lookup.toon") + ":", line);
        Assert.Contains(": V-34 ", line);
    }

    /// <summary>With a catalog, screens are cross-checked against the lookups in the folder (V-35..V-37)</summary>
    [Fact]
    public void GS_D16_CrossCheck()
    {
        using var sd = new SdCopy();
        File.Delete(Path.Combine(sd.Folder, "discount.lookup.toon"));
        sd.Change("sd.so.entry.screen.toon", "\"item.byCustomer(customer)\"", "\"item.byVendor(customer)\"");

        var (code, output, _) = Lint(sd.Folder, "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(1, code);
        Assert.All(output, line => Assert.StartsWith(sd.Shown("sd.so.entry.screen.toon") + ":", line));
        Assert.Equal(["V-37", "V-35"], output.Select(l => l.Split(": ")[1].Split(' ')[0]));   // dcCode column, then the rule
    }

    /// <summary>Without a catalog there are no cross-checks (the validator needs the catalog)</summary>
    [Fact]
    public void GS_D16_NoCatalog_NoCrossCheck()
    {
        using var sd = new SdCopy();
        File.Delete(Path.Combine(sd.Folder, "discount.lookup.toon"));

        Assert.Equal(0, Lint(sd.Folder).Code);
    }

    /// <summary>A lookup that does not load is not used for cross-checks, so screens get no follow-up V-37 for it</summary>
    [Fact]
    public void GS_D16_BrokenLookup_SkipsCrossCheck()
    {
        using var sd = new SdCopy();
        sd.Change("discount.lookup.toon", "  key: dcCode", "  key: code");

        var (code, output, error) = Lint(sd.Folder, "--catalog", Golden.PathOf("catalog.json"));

        Assert.Equal(1, code);
        Assert.All(output, line => Assert.Matches(LookupErrorLine, line));
        Assert.Contains("cross-checks skipped", error);
    }
}
