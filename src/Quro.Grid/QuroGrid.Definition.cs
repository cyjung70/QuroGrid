using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quro.Grid.Data;
using Quro.Grid.Definition;

namespace Quro.Grid;

// Screen definition connection (T-11, design v0.2 7.1·7.2) and copy/paste switch (G-03).
public partial class QuroGrid<TItem>
{
    private Dictionary<string, FieldRule> _definitionRules = [];
    private GridDef? _appliedDefinition;
    private int _pendingInitRows;
    private (GridDef? Def, object? LookupFor, object? OptionsFor) _sourcesFor;
    private Dictionary<string, Func<string, Task<IReadOnlyList<string>>>> _definitionLookups = [];
    private Dictionary<string, IReadOnlyList<ComboOption>?> _definitionOptions = [];

    /// <summary>Copy and paste allowed (G-03). Default true. A <see cref="Definition"/> sets it from <c>clipboard</c>.</summary>
    [Parameter] public bool Clipboard { get; set; } = true;

    /// <summary>
    /// Grid from a screen definition (<c>&lt;QuroGrid Definition="@gridDef"&gt;</c>, T-11). Creates the columns and sets
    /// <see cref="FrozenColumns"/>, <see cref="DeletedRows"/>, <see cref="Clipboard"/>, <see cref="ShowFooter"/> (when a
    /// column has <c>sum</c>) and <see cref="PageSize"/> (when <c>pageSize</c> &gt; 0); these definition values win over the
    /// parameters. <c>validate</c> rows <c>gt ge lt le ne</c> become data set rules with the row's <c>msg</c> key.
    /// <c>initRows</c> adds empty rows once when the data set is empty. Rules and lookups stay with the app (A-44).
    /// Parse the definition once and share it (R6).
    /// </summary>
    [Parameter] public GridDef? Definition { get; set; }

    /// <summary>
    /// Code search for <see cref="Definition"/> columns with <c>lookup</c>: (lookup id, typed text) → matching codes, as
    /// <see cref="QuroColumn.Lookup"/> (A-16). The grid builds one function per column when the definition or this
    /// parameter changes.
    /// </summary>
    [Parameter] public Func<string, string, Task<IReadOnlyList<string>>>? LookupFor { get; set; }

    /// <summary>
    /// List for <see cref="Definition"/> select columns: the column's source (for example <c>code:S910</c>) → options,
    /// as <see cref="QuroColumn.Options"/>. Asked once per column when the definition or this parameter changes; pass a
    /// new function to reload the lists.
    /// </summary>
    [Parameter] public Func<string, IReadOnlyList<ComboOption>?>? OptionsFor { get; set; }

    private void ApplyDefinition()
    {
        if (Definition is not { } def)
            return;

        FrozenColumns = def.Frozen;
        DeletedRows = def.DeletedRows == DeletedRowsMode.Strike ? DeletedRowDisplay.Strikethrough : DeletedRowDisplay.Hide;
        Clipboard = def.Clipboard;
        ShowFooter = ShowFooter || def.Columns.Any(c => c.Sum && !c.Hidden);
        if (def.PageSize > 0)
            PageSize = def.PageSize;
        UpdateSources(def);

        if (ReferenceEquals(def, _appliedDefinition))
            return;
        var first = _appliedDefinition is null;
        _appliedDefinition = def;
        _definitionRules = RulesOf(def);
        foreach (var (field, rule) in _definitionRules)
            DataSet.SetRule(field, rule);   // hidden columns included
        SupplementColumns(def);
        if (first && def.InitRows > 0 && DataSet.RowKeys.Count == 0)
            _pendingInitRows = def.InitRows;
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_pendingInitRows == 0)
            return;
        var count = _pendingInitRows;
        _pendingInitRows = 0;
        _batch++;   // one render for all rows (R3)
        try
        {
            for (var i = 0; i < count; i++)
                await AddRowCore(blank: true);
        }
        finally
        {
            _batch--;
        }
    }

    /// <summary>Lookup functions and option lists per column, kept while the inputs stay the same (no column change per render, R4).</summary>
    private void UpdateSources(GridDef def)
    {
        if (_sourcesFor == (def, LookupFor, OptionsFor))
            return;
        _sourcesFor = (def, LookupFor, OptionsFor);
        _definitionLookups = [];
        _definitionOptions = [];
        foreach (var c in def.Columns.Where(c => !c.Hidden))
        {
            if (LookupFor is { } lookupFor && c.Lookup is { } id && DefinitionType(c) == CellType.Popup)
                _definitionLookups[c.Id] = text => lookupFor(id, text);
            if (OptionsFor is { } optionsFor && c.Type == ColumnType.Select && c.DataSource is { } source)
                _definitionOptions[c.Id] = optionsFor(source);
        }
    }

    private Func<string, Task<IReadOnlyList<string>>>? DefinitionLookup(ColumnDef c) => _definitionLookups.GetValueOrDefault(c.Id);

    private IReadOnlyList<ComboOption>? DefinitionOptions(ColumnDef c) => _definitionOptions.GetValueOrDefault(c.Id);

    /// <summary>
    /// Data set column metadata from the definition (A-52): <c>req</c> → Required, hidden columns included. <c>req</c>
    /// false keeps what T or the app set. TOON v0.2 has no max length, default or scale (<c>fmt</c> is display only, T-13).
    /// </summary>
    private void SupplementColumns(GridDef def)
    {
        foreach (var col in def.Columns.Where(c => c.Type != ColumnType.Button && c.Req))
            DataSet.SetColumn(DataSet.GetColumn(col.Id) with { Required = true });
    }

    /// <summary>Column type (design 7.1): text with lookup → Popup, money with currency → Currency, select → Combo.</summary>
    private static CellType DefinitionType(ColumnDef c) => c.Type switch
    {
        ColumnType.Text => c.Lookup is null ? CellType.Text : CellType.Popup,
        ColumnType.Number => CellType.Number,
        ColumnType.Money => c.Currency is null ? CellType.Number : CellType.Currency,
        ColumnType.Date => CellType.Date,
        ColumnType.Select => CellType.Combo,
        ColumnType.Checkbox => CellType.Check,
        _ => CellType.Button,
    };

    /// <summary>
    /// Column <c>req</c>/<c>min</c>/<c>max</c> plus <c>validate</c> rows with a number comparison (design 5.8, G-02).
    /// <c>requires</c> and other rules are the app's. When a column has several validate rows, the first row's msg is used.
    /// </summary>
    private static Dictionary<string, FieldRule> RulesOf(GridDef def)
    {
        var rules = new Dictionary<string, FieldRule>(StringComparer.Ordinal);
        foreach (var col in def.Columns.Where(c => c.Type != ColumnType.Button))
        {
            var (min, minExclusive, max, maxExclusive) = (col.Min, false, col.Max, false);
            decimal? notEqual = null;
            string? messageKey = null;
            foreach (var v in def.Validate.Where(v => v.Target == col.Id))
            {
                if (v.Rule.Split(' ', StringSplitOptions.RemoveEmptyEntries) is not [var op, var text]
                    || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n))
                    continue;
                switch (op)
                {
                    case "gt" or "ge":
                        (min, minExclusive) = (n, op == "gt");
                        break;
                    case "lt" or "le":
                        (max, maxExclusive) = (n, op == "lt");
                        break;
                    case "ne":
                        notEqual = n;
                        break;
                    default:
                        continue;
                }
                messageKey ??= v.Msg;
            }
            if (col.Req || min is not null || max is not null || notEqual is not null)
                rules[col.Id] = new FieldRule
                {
                    Required = col.Req, Min = min, MinExclusive = minExclusive, Max = max, MaxExclusive = maxExclusive,
                    NotEqual = notEqual, MessageKey = messageKey,
                };
        }
        return rules;
    }
}
