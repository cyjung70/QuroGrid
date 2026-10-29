using Quro.Grid.Definition.Toon;

namespace Quro.Grid.Definition;

/// <summary>Loads <c>*.lookup.toon</c> files (Addendum A-48, lookup design v0.3 chapters 4 and 6).</summary>
public static class LookupDefLoader
{
    /// <summary>
    /// Parses <paramref name="text"/> in strict mode, builds the lookup definition and checks its structure: V-01..V-03
    /// as for screens, V-33 for the id and file name, references to columns and arguments, and duplicate ids. All errors
    /// are returned together; <see cref="DefinitionResult{T}.Value"/> is null when there is any error. Structure checks run
    /// only when the file has no syntax, key or value errors.
    /// </summary>
    /// <param name="path">File path used in errors and <see cref="SourceInfo"/></param>
    /// <param name="text">File content</param>
    public static DefinitionResult<LookupDef> Load(string path, string text)
    {
        var errors = new List<DefinitionError>();
        var root = ToonReader.Read(path, text, errors);
        var lookup = new LookupBuilder(path, errors).Build(root);
        if (lookup is not null && errors.Count == 0)
            Check(path, lookup, errors);
        errors.Sort((a, b) => a.Line.CompareTo(b.Line));
        return new DefinitionResult<LookupDef>(lookup, errors);
    }

    private static void Check(string file, LookupDef def, List<DefinitionError> errors)
    {
        void Error(int line, string message) => errors.Add(new DefinitionError("V-33", file, line, message));

        if (!string.Equals(Path.GetFileName(file), def.Id + ".lookup.toon", StringComparison.Ordinal))
            Error(def.LineOf("id"), $"lookup id '{def.Id}' does not match the file name; expected {def.Id}.lookup.toon");

        var columns = def.Columns.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        if (!columns.Contains(def.Key))
            Error(def.LineOf("key"), $"key '{def.Key}' is not a column");
        if (def.Text is { } textColumn && !columns.Contains(textColumn))
            Error(def.LineOf("text"), $"text '{textColumn}' is not a column");

        var parameters = def.Params.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var q in def.Queries)
            foreach (var p in q.Params.Where(p => !parameters.Contains(p)))
                Error(q.Source.Line, $"query '{q.Id}' uses '{p}', which is not in params");

        Duplicates(def.Params.Select(p => (p.Id, p.Source.Line)), "param");
        Duplicates(def.Queries.Select(q => (q.Id, q.Source.Line)), "query");
        Duplicates(def.Columns.Select(c => (c.Id, c.Source.Line)), "column");

        void Duplicates(IEnumerable<(string Id, int Line)> items, string what)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, line) in items)
                if (!seen.Add(id))
                    Error(line, $"{what} id '{id}' is used more than once");
        }
    }
}

/// <summary>Maps the TOON tree onto <see cref="LookupDef"/>. Unknown or missing keys are V-02, bad values V-03.</summary>
internal sealed class LookupBuilder(string file, List<DefinitionError> errors) : DefinitionBuilder(file, errors)
{
    private static readonly Dictionary<string, LookupParamType> ParamTypes = new()
    {
        ["text"] = LookupParamType.Text, ["number"] = LookupParamType.Number, ["date"] = LookupParamType.Date,
    };

    private static readonly Dictionary<string, ColumnType> ColumnTypes = new()
    {
        ["text"] = ColumnType.Text, ["number"] = ColumnType.Number, ["money"] = ColumnType.Money,
        ["date"] = ColumnType.Date, ["checkbox"] = ColumnType.Checkbox,
    };

    public LookupDef? Build(ToonObject root)
    {
        var top = Entries(root, "file", required: ["lookup", "queries", "columns"], optional: ["params"]);
        if (!top.TryGetValue("lookup", out var lookupEntry) || Obj(lookupEntry) is not { } lookupObj)
            return null;

        var l = Entries(lookupObj, "lookup", required: ["id", "title", "version", "api", "key"],
            optional: ["text", "minChars", "perm"]);
        var lines = l.ToDictionary(e => e.Key, e => e.Value.Line);

        var version = Int(l, "version") ?? 1;
        if (version < 1)
            Error("V-03", lines["version"], $"'version' must be 1 or more; it is {version}");
        var minChars = Int(l, "minChars") ?? 1;
        if (minChars < 0)
            Error("V-03", lines["minChars"], $"'minChars' must be 0 or more; it is {minChars}");
        var api = Text(l, "api") ?? "";
        if (l.ContainsKey("api") && !api.StartsWith('/'))
            Error("V-03", lines["api"], $"'api' must be a path that starts with '/'; it is '{api}'");

        var parameters = new List<LookupParamDef>();
        if (top.TryGetValue("params", out var pe) && Table(pe, required: ["id", "type"], optional: ["req"]) is { } pt)
            foreach (var r in pt)
                parameters.Add(new LookupParamDef(Cell(r, "id") ?? "", CellEnum(r, "type", ParamTypes) ?? LookupParamType.Text,
                    CellBool(r, "req") ?? false, At(r.Line)));

        var queries = new List<LookupQueryDef>();
        if (top.TryGetValue("queries", out var qe) && Table(qe, required: ["id"], optional: ["params"]) is { } qt)
            foreach (var r in qt)
                queries.Add(new LookupQueryDef(Cell(r, "id") ?? "", Ids(Cell(r, "params")), At(r.Line)));

        var columns = new List<LookupColumnDef>();
        if (top.TryGetValue("columns", out var ce) && Table(ce, required: ["id", "label", "type"], optional: ["width", "fmt"]) is { } ct)
            foreach (var r in ct)
            {
                var width = CellInt(r, "width") ?? 120;
                if (width <= 0)
                    Error("V-03", r.Line, $"'width' must be more than 0; it is {width}");
                columns.Add(new LookupColumnDef(Cell(r, "id") ?? "", Cell(r, "label") ?? "",
                    CellEnum(r, "type", ColumnTypes) ?? ColumnType.Text, width, Cell(r, "fmt"), At(r.Line)));
            }

        return new LookupDef(Text(l, "id") ?? "", Text(l, "title") ?? "", version, api, Text(l, "key") ?? "",
            Text(l, "text"), minChars, Text(l, "perm"), parameters, queries, columns, At(lookupEntry.Line)) { Lines = lines };
    }
}
