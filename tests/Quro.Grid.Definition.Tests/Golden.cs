namespace Quro.Grid.Definition.Tests;

/// <summary>Golden screen definition files copied next to the test assembly.</summary>
internal static class Golden
{
    public static string PathOf(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", relative.Replace('/', Path.DirectorySeparatorChar));

    public static string Read(string relative) => File.ReadAllText(PathOf(relative));

    /// <summary>1-based number of the only line that contains <paramref name="text"/>.</summary>
    public static int LineOf(string relative, string text) => LineIn(Read(relative), text);

    public static DefinitionResult<ScreenDef> Load(string relative) =>
        ScreenDefLoader.Load(PathOf(relative), Read(relative));

    public static DefinitionResult<LookupDef> LoadLookup(string relative) =>
        LookupDefLoader.Load(PathOf(relative), Read(relative));

    /// <summary>1-based number of the only line of <paramref name="text"/> that contains <paramref name="fragment"/>.</summary>
    public static int LineIn(string text, string fragment)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var hits = Enumerable.Range(0, lines.Length).Where(i => lines[i].Contains(fragment, StringComparison.Ordinal)).ToList();
        Assert.Single(hits);
        return hits[0] + 1;
    }

    /// <summary>Test catalog from <c>Golden/catalog.json</c> (GS-D.md chapter 2).</summary>
    public static IQuroDefinitionCatalog Catalog { get; } = TestCatalog.Read(PathOf("catalog.json"));

    private sealed record TestCatalog(
        IReadOnlySet<string> ResourceKeys, IReadOnlySet<string> CodeMajors, IReadOnlySet<string> LookupIds,
        IReadOnlySet<string> Handlers, IReadOnlySet<string> Permissions) : IQuroDefinitionCatalog
    {
        public static TestCatalog Read(string path)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            HashSet<string> Set(string name) => [.. doc.RootElement.GetProperty(name).EnumerateArray().Select(e => e.GetString()!)];
            return new TestCatalog(Set("resourceKeys"), Set("codeMajors"), Set("lookupIds"), Set("handlers"), Set("permissions"));
        }
    }
}
