using System.Text.RegularExpressions;

namespace Quro.Grid.Definition;

/// <summary>
/// Checks a loaded screen or lookup against the consumer's catalog and the safety rules (design v0.2 chapter 8,
/// V-19..V-32; lookups V-34, Addendum A-48). Created with the lookup definitions, it also checks screens against them
/// (V-35..V-37). Run it after the loaders succeed, typically once at application start.
/// </summary>
/// <param name="catalog">Resource keys, code majors, lookups, handlers and permissions the consumer has registered</param>
public sealed partial class ScreenDefValidator(IQuroDefinitionCatalog catalog)
{
    private readonly IReadOnlyDictionary<string, LookupDef>? _lookups;

    /// <summary>Also checks screens against the lookup definitions: queries and arguments (V-35), rule targets (V-36)
    /// and lookups without a definition (V-37).</summary>
    /// <param name="catalog">Resource keys, code majors, lookups, handlers and permissions the consumer has registered</param>
    /// <param name="lookups">Lookup definitions by id, loaded by <see cref="LookupDefLoader.Load"/></param>
    public ScreenDefValidator(IQuroDefinitionCatalog catalog, IReadOnlyDictionary<string, LookupDef> lookups) : this(catalog) =>
        _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));

    // .claude/rules/clean-room.md: identifiers of reference grids that must not appear as ids. Data for the check, not code.
    private static readonly HashSet<string> ForbiddenIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ed", "edtxt", "txt", "ch", "setColumnEditable", "setColumnSize", "setImagePath", "afterInit",
    };

    [GeneratedRegex(@"^[a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+$")]
    private static partial Regex ResourceKey();

    [GeneratedRegex(@"^[a-z][A-Za-z0-9]*$")]
    private static partial Regex CamelCase();

    [GeneratedRegex(@"\bselect\b.+\bfrom\b|\binsert\s+into\b|\bupdate\b.+\bset\b|\bdelete\s+from\b", RegexOptions.IgnoreCase)]
    private static partial Regex Sql();

    [GeneratedRegex(@"(?<![\w#])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b")]
    private static partial Regex Color();

    // design 4.4: {lookupId}.{query}({args})
    [GeneratedRegex(@"^\s*(?<id>[A-Za-z][A-Za-z0-9]*)\.(?<query>[A-Za-z][A-Za-z0-9]*)\((?<args>[^()]*)\)\s*$")]
    private static partial Regex LookupCall();

    /// <summary>Returns every problem found in a lookup definition (V-34, V-30..V-32), in file order.</summary>
    /// <param name="def">Lookup loaded by <see cref="LookupDefLoader.Load"/></param>
    public IReadOnlyList<DefinitionError> Validate(LookupDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var run = new Run(catalog, def.Source.File);

        run.Id(def.Id, def.LineOf("id"));
        run.Key(def.Title, def.LineOf("title"), "title", "V-34");
        if (def.Perm is { } perm)
            run.Permission(perm, def.LineOf("perm"), "V-34");
        run.Texts(def.LineOf("api"), def.Api);
        foreach (var p in def.Params)
            run.Id(p.Id, p.Source.Line);
        foreach (var q in def.Queries)
            run.Id(q.Id, q.Source.Line);
        foreach (var c in def.Columns)
        {
            run.Id(c.Id, c.Source.Line);
            run.Key(c.Label, c.Source.Line, $"label of '{c.Id}'", "V-34");
            run.Texts(c.Source.Line, c.Fmt);
        }

        run.Errors.Sort((a, b) => a.Line.CompareTo(b.Line));
        return run.Errors;
    }

    /// <summary>The lookup definition for <paramref name="id"/>, or null with V-37 when cross-checking and it is missing.</summary>
    private LookupDef? Definition(Run run, string id, int line)
    {
        if (_lookups is null)
            return null;
        if (_lookups.TryGetValue(id, out var def))
            return def;
        run.Error("V-37", line, $"lookup '{id}' has no definition ({id}.lookup.toon)");
        return null;
    }

    /// <summary>V-35 and V-36 for a lookup rule (lookup design 6, A-48).</summary>
    private void CheckLookupRule(Run run, RuleDef rule)
    {
        if (_lookups is null)
            return;
        var line = rule.Source.Line;
        if (LookupCall().Match(rule.Expr) is not { Success: true } call)
        {
            run.Error("V-35", line, $"lookup rule '{rule.Expr}' must be lookupId.query(arguments)");
            return;
        }
        if (Definition(run, call.Groups["id"].Value, line) is not { } lookup)
            return;

        var args = call.Groups["args"].Value.Trim();
        var count = args.Length == 0 ? 0 : args.Split(',').Length;
        var queryId = call.Groups["query"].Value;
        if (lookup.Queries.FirstOrDefault(q => q.Id == queryId) is not { } query)
            run.Error("V-35", line, $"lookup '{lookup.Id}' has no query '{queryId}'");
        else if (query.Params.Count != count)
            run.Error("V-35", line, $"query '{lookup.Id}.{queryId}' takes {query.Params.Count} argument(s), not {count}");

        foreach (var target in rule.Targets.Where(t => lookup.Columns.All(c => c.Id != t)))
            run.Error("V-36", line, $"lookup '{lookup.Id}' returns no column '{target}' for the target of the same id");
    }

    /// <summary>Returns every problem found, in file order. An empty list means the screen is valid.</summary>
    /// <param name="def">Screen loaded by <see cref="ScreenDefLoader.Load"/></param>
    public IReadOnlyList<DefinitionError> Validate(ScreenDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var run = new Run(catalog, def.Source.File);

        run.Key(def.Title, def.LineOf("title"), "title");
        run.Permission(def.Perm.Read, def.LineOf("perm.read"));
        run.Permission(def.Perm.Write, def.LineOf("perm.write"));
        run.Texts(def.LineOf("api"), def.Api);
        run.Texts(def.LineOf("lockWhen"), def.LockWhen);

        if (def.Form is { } form)
        {
            foreach (var f in form.Fields)
            {
                var line = f.Source.Line;
                run.Id(f.Id, line);
                if (f.Type != FieldType.Hidden || f.Label is not null)
                    run.Key(f.Label, line, $"label of '{f.Id}'");
                if (f.Type is FieldType.Select or FieldType.Radio && f.DataSource is null)
                    run.Error("V-19", line, $"{f.Type} field '{f.Id}' needs a source");
                run.DataSource(f.DataSource, line);
                if (f.DataSource?.StartsWith("lookup:", StringComparison.Ordinal) == true)
                    Definition(run, f.DataSource["lookup:".Length..], line);
                if (f.On is { } handler && !catalog.Handlers.Contains(handler))
                    run.Error("V-23", line, $"handler '{handler}' of '{f.Id}' is not in the catalog");
                run.Texts(line, f.Default, f.On);
            }
            foreach (var r in form.ReadOnlyFor)
                run.Permission(r.Perm, r.Source.Line);
        }

        foreach (var g in def.Grids)
        {
            run.Id(g.Id, g.Source.Line);
            foreach (var c in g.Columns)
            {
                var line = c.Source.Line;
                run.Id(c.Id, line);
                run.Key(c.Label, line, $"label of '{c.Id}'");
                if (c.Type == ColumnType.Select && c.DataSource is null)
                    run.Error("V-19", line, $"select column '{c.Id}' needs a source");
                if (c.Lookup is not null && c.Type != ColumnType.Text)
                    run.Error("V-19", line, $"lookup on '{c.Id}' needs a text column; it is {c.Type}");
                if (c.Lookup is { } lookup && !catalog.LookupIds.Contains(lookup))
                    run.Error("V-22", line, $"lookup '{lookup}' of '{c.Id}' is not in the catalog");
                if (c.Lookup is { } columnLookup)
                    Definition(run, columnLookup, line);
                run.DataSource(c.DataSource, line);
                if (c.DataSource?.StartsWith("lookup:", StringComparison.Ordinal) == true)
                    Definition(run, c.DataSource["lookup:".Length..], line);
                run.Texts(line, c.Fmt, c.Currency);
            }
            foreach (var r in g.Rules)
            {
                if (r.Action == RuleAction.Lookup)
                {
                    // design 4.4: {lookupId}.{query}({args})
                    var id = r.Expr.Split('.', 2)[0];
                    if (!catalog.LookupIds.Contains(id))
                        run.Error("V-22", r.Source.Line, $"lookup '{id}' in rule '{r.On}' is not in the catalog");
                    CheckLookupRule(run, r);
                }
                run.Texts(r.Source.Line, r.On, r.Expr);
            }
            foreach (var v in g.Validate)
            {
                run.Key(v.Msg, v.Source.Line, $"message of '{v.Target}'");
                run.Texts(v.Source.Line, v.Rule);
            }
        }

        run.Errors.Sort((a, b) => a.Line.CompareTo(b.Line));
        return run.Errors;
    }

    private sealed class Run(IQuroDefinitionCatalog catalog, string file)
    {
        public List<DefinitionError> Errors { get; } = [];

        public void Error(string code, int line, string message) => Errors.Add(new DefinitionError(code, file, line, message));

        public void Key(string? key, int line, string what, string code = "V-21")
        {
            if (key is null || !ResourceKey().IsMatch(key))
                Error(code, line, $"{what} '{key}' is not a resource key (for example module.doc.name)");
            else if (!catalog.ResourceKeys.Contains(key))
                Error(code, line, $"{what} '{key}' is not in the catalog");
            Texts(line, key);
        }

        public void Permission(string perm, int line, string code = "V-24")
        {
            if (!catalog.Permissions.Contains(perm))
                Error(code, line, $"permission '{perm}' is not in the catalog");
        }

        public void DataSource(string? source, int line)
        {
            switch (source?.Split(':', 2))
            {
                case null:
                    return;
                case ["code", var major]:
                    if (!catalog.CodeMajors.Contains(major))
                        Error("V-22", line, $"code major '{major}' is not in the catalog");
                    break;
                case ["lookup", var id]:
                    if (!catalog.LookupIds.Contains(id))
                        Error("V-22", line, $"lookup '{id}' is not in the catalog");
                    break;
                default:
                    Error("V-22", line, $"source '{source}' must be code:{{major}} or lookup:{{id}}");
                    break;
            }
            Texts(line, source);
        }

        public void Id(string id, int line)
        {
            if (ForbiddenIds.Contains(id))
                Error("V-31", line, $"id '{id}' is a reference grid identifier (clean-room rule)");
            else if (!CamelCase().IsMatch(id))
                Error("V-31", line, $"id '{id}' must be camelCase");
        }

        /// <summary>SQL statements (V-30) and color values (V-32) are not allowed in any value.</summary>
        public void Texts(int line, params string?[] values)
        {
            foreach (var value in values)
            {
                if (value is null)
                    continue;
                if (Sql().IsMatch(value))
                    Error("V-30", line, $"value '{value}' looks like SQL; definitions reference APIs and lookups, not queries");
                if (Color().IsMatch(value))
                    Error("V-32", line, $"value '{value}' is a color; styles come from edit and req, not from colors");
            }
        }
    }
}
