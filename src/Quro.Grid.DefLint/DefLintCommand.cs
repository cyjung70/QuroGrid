using System.Text.Json;
using Quro.Grid.Definition;

namespace Quro.Grid.DefLint;

/// <summary>
/// <c>quro-deflint &lt;folder&gt; [--catalog catalog.json]</c> (T-12, GS-D16): loads every <c>*.lookup.toon</c> and
/// <c>*.screen.toon</c> under the folder and, with a catalog, checks them with <see cref="ScreenDefValidator"/>. When the
/// folder has lookups and all of them load, screens are also cross-checked against them (V-35..V-37, A-48).
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

        IQuroDefinitionCatalog? catalog = null;
        if (catalogPath is not null)
        {
            if (JsonCatalog.Read(catalogPath, out var problem) is not { } read)
                return Fail(error, $"catalog {catalogPath}: {problem}");
            catalog = read;
        }

        var errors = new List<(string Shown, IReadOnlyList<DefinitionError> Errors)>();

        // Lookups first (A-48): the screens are cross-checked against the ones that load.
        var lookups = new Dictionary<string, LookupDef>(StringComparer.Ordinal);
        var shownOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var brokenLookups = false;
        var lookupFiles = Files(folder, "*.lookup.toon");
        foreach (var (path, shown) in lookupFiles)
        {
            var loaded = LookupDefLoader.Load(shown, File.ReadAllText(path));
            if (loaded.Value is not { } def)
            {
                brokenLookups = true;
                errors.Add((shown, loaded.Errors));
                continue;
            }
            if (!lookups.TryAdd(def.Id, def))
            {
                errors.Add((shown, [new DefinitionError("V-33", shown, def.Source.Line,
                    $"lookup id '{def.Id}' is also defined in {shownOf[def.Id]}")]));
                continue;
            }
            shownOf[def.Id] = shown;
            if (catalog is not null)
                errors.Add((shown, new ScreenDefValidator(catalog).Validate(def)));
        }

        // A lookup that does not load would make every screen that uses it report V-37, so cross-checks need all of them.
        var crossCheck = catalog is not null && !brokenLookups && lookupFiles.Count > 0;
        var validator = catalog is null ? null
            : crossCheck ? new ScreenDefValidator(catalog, lookups) : new ScreenDefValidator(catalog);

        var screenFiles = Files(folder, "*.screen.toon");
        foreach (var (path, shown) in screenFiles)
        {
            var loaded = ScreenDefLoader.Load(shown, File.ReadAllText(path));
            errors.Add((shown, loaded.Value is { } def && validator is not null ? validator.Validate(def) : loaded.Errors));
        }

        var count = 0;
        foreach (var (_, list) in errors.OrderBy(e => e.Shown, StringComparer.Ordinal))
        {
            foreach (var e in list)
                output.WriteLine(e);
            count += list.Count;
        }

        var notes = catalog is null ? ", no catalog" : brokenLookups ? ", cross-checks skipped (lookup errors)" : "";
        error.WriteLine($"quro-deflint: {screenFiles.Count} screen(s), {lookupFiles.Count} lookup(s), {count} error(s){notes}");
        return count == 0 ? Ok : Errors;
    }

    /// <summary>
    /// Files under the folder, in path order. Paths are shown as the folder was given, with '/' so the lines read the same
    /// on every OS (CI annotations).
    /// </summary>
    private static List<(string Path, string Shown)> Files(string folder, string pattern)
    {
        var root = folder.Replace('\\', '/').TrimEnd('/');
        return Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Select(f => (Path: f, Shown: root + "/" + Path.GetRelativePath(folder, f).Replace('\\', '/')))
            .OrderBy(f => f.Shown, StringComparer.Ordinal)
            .ToList();
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
