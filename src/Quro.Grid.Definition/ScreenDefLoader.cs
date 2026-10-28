using System.Globalization;
using Quro.Grid.Definition.Toon;

namespace Quro.Grid.Definition;

/// <summary>Loads <c>*.screen.toon</c> files (design v0.2 chapters 3 and 4).</summary>
public static class ScreenDefLoader
{
    /// <summary>
    /// Parses <paramref name="text"/> in strict mode, builds the screen definition and checks its structure
    /// (V-01..V-18). All errors are collected and returned together; <see cref="DefinitionResult{T}.Value"/> is null
    /// when there is any error. Structure checks run only when the file has no syntax, key or value errors.
    /// </summary>
    /// <param name="path">File path used in errors and <see cref="SourceInfo"/></param>
    /// <param name="text">File content</param>
    public static DefinitionResult<ScreenDef> Load(string path, string text)
    {
        var errors = new List<DefinitionError>();
        var root = ToonReader.Read(path, text, errors);
        var builder = new ScreenBuilder(path, errors);
        var screen = builder.Build(root);
        // Structure checks only on a cleanly read file: dropped rows or unknown keys would cause follow-up errors.
        if (screen is not null && errors.Count == 0)
            new StructureChecks(path, errors).Run(screen);
        errors.Sort((a, b) => a.Line.CompareTo(b.Line));
        return new DefinitionResult<ScreenDef>(screen, errors);
    }
}

/// <summary>Maps the TOON tree onto <see cref="ScreenDef"/>. Unknown or missing keys are V-02, bad values V-03.</summary>
internal sealed class ScreenBuilder(string file, List<DefinitionError> errors)
{
    private static readonly Dictionary<string, ScreenLayout> Layouts = new()
    {
        ["form-grid"] = ScreenLayout.FormGrid, ["form"] = ScreenLayout.Form, ["grid"] = ScreenLayout.Grid,
    };

    private static readonly Dictionary<string, ToolbarCommand> Commands = new()
    {
        ["query"] = ToolbarCommand.Query, ["new"] = ToolbarCommand.New, ["save"] = ToolbarCommand.Save,
        ["delete"] = ToolbarCommand.Delete, ["addRow"] = ToolbarCommand.AddRow, ["deleteRow"] = ToolbarCommand.DeleteRow,
        ["undoRow"] = ToolbarCommand.UndoRow, ["uploadCsv"] = ToolbarCommand.UploadCsv,
        ["exportXlsx"] = ToolbarCommand.ExportXlsx, ["exportCsv"] = ToolbarCommand.ExportCsv, ["print"] = ToolbarCommand.Print,
    };

    private static readonly Dictionary<string, FieldType> FieldTypes = new()
    {
        ["text"] = FieldType.Text, ["textarea"] = FieldType.Textarea, ["password"] = FieldType.Password,
        ["select"] = FieldType.Select, ["radio"] = FieldType.Radio, ["checkbox"] = FieldType.Checkbox,
        ["date"] = FieldType.Date, ["number"] = FieldType.Number, ["money"] = FieldType.Money,
        ["money4"] = FieldType.Money4, ["hidden"] = FieldType.Hidden,
    };

    private static readonly Dictionary<string, FieldMode> Modes = new()
    {
        ["opt"] = FieldMode.Opt, ["req"] = FieldMode.Req, ["key"] = FieldMode.Key, ["newOnly"] = FieldMode.NewOnly,
        ["ro"] = FieldMode.Ro, ["auto"] = FieldMode.Auto, ["hidden"] = FieldMode.Hidden,
    };

    private static readonly Dictionary<string, CaseRule> Cases = new()
    {
        ["upper"] = CaseRule.Upper, ["lower"] = CaseRule.Lower,
    };

    private static readonly Dictionary<string, ColumnType> ColumnTypes = new()
    {
        ["text"] = ColumnType.Text, ["number"] = ColumnType.Number, ["money"] = ColumnType.Money,
        ["date"] = ColumnType.Date, ["select"] = ColumnType.Select, ["checkbox"] = ColumnType.Checkbox,
        ["button"] = ColumnType.Button,
    };

    private static readonly Dictionary<string, ColumnAlign> Aligns = new()
    {
        ["left"] = ColumnAlign.Left, ["center"] = ColumnAlign.Center, ["right"] = ColumnAlign.Right,
    };

    private static readonly Dictionary<string, RuleAction> Actions = new()
    {
        ["calc"] = RuleAction.Calc, ["sum"] = RuleAction.Sum, ["lookup"] = RuleAction.Lookup, ["set"] = RuleAction.Set,
    };

    private static readonly Dictionary<string, DeletedRowsMode> DeletedModes = new()
    {
        ["hide"] = DeletedRowsMode.Hide, ["strike"] = DeletedRowsMode.Strike,
    };

    private void Error(string code, int line, string message) => errors.Add(new DefinitionError(code, file, line, message));

    private SourceInfo At(int line) => new(file, line);

    public ScreenDef? Build(ToonObject root)
    {
        var top = Entries(root, "file", required: ["screen"], optional: ["toolbar", "form", "grids"]);

        if (!top.TryGetValue("screen", out var screenEntry) || Obj(screenEntry) is not { } screenObj)
            return null;

        var s = Entries(screenObj, "screen",
            required: ["id", "module", "title", "version", "api", "key", "layout", "perm"], optional: ["lockWhen"]);
        var lines = new Dictionary<string, int>();
        foreach (var (key, entry) in s.Concat(top))
            lines.TryAdd(key, entry.Line);
        var perm = s.TryGetValue("perm", out var permEntry) && Obj(permEntry) is { } permObj
            ? BuildPerm(permObj, lines)
            : new PermDef("", "");

        var toolbar = top.TryGetValue("toolbar", out var tb) ? BuildToolbar(tb) : [];
        var form = top.TryGetValue("form", out var formEntry) && Obj(formEntry) is { } formObj ? BuildForm(formObj) : null;
        var grids = top.TryGetValue("grids", out var gridsEntry) ? BuildGrids(gridsEntry) : [];

        return new ScreenDef(
            Text(s, "id") ?? "", Text(s, "module") ?? "", Text(s, "title") ?? "", Int(s, "version") ?? 0,
            Text(s, "api") ?? "", Text(s, "key") ?? "",
            Enum(s, "layout", Layouts) ?? ScreenLayout.FormGrid, perm, Text(s, "lockWhen"),
            toolbar, form, grids, At(screenEntry.Line)) { Lines = lines };
    }

    private PermDef BuildPerm(ToonObject obj, Dictionary<string, int> lines)
    {
        var p = Entries(obj, "perm", required: ["read", "write"], optional: []);
        foreach (var (key, entry) in p)
            lines["perm." + key] = entry.Line;
        return new PermDef(Text(p, "read") ?? "", Text(p, "write") ?? "");
    }

    private List<ToolbarCommand> BuildToolbar(ToonEntry entry)
    {
        if (entry.Value is not ToonArray { Items: var items })
        {
            Error("V-02", entry.Line, "'toolbar' must be a list: toolbar[N]: a,b,c");
            return [];
        }
        var commands = new List<ToolbarCommand>();
        foreach (var item in items)
            if (EnumOf(item as ToonScalar, "toolbar", Commands) is { } c)
                commands.Add(c);
        return commands;
    }

    private FormDef BuildForm(ToonObject obj)
    {
        var f = Entries(obj, "form", required: ["cols", "fields"], optional: ["readOnlyFor"]);
        var fields = new List<FieldDef>();
        if (f.TryGetValue("fields", out var fe) && Table(fe, required: ["id", "label", "type", "mode"],
                optional: ["max", "case", "default", "source", "span", "on"]) is { } table)
        {
            foreach (var r in table)
                fields.Add(new FieldDef(
                    Cell(r, "id") ?? "", Cell(r, "label"),
                    CellEnum(r, "type", FieldTypes) ?? FieldType.Text, CellEnum(r, "mode", Modes) ?? FieldMode.Opt,
                    CellInt(r, "max"), CellEnum(r, "case", Cases) ?? CaseRule.None,
                    Cell(r, "default"), Cell(r, "source"), CellInt(r, "span") ?? 1, Cell(r, "on"), At(r.Line)));
        }

        var readOnly = new List<ReadOnlyRule>();
        if (f.TryGetValue("readOnlyFor", out var re) && Table(re, required: ["perm", "fields"], optional: []) is { } ro)
            foreach (var r in ro)
                readOnly.Add(new ReadOnlyRule(Cell(r, "perm") ?? "", Ids(Cell(r, "fields")), At(r.Line)));

        return new FormDef(Int(f, "cols") ?? 1, fields, readOnly);
    }

    private List<GridDef> BuildGrids(ToonEntry entry)
    {
        if (entry.Value is not ToonArray { Items: var items })
        {
            Error("V-02", entry.Line, "'grids' must be a list: grids[N]: followed by '- id: ...' items");
            return [];
        }

        var grids = new List<GridDef>();
        foreach (var item in items)
        {
            if (item is not ToonObject obj)
            {
                Error("V-02", item.Line, "each grid must be an object");
                continue;
            }
            var g = Entries(obj, "grid", required: ["id", "bind", "rowKey", "columns"],
                optional: ["initRows", "clipboard", "pageSize", "frozen", "deletedRows", "rules", "validate"]);

            var columns = new List<ColumnDef>();
            if (g.TryGetValue("columns", out var ce) && Table(ce, required: ["id", "label", "type"],
                    optional: ["width", "edit", "req", "fmt", "lookup", "sum", "hidden", "align", "min", "max", "source",
                        "currency", "filter", "sort", "group"]) is { } ct)
            {
                foreach (var r in ct)
                    columns.Add(new ColumnDef(
                        Cell(r, "id") ?? "", Cell(r, "label") ?? "", CellEnum(r, "type", ColumnTypes) ?? ColumnType.Text,
                        CellInt(r, "width") ?? 120, CellEdit(r), CellBool(r, "req") ?? false, Cell(r, "fmt"),
                        Cell(r, "lookup"), CellBool(r, "sum") ?? false, CellBool(r, "hidden") ?? false, At(r.Line))
                    {
                        Align = CellEnum(r, "align", Aligns),
                        Min = CellDecimal(r, "min"),
                        Max = CellDecimal(r, "max"),
                        DataSource = Cell(r, "source"),
                        Currency = Cell(r, "currency"),
                        Filter = CellBool(r, "filter") ?? true,
                        Sort = CellBool(r, "sort") ?? true,
                        Group = CellBool(r, "group") ?? false,
                    });
            }

            var rules = new List<RuleDef>();
            if (g.TryGetValue("rules", out var rle) && Table(rle, required: ["on", "action", "target", "expr"], optional: []) is { } rt)
                foreach (var r in rt)
                    rules.Add(new RuleDef(Cell(r, "on") ?? "", CellEnum(r, "action", Actions) ?? RuleAction.Calc,
                        Ids(Cell(r, "target")), Cell(r, "expr") ?? "", At(r.Line)));

            var validate = new List<ValidateDef>();
            if (g.TryGetValue("validate", out var ve) && Table(ve, required: ["target", "rule", "msg"], optional: []) is { } vt)
                foreach (var r in vt)
                    validate.Add(new ValidateDef(Cell(r, "target") ?? "", Cell(r, "rule") ?? "", Cell(r, "msg") ?? "", At(r.Line)));

            grids.Add(new GridDef(
                Text(g, "id") ?? "", Text(g, "bind") ?? "", Text(g, "rowKey") ?? "",
                Int(g, "initRows") ?? 0, Bool(g, "clipboard") ?? true, Int(g, "pageSize") ?? 0, Int(g, "frozen") ?? 0,
                Enum(g, "deletedRows", DeletedModes) ?? DeletedRowsMode.Hide, columns, rules, validate, At(obj.Line)));
        }
        return grids;
    }

    // ── object entries ──

    private Dictionary<string, ToonEntry> Entries(ToonObject obj, string section, string[] required, string[] optional)
    {
        var map = new Dictionary<string, ToonEntry>();
        foreach (var e in obj.Entries)
        {
            if (!required.Contains(e.Key) && !optional.Contains(e.Key))
                Error("V-02", e.Line, $"unknown key '{e.Key}' in {section}");
            else if (!map.TryAdd(e.Key, e))
                Error("V-02", e.Line, $"duplicate key '{e.Key}' in {section}");
        }
        foreach (var key in required.Where(k => !map.ContainsKey(k)))
            Error("V-02", obj.Line, $"missing key '{key}' in {section}");
        return map;
    }

    private ToonObject? Obj(ToonEntry entry)
    {
        if (entry.Value is ToonObject o)
            return o;
        Error("V-02", entry.Line, $"'{entry.Key}' must be a nested object");
        return null;
    }

    private string? Text(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsText(Scalar(e)) : null;

    private int? Int(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsInt(Scalar(e), key) : null;

    private bool? Bool(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsBool(Scalar(e), key) : null;

    private T? Enum<T>(Dictionary<string, ToonEntry> map, string key, Dictionary<string, T> values) where T : struct =>
        map.TryGetValue(key, out var e) ? EnumOf(Scalar(e), key, values) : null;

    private ToonScalar? Scalar(ToonEntry entry)
    {
        if (entry.Value is ToonScalar s)
            return s;
        Error("V-02", entry.Line, $"'{entry.Key}' must be a single value");
        return null;
    }

    // ── tables ──

    private sealed record Row(Dictionary<string, ToonScalar> Cells, int Line);

    private List<Row>? Table(ToonEntry entry, string[] required, string[] optional)
    {
        if (entry.Value is not ToonTable table)
        {
            Error("V-02", entry.Line, $"'{entry.Key}' must be a table: {entry.Key}[N]{{{string.Join(",", required)},...}}:");
            return null;
        }
        foreach (var f in table.Fields.Where(f => !required.Contains(f) && !optional.Contains(f)))
            Error("V-02", table.Line, $"unknown field '{f}' in {entry.Key}");
        foreach (var f in required.Where(f => !table.Fields.Contains(f)))
            Error("V-02", table.Line, $"missing field '{f}' in {entry.Key}");
        if (table.Fields.Distinct().Count() != table.Fields.Count)
            Error("V-02", table.Line, $"duplicate field in {entry.Key} header");

        return table.Rows
            .Select(r => new Row(table.Fields.Zip(r.Cells).DistinctBy(p => p.First).ToDictionary(p => p.First, p => p.Second), r.Line))
            .ToList();
    }

    private string? Cell(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsText(s) : null;

    private int? CellInt(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsInt(s, field) : null;

    private bool? CellBool(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsBool(s, field) : null;

    private decimal? CellDecimal(Row row, string field)
    {
        if (!row.Cells.TryGetValue(field, out var s) || IsEmpty(s))
            return null;
        if (s is { Value: decimal d, Quoted: false })
            return d;
        Error("V-03", s.Line, $"'{field}' must be a number");
        return null;
    }

    private T? CellEnum<T>(Row row, string field, Dictionary<string, T> values) where T : struct =>
        row.Cells.TryGetValue(field, out var s) ? EnumOf(s, field, values) : null;

    private ColumnEdit CellEdit(Row row)
    {
        if (!row.Cells.TryGetValue("edit", out var s) || IsEmpty(s))
            return ColumnEdit.Editable;
        return s switch
        {
            { Value: true, Quoted: false } => ColumnEdit.Editable,
            { Value: false, Quoted: false } => ColumnEdit.ReadOnly,
            { Value: "new" } => ColumnEdit.NewRowOnly,
            _ => Invalid(s, "edit", "true, false, new", ColumnEdit.Editable),
        };
    }

    // ── scalar conversion ──

    private static bool IsEmpty(ToonScalar? s) => s is null || s.Value is null || s.Value is "";

    private static string? AsText(ToonScalar? s) => IsEmpty(s)
        ? null
        : s!.Value switch
        {
            string str => str,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => null,
        };

    private int? AsInt(ToonScalar? s, string key)
    {
        if (IsEmpty(s))
            return null;
        if (s is { Value: decimal d, Quoted: false } && d == decimal.Truncate(d) && d is >= int.MinValue and <= int.MaxValue)
            return (int)d;
        Error("V-03", s!.Line, $"'{key}' must be a whole number");
        return null;
    }

    private bool? AsBool(ToonScalar? s, string key)
    {
        if (IsEmpty(s))
            return null;
        if (s is { Value: bool b, Quoted: false })
            return b;
        Error("V-03", s!.Line, $"'{key}' must be true or false");
        return null;
    }

    private T? EnumOf<T>(ToonScalar? s, string key, Dictionary<string, T> values) where T : struct
    {
        if (IsEmpty(s))
            return null;
        if (s!.Value is string str && values.TryGetValue(str, out var v))
            return v;
        return Invalid<T?>(s, key, string.Join(", ", values.Keys), null);
    }

    private T Invalid<T>(ToonScalar s, string key, string allowed, T fallback)
    {
        Error("V-03", s.Line, $"'{key}' value '{AsText(s)}' is not one of: {allowed}");
        return fallback;
    }

    private static List<string> Ids(string? list) =>
        string.IsNullOrEmpty(list) ? [] : [.. list.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
}
