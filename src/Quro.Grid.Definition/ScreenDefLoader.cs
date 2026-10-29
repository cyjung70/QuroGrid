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
internal sealed class ScreenBuilder(string file, List<DefinitionError> errors) : DefinitionBuilder(file, errors)
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
}
