using System.Text.Json;
using Quro.Grid.Definition;

namespace Quro.Grid.DefLint;

/// <summary>
/// <c>quro-deflint &lt;folder&gt; [--catalog catalog.json]</c> (T-12, GS-D16): loads every <c>*.screen.toon</c> under the
/// folder with <see cref="ScreenDefLoader"/> and, with a catalog, checks it with <see cref="ScreenDefValidator"/>.
/// Errors go to standard output, one line each (<c>file:line: V-xx message</c>); usage and the summary go to standard error.
/// </summary>
internal static class DefLintCommand
{
    public const int Ok = 0;
    public const int Errors = 1;
    public const int Usage = 2;

    private const string Help = "usage: quro-deflint <folder> [--catalog catalog.json]";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        string? folder = null, catalogPath = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--catalog")
            {
                if (i + 1 >= args.Count || catalogPath is not null)
                    return Fail(error, "--catalog needs one file");
                catalogPath = args[++i];
            }
            else if (args[i].StartsWith('-'))
                return Fail(error, $"unknown option {args[i]}");
            else if (folder is not null)
                return Fail(error, "only one folder");
            else
                folder = args[i];
        }
        if (folder is null)
            return Fail(error, "folder missing");
        if (!Directory.Exists(folder))
            return Fail(error, $"folder not found: {folder}");

        ScreenDefValidator? validator = null;
        if (catalogPath is not null)
        {
            if (JsonCatalog.Read(catalogPath, out var problem) is not { } catalog)
                return Fail(error, $"catalog {catalogPath}: {problem}");
            validator = new ScreenDefValidator(catalog);
        }

        // Paths are shown as the folder was given, with '/' so the lines read the same on every OS (CI annotations).
        var root = folder.Replace('\\', '/').TrimEnd('/');
        var files = Directory.EnumerateFiles(folder, "*.screen.toon", SearchOption.AllDirectories)
            .Select(f => (Path: f, Shown: root + "/" + Path.GetRelativePath(folder, f).Replace('\\', '/')))
            .OrderBy(f => f.Shown, StringComparer.Ordinal)
            .ToList();

        var count = 0;
        foreach (var (path, shown) in files)
        {
            var loaded = ScreenDefLoader.Load(shown, File.ReadAllText(path));
            var errors = loaded.Value is { } def && validator is not null ? validator.Validate(def) : loaded.Errors;
            foreach (var e in errors)
                output.WriteLine(e);
            count += errors.Count;
        }

        error.WriteLine($"quro-deflint: {files.Count} file(s), {count} error(s){(validator is null ? ", no catalog" : "")}");
        return count == 0 ? Ok : Errors;
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine($"quro-deflint: {message}");
        error.WriteLine(Help);
        return Usage;
    }

    /// <summary>
    /// Catalog file: a JSON object with the string arrays <c>resourceKeys</c>, <c>codeMajors</c>, <c>lookupIds</c>,
    /// <c>handlers</c> and <c>permissions</c> (all required, design v0.2 chapter 8).
    /// </summary>
    private sealed record JsonCatalog(
        IReadOnlySet<string> ResourceKeys, IReadOnlySet<string> CodeMajors, IReadOnlySet<string> LookupIds,
        IReadOnlySet<string> Handlers, IReadOnlySet<string> Permissions) : IQuroDefinitionCatalog
    {
        public static JsonCatalog? Read(string path, out string problem)
        {
            problem = "";
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var sets = new List<HashSet<string>>();
                foreach (var name in (string[])["resourceKeys", "codeMajors", "lookupIds", "handlers", "permissions"])
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object
                        || !doc.RootElement.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array
                        || array.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
                    {
                        problem = $"\"{name}\" must be an array of strings";
                        return null;
                    }
                    sets.Add([.. array.EnumerateArray().Select(e => e.GetString()!)]);
                }
                return new JsonCatalog(sets[0], sets[1], sets[2], sets[3], sets[4]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                problem = e.Message;
                return null;
            }
        }
    }
}
