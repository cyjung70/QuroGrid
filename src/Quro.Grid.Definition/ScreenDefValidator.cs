using System.Text.RegularExpressions;

namespace Quro.Grid.Definition;

/// <summary>
/// Checks a loaded screen against the consumer's catalog and the safety rules (design v0.2 chapter 8, V-19..V-32).
/// Run it after <see cref="ScreenDefLoader.Load"/> succeeds, typically once at application start.
/// </summary>
/// <param name="catalog">Resource keys, code majors, lookups, handlers and permissions the consumer has registered</param>
public sealed partial class ScreenDefValidator(IQuroDefinitionCatalog catalog)
{
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
                run.DataSource(c.DataSource, line);
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

        public void Key(string? key, int line, string what)
        {
            if (key is null || !ResourceKey().IsMatch(key))
                Error("V-21", line, $"{what} '{key}' is not a resource key (for example module.doc.name)");
            else if (!catalog.ResourceKeys.Contains(key))
                Error("V-21", line, $"{what} '{key}' is not in the catalog");
            Texts(line, key);
        }

        public void Permission(string perm, int line)
        {
            if (!catalog.Permissions.Contains(perm))
                Error("V-24", line, $"permission '{perm}' is not in the catalog");
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
