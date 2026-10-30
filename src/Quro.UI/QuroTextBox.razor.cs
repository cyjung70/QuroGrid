using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Quro.Grid.Data;

namespace Quro.UI;

/// <summary>
/// Text box bound to a data set column (Control Spec 8, Addendum A-53·A-63). Shows and edits the data set's current
/// row (A-51); the value is set once when the input is committed (blur or Enter) and saved with the data set.
/// Character filtering, max length and select-all happen in the browser (quro-ui.js), so typing does not call the server.
/// </summary>
public partial class QuroTextBox<TItem> where TItem : class
{
    private const string ModulePath = "./_content/Quro.UI/quro-ui.js";

    private ElementReference _root;
    private IJSObjectReference? _module;
    private QuroDataSet<TItem>? _bound;
    private string? _field;
    private int? _row;
    private string? _text;
    private DataSetColumn _column = null!;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Data set whose current row is shown.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = null!;

    /// <summary>String field of <typeparamref name="TItem"/> (matched like <see cref="QuroDataSet{T}.SetValue"/>).</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>Resource key of the hint shown while the box is empty.</summary>
    [Parameter] public string? PlaceholderKey { get; set; }

    /// <summary>Resolves <see cref="PlaceholderKey"/>. Without it the key itself is shown.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>Characters the box accepts. Default <see cref="QuroCharType.Any"/>.</summary>
    [Parameter] public QuroCharType CharType { get; set; }

    /// <summary>Largest text length. Null uses the column metadata <see cref="DataSetColumn.MaxLength"/>.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>Masks the typed characters.</summary>
    [Parameter] public bool Password { get; set; }

    /// <summary>Selects all text when the box gets focus.</summary>
    [Parameter] public bool AutoSelect { get; set; }

    /// <summary>Not editable (grey). A current row marked Deleted is read-only too.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    private int? EffectiveMaxLength => MaxLength ?? _column.MaxLength;

    private bool IsReadOnly => ReadOnly || _row is { } row && DataSet.GetState(row) == RowState.Deleted;

    private string RootClass => "quro-ui-textbox"
        + (IsReadOnly ? " quro-ui-readonly" : _column.Required ? " quro-ui-required" : "");

    private string? CharsAttribute => CharType == QuroCharType.Any ? null : CharType.ToString().ToLowerInvariant();

    private string? PlaceholderText => PlaceholderKey is null ? null : Localizer?[PlaceholderKey].Value ?? PlaceholderKey;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(DataSet, _bound) || Field != _field)
        {
            var column = DataSet.GetColumn(Field);
            if (column.Type != typeof(string))
                throw new ArgumentException($"QuroTextBox는 문자열 열에만 바인딩합니다 ('{column.Name}'은 {column.Type.Name}).", nameof(Field));
            _column = column;
            _field = Field;
            Bind(DataSet);
        }
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

    private void OnChange(ChangeEventArgs e)
    {
        if (_row is { } row && !IsReadOnly)
            DataSet.SetValue(row, Field, e.Value as string);   // ValueChanged refreshes the box
    }

    private void Read()
    {
        _row = DataSet.CurrentRowKey;
        _text = _row is { } row ? (string?)DataSet.GetValue(row, Field) : null;
    }

    private void Bind(QuroDataSet<TItem>? dataSet)
    {
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

    private void OnCurrentRowChanged(object? sender, CurrentRowChangedEventArgs e) => Refresh();

    private void OnValueChanged(object? sender, DataSetValueChangedEventArgs e)
    {
        if (e.RowKey == _row && e.Field == _column.Name)
            Refresh();
    }

    // Deleted marks change read-only; a row brought back by Redo may be the current row
    private void OnRowChanged(object? sender, DataSetRowEventArgs e)
    {
        if (e.RowKey == _row)
            Refresh();
    }

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
}
