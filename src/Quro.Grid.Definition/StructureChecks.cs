using System.Text.RegularExpressions;

namespace Quro.Grid.Definition;

/// <summary>
/// Checks that need only the definition itself (design v0.2 chapter 8, V-03 ranges and V-04..V-18).
/// Catalog checks (V-19..V-32) are <c>ScreenDefValidator</c>'s job.
/// </summary>
internal sealed partial class StructureChecks(string file, List<DefinitionError> errors)
{
    private static readonly HashSet<ToolbarCommand> GridCommands =
    [
        ToolbarCommand.AddRow, ToolbarCommand.DeleteRow, ToolbarCommand.UndoRow,
        ToolbarCommand.UploadCsv, ToolbarCommand.ExportXlsx, ToolbarCommand.ExportCsv,
    ];

    private static readonly HashSet<FieldType> TextTypes = [FieldType.Text, FieldType.Textarea, FieldType.Password];

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9]*(?!\s*\()(?![A-Za-z0-9])")]
    private static partial Regex Identifier();

    // Design 5.5: @today, @now, @company.{key}, @setting:{key}, @user.{key}. Anything else starting with '@' is a typo.
    [GeneratedRegex(@"^@(?:today|now|company\.[A-Za-z][A-Za-z0-9]*|user\.[A-Za-z][A-Za-z0-9]*|setting:[A-Za-z][A-Za-z0-9.]*)$")]
    private static partial Regex DefaultToken();

    private void Error(string code, int line, string message) => errors.Add(new DefinitionError(code, file, line, message));

    public void Run(ScreenDef def)
    {
        var fields = def.Form?.Fields ?? [];
        var fieldById = fields.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());

        if (!string.Equals(Path.GetFileName(file), def.Id + ".screen.toon", StringComparison.Ordinal))
            Error("V-05", def.LineOf("id"), $"screen id '{def.Id}' does not match the file name; expected {def.Id}.screen.toon");
        if (!fieldById.ContainsKey(def.Key))
            Error("V-10", def.LineOf("key"), $"key '{def.Key}' is not a form field");
        if (def.Grids.Count == 0 && def.Toolbar.FirstOrDefault(GridCommands.Contains) is var cmd && GridCommands.Contains(cmd))
            Error("V-14", def.LineOf("toolbar"), $"toolbar command '{cmd}' needs a grid");

        CheckUniqueIds(def);
        if (def.Form is { } form)
            foreach (var f in form.Fields)
                CheckField(f, form.Cols);
        foreach (var grid in def.Grids)
            CheckGrid(grid, fieldById);
    }

    private void CheckUniqueIds(ScreenDef def)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string id, SourceInfo at)
        {
            if (id.Length > 0 && !seen.Add(id))
                Error("V-04", at.Line, $"id '{id}' is used more than once (form fields, grids and columns share one namespace)");
        }
        // File order: form, then each grid followed by its columns.
        foreach (var f in def.Form?.Fields ?? [])
            Add(f.Id, f.Source);
        foreach (var g in def.Grids)
        {
            Add(g.Id, g.Source);
            foreach (var c in g.Columns)
                Add(c.Id, c.Source);
        }
    }

    private void CheckField(FieldDef f, int cols)
    {
        if (f.Max is not null && !TextTypes.Contains(f.Type))
            Error("V-11", f.Source.Line, $"'max' applies only to text, textarea and password fields; '{f.Id}' is {f.Type}");
        if (f.Span < 0 || f.Span > cols)
            Error("V-03", f.Source.Line, $"span {f.Span} of '{f.Id}' must be between 0 and cols ({cols})");
        else if (f.Span == 0 && f.Type != FieldType.Hidden)
            Error("V-03", f.Source.Line, $"span 0 is only for hidden fields; '{f.Id}' is {f.Type}");
        if (f.Default is ['@', ..] && !DefaultToken().IsMatch(f.Default))
            Error("V-03", f.Source.Line, $"default '{f.Default}' of '{f.Id}' is not a known token (@today, @now, @company.x, @setting:x, @user.x)");
    }

    private void CheckGrid(GridDef grid, Dictionary<string, FieldDef> fieldById)
    {
        var columnById = grid.Columns.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());

        foreach (var c in grid.Columns)
        {
            if (c.Req && c.Edit == ColumnEdit.ReadOnly)
                Error("V-12", c.Source.Line, $"column '{c.Id}' is required but not editable");
            if (c.Sum && c.Type is not (ColumnType.Number or ColumnType.Money))
                Error("V-13", c.Source.Line, $"column '{c.Id}' has sum but is {c.Type}; only number and money can be summed");
        }

        var calcInputs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var rule in grid.Rules)
        {
            switch (rule.Action)
            {
                case RuleAction.Calc:
                    var inputs = InputsOf(rule.Expr);
                    foreach (var target in rule.Targets)
                    {
                        if (!columnById.TryGetValue(target, out var col))
                            Error("V-15", rule.Source.Line, $"calc target '{target}' is not a column of grid '{grid.Id}'");
                        else if (col.Edit != ColumnEdit.ReadOnly)
                            Error("V-15", rule.Source.Line, $"calc target '{target}' must have edit=false");
                        if (!calcInputs.TryGetValue(target, out var set))
                            calcInputs[target] = set = new HashSet<string>(StringComparer.Ordinal);
                        set.UnionWith(inputs);
                    }
                    foreach (var input in inputs.Where(i => !columnById.ContainsKey(i)))
                        Error("V-15", rule.Source.Line, $"calc input '{input}' is not a column of grid '{grid.Id}'");
                    break;
                case RuleAction.Lookup:
                    foreach (var target in rule.Targets.Where(t => !columnById.ContainsKey(t)))
                        Error("V-15", rule.Source.Line, $"lookup target '{target}' is not a column of grid '{grid.Id}'");
                    break;
                case RuleAction.Sum:
                    foreach (var target in rule.Targets)
                        if (!fieldById.TryGetValue(target, out var field) || field.Mode != FieldMode.Ro)
                            Error("V-16", rule.Source.Line, $"sum target '{target}' must be a form field with mode=ro");
                    break;
                case RuleAction.Set:
                    foreach (var target in rule.Targets.Where(t => !columnById.ContainsKey(t) && !fieldById.ContainsKey(t)))
                        Error("V-15", rule.Source.Line, $"set target '{target}' is not a column or form field");
                    break;
            }
        }

        foreach (var rule in grid.Rules.Where(r => r.Action == RuleAction.Calc))
            if (rule.Targets.Any(t => DependsOn(t, t, calcInputs)))
                Error("V-17", rule.Source.Line, $"calc rule for '{string.Join("|", rule.Targets)}' is part of a cycle");

        foreach (var v in grid.Validate)
        {
            var parts = v.Rule.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts is ["requires", var field] && !fieldById.ContainsKey(field))
                Error("V-18", v.Source.Line, $"'requires {field}': '{field}' is not a form field");
        }
        // lockWhen fields (also V-18) are checked with LockExpression in chunk D-4.
    }

    /// <summary>True when <paramref name="column"/> is computed, directly or through other calc rules, from <paramref name="target"/>.</summary>
    private static bool DependsOn(string column, string target, Dictionary<string, HashSet<string>> calcInputs)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>([column]);
        while (stack.TryPop(out var current))
        {
            if (!calcInputs.TryGetValue(current, out var inputs))
                continue;
            foreach (var input in inputs)
            {
                if (input == target)
                    return true;
                if (visited.Add(input))
                    stack.Push(input);
            }
        }
        return false;
    }

    // Identifiers of a rule expression that are not function names. Replaced by the RuleExpression parser in chunk D-4.
    private static HashSet<string> InputsOf(string expr) =>
        Identifier().Matches(expr).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
}
