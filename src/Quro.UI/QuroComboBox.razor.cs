using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Quro.Grid.Data;

namespace Quro.UI;

/// <summary>
/// Combo box bound to a data set column, with its list from a code data set (Control Spec 9, Addendum A-57·A-64).
/// Shows and edits the data set's current row (A-51). The whole list is rendered once; typing filters it and arrow keys
/// move in the browser (quro-ui.js), and the value is set once when an item is chosen. Free text is not accepted: on
/// blur the box goes back to the last choice.
/// </summary>
/// <typeparam name="TItem">Row type of the bound data set.</typeparam>
/// <typeparam name="TCode">Row type of the code data set.</typeparam>
public partial class QuroComboBox<TItem, TCode> where TItem : class where TCode : class
{
    private const string ModulePath = "./_content/Quro.UI/quro-ui.js";

    private ElementReference _root;
    private IJSObjectReference? _module;
    private QuroDataSet<TItem>? _bound;
    private QuroDataSet<TCode>? _boundCodes;
    private int? _row;
    private string? _text;
    private List<Option> _options = [];
    private DataSetColumn _column = null!;
    private string? _parentName;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Data set whose current row is shown.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = null!;

    /// <summary>Field of <typeparamref name="TItem"/> that holds the chosen code. Same type as <see cref="ValueField"/>.</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>Code data set. Its rows (Deleted rows left out) are the list, in row order.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TCode> OptionsSource { get; set; } = null!;

    /// <summary>Field of <typeparamref name="TCode"/> stored in <see cref="Field"/>.</summary>
    [Parameter, EditorRequired] public string ValueField { get; set; } = "";

    /// <summary>Field of <typeparamref name="TCode"/> shown as the item text.</summary>
    [Parameter, EditorRequired] public string TextField { get; set; } = "";

    /// <summary>Linked combo: field of <typeparamref name="TCode"/> compared with <see cref="ParentField"/>. Set both or neither.</summary>
    [Parameter] public string? FilterField { get; set; }

    /// <summary>
    /// Linked combo: field of <typeparamref name="TItem"/> holding the parent value. Only codes whose
    /// <see cref="FilterField"/> equals it are listed (none while it is null). When it changes and the value left the
    /// list, the value is cleared (one undoable <see cref="QuroDataSet{T}.SetValue"/>).
    /// </summary>
    [Parameter] public string? ParentField { get; set; }

    /// <summary>Resource key of an empty first item ("All", "Select") that sets null. Null shows no empty item.</summary>
    [Parameter] public string? EmptyKey { get; set; }

    /// <summary>Shows the code before the text ("01 Seoul").</summary>
    [Parameter] public bool ShowCode { get; set; }

    /// <summary>Columns of the open list. Null shows the item text only.</summary>
    [Parameter] public IReadOnlyList<QuroListColumn>? ListColumns { get; set; }

    /// <summary>
    /// Multi-select with check marks. The chosen codes are joined with <see cref="Separator"/> in list order into one
    /// string column (null when none). Needs string <see cref="Field"/> and <see cref="ValueField"/>.
    /// </summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>Joins the codes of a <see cref="Multiple"/> combo. Default <c>|</c>.</summary>
    [Parameter] public string Separator { get; set; } = "|";

    /// <summary>Resource key of the hint shown while the box is empty.</summary>
    [Parameter] public string? PlaceholderKey { get; set; }

    /// <summary>Resolves <see cref="EmptyKey"/>, <see cref="PlaceholderKey"/> and header keys. Without it the key itself is shown.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>Not editable (grey). A current row marked Deleted is read-only too.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    private bool IsReadOnly => ReadOnly || _row is { } row && DataSet.GetState(row) == RowState.Deleted;

    private string RootClass => "quro-ui-combo"
        + (IsReadOnly ? " quro-ui-readonly" : _column.Required ? " quro-ui-required" : "");

    private string? PlaceholderText => PlaceholderKey is null ? null : Text(PlaceholderKey);

    private string Text(string? key) => key is null ? "" : Localizer?[key].Value ?? key;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _column = DataSet.GetColumn(Field);
        var value = OptionsSource.GetColumn(ValueField);
        OptionsSource.GetColumn(TextField);
        if (Under(_column.Type) != Under(value.Type))
            throw new ArgumentException($"QuroComboBox '{_column.Name}'({_column.Type.Name})와 ValueField '{value.Name}'({value.Type.Name})의 형식이 다릅니다.", nameof(Field));
        if (Multiple && _column.Type != typeof(string))
            throw new ArgumentException($"다중 선택 QuroComboBox는 문자열 열에만 바인딩합니다 ('{_column.Name}'은 {_column.Type.Name}).", nameof(Field));
        if ((FilterField is null) != (ParentField is null))
            throw new ArgumentException("FilterField와 ParentField는 함께 지정합니다.", nameof(FilterField));
        if (FilterField is not null)
            OptionsSource.GetColumn(FilterField);
        _parentName = ParentField is null ? null : DataSet.GetColumn(ParentField).Name;
        foreach (var column in ListColumns ?? [])
            OptionsSource.GetColumn(column.Field);
        if (string.IsNullOrEmpty(Separator))
            throw new ArgumentException("Separator는 비울 수 없습니다.", nameof(Separator));

        Bind(DataSet);
        BindCodes(OptionsSource);
        Read();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("attach", _root);
    }

    /// <summary>Choice from the browser: "" (empty item or none checked), "2" or, for multi-select, "0,3".</summary>
    private void OnChoose(ChangeEventArgs e)
    {
        if (_row is not { } row || IsReadOnly)
            return;
        var chosen = (e.Value as string ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : -1)
            .Where(i => i >= 0 && i < _options.Count)
            .Distinct()
            .Order()
            .Select(i => _options[i])
            .ToList();
        var value = Multiple
            ? chosen.Count == 0 ? null : string.Join(Separator, chosen.Select(o => o.Code))
            : chosen.FirstOrDefault()?.Value;
        if (!Equals(value, DataSet.GetValue(row, Field)))
            DataSet.SetValue(row, Field, value);   // ValueChanged refreshes the box
    }

    private void Read()
    {
        _row = DataSet.CurrentRowKey;
        var value = _row is { } row ? DataSet.GetValue(row, Field) : null;
        var codes = Multiple ? Codes(value as string) : [];
        var parent = _row is { } r && _parentName is { } p ? Key(DataSet.GetValue(r, p)) : null;

        _options = [];
        foreach (var key in OptionsSource.RowKeys)
        {
            if (OptionsSource.GetState(key) == RowState.Deleted)
                continue;
            if (FilterField is { } filter && (parent is null || Key(OptionsSource.GetValue(key, filter)) != parent))
                continue;
            var code = OptionsSource.GetValue(key, ValueField);
            var codeText = Key(code) ?? "";
            var text = Convert.ToString(OptionsSource.GetValue(key, TextField), CultureInfo.CurrentCulture) ?? "";
            var display = ShowCode ? $"{codeText} {text}" : text;
            var cells = ListColumns is { Count: > 0 } columns
                ? columns.Select(c => Convert.ToString(OptionsSource.GetValue(key, c.Field), CultureInfo.CurrentCulture) ?? "").ToArray()
                : null;
            var selected = Multiple ? codes.Contains(codeText) : value is not null && Equals(code, value);
            _options.Add(new Option(key, code, codeText, display, $"{codeText} {text}".ToLowerInvariant(), cells, selected));
        }

        _text = Multiple
            ? string.Join(", ", codes.Select(c => _options.Find(o => o.Code == c)?.Display ?? c))
            : value is null ? null : _options.Find(o => o.Selected)?.Display ?? Key(value);
    }

    /// <summary>After a parent change: drops the value (or, for multi-select, the codes) that left the list.</summary>
    private void DropInvalid()
    {
        if (_row is not { } row || IsReadOnly)
            return;
        var value = DataSet.GetValue(row, Field);
        if (value is null)
            return;
        object? kept = Multiple
            ? string.Join(Separator, _options.Where(o => Codes(value as string).Contains(o.Code)).Select(o => o.Code)) is { Length: > 0 } s ? s : null
            : _options.Exists(o => o.Selected) ? value : null;
        if (!Equals(kept, value))
            DataSet.SetValue(row, Field, kept);
    }

    private HashSet<string> Codes(string? value) =>
        value is null ? [] : value.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static string? Key(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);

    private static Type Under(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private void Bind(QuroDataSet<TItem>? dataSet)
    {
        if (ReferenceEquals(dataSet, _bound))
            return;
        if (_bound is { } old)
        {
            old.CurrentRowChanged -= OnCurrentRowChanged;
            old.ValueChanged -= OnValueChanged;
            old.RowAdded -= OnRowChanged;
            old.RowDeleted -= OnRowChanged;
            old.Loaded -= OnLoaded;
        }
        _bound = dataSet;
        if (dataSet is null)
            return;
        dataSet.CurrentRowChanged += OnCurrentRowChanged;
        dataSet.ValueChanged += OnValueChanged;
        dataSet.RowAdded += OnRowChanged;
        dataSet.RowDeleted += OnRowChanged;
        dataSet.Loaded += OnLoaded;
    }

    private void BindCodes(QuroDataSet<TCode>? codes)
    {
        if (ReferenceEquals(codes, _boundCodes))
            return;
        if (_boundCodes is { } old)
        {
            old.ValueChanged -= OnCodesChanged;
            old.RowAdded -= OnCodesChanged;
            old.RowDeleted -= OnCodesChanged;
            old.Loaded -= OnLoaded;
        }
        _boundCodes = codes;
        if (codes is null)
            return;
        codes.ValueChanged += OnCodesChanged;
        codes.RowAdded += OnCodesChanged;
        codes.RowDeleted += OnCodesChanged;
        codes.Loaded += OnLoaded;
    }

    private void OnCurrentRowChanged(object? sender, CurrentRowChangedEventArgs e) => Refresh();

    private void OnValueChanged(object? sender, DataSetValueChangedEventArgs e)
    {
        if (e.RowKey != _row)
            return;
        if (e.Field == _parentName)
        {
            Refresh();
            DropInvalid();
        }
        else if (e.Field == _column.Name)
            Refresh();
    }

    // Deleted marks change read-only; a row brought back by Redo may be the current row
    private void OnRowChanged(object? sender, DataSetRowEventArgs e)
    {
        if (e.RowKey == _row)
            Refresh();
    }

    private void OnCodesChanged(object? sender, EventArgs e) => Refresh();

    private void OnLoaded(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Read();
        _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Bind(null);
        BindCodes(null);
        if (_module is null)
            return;
        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // circuit already gone
        }
    }

    private sealed record Option(int RowKey, object? Value, string Code, string Display, string Search, string[]? Cells, bool Selected);
}
