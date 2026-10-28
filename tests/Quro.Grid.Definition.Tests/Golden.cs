namespace Quro.Grid.Definition.Tests;

/// <summary>Golden screen definition files copied next to the test assembly.</summary>
internal static class Golden
{
    public static string PathOf(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", relative.Replace('/', Path.DirectorySeparatorChar));

    public static string Read(string relative) => File.ReadAllText(PathOf(relative));

    /// <summary>1-based number of the only line that contains <paramref name="text"/>.</summary>
    public static int LineOf(string relative, string text)
    {
        var lines = File.ReadAllLines(PathOf(relative));
        var hits = Enumerable.Range(0, lines.Length).Where(i => lines[i].Contains(text, StringComparison.Ordinal)).ToList();
        Assert.Single(hits);
        return hits[0] + 1;
    }

    public static DefinitionResult<ScreenDef> Load(string relative) =>
        ScreenDefLoader.Load(PathOf(relative), Read(relative));
}
