using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Quro.Grid.Data;

namespace Quro.UI;

/// <summary>
/// Mask box bound to a string column (Control Spec 11, Addendum A-69). Shows the stored typed characters with the
/// mask's fixed characters; the browser (quro-ui.js) adds the fixed characters and drops characters that don't fit while
/// typing, so typing does not call the server. On commit (blur or Enter) a complete input stores only the typed
/// characters, an empty box stores null, and anything else goes back to the last value.
/// </summary>
public partial class QuroMaskBox<TItem> where TItem : class
{
    private const string ModulePath = "./_content/Quro.UI/quro-ui.js";

    private ElementReference _root;
    private IJSObjectReference? _module;
    private QuroDataSet<TItem>? _bound;
    private string? _field;
    private int? _row;
    private string? _text;
    private DataSetColumn _column = null!;
    private QuroMask _mask = null!;
    private string? _maskText;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Data set whose current row is shown.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = null!;

    /// <summary>String field of <typeparamref name="TItem"/> (matched like <see cref="QuroDataSet{T}.SetValue"/>).</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>Resource key of the hint shown while the box is empty.</summary>
    [Parameter] public string? PlaceholderKey { get; set; }

    /// <summary>Resolves <see cref="PlaceholderKey"/>. Without it the key itself is shown.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>
    /// Mask: <c>0</c> digit, <c>A</c> letter of any language, <c>*</c> letter or digit, <c>\</c> makes the next
    /// character fixed, any other character is fixed. Example: <c>000-00-00000</c>.
    /// </summary>
    [Parameter, EditorRequired] public string Mask { get; set; } = "";

    /// <summary>Selects all text when the box gets focus.</summary>
    [Parameter] public bool AutoSelect { get; set; }

    /// <summary>Not editable (grey). A current row marked Deleted is read-only too.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    private bool IsReadOnly => ReadOnly || _row is { } row && DataSet.GetState(row) == RowState.Deleted;

    private string RootClass => "quro-ui-mask"
        + (IsReadOnly ? " quro-ui-readonly" : _column.Required ? " quro-ui-required" : "");

    private string? PlaceholderText => PlaceholderKey is null ? null : Localizer?[PlaceholderKey].Value ?? PlaceholderKey;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Mask != _maskText)
        {
            _mask = QuroMask.Parse(Mask, nameof(Mask));
            _maskText = Mask;
        }
        if (!ReferenceEquals(DataSet, _bound) || Field != _field)
        {
            var column = DataSet.GetColumn(Field);
            if (column.Type != typeof(string))
                throw new ArgumentException($"QuroMaskBox는 문자열 열에만 바인딩합니다 ('{column.Name}'은 {column.Type.Name}).", nameof(Field));
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

    private async Task OnChange(ChangeEventArgs e)
    {
        if (_row is not { } row)
            return;
        var text = e.Value as string ?? "";
        string? value = null;
        if (!IsReadOnly && (text.Length == 0 || _mask.TryRead(text, out value!))
            && value != (string?)DataSet.GetValue(row, _column.Name))
        {
            DataSet.SetValue(row, _column.Name, value);   // ValueChanged refreshes the box
            return;
        }

        // Refused or unchanged: the input still shows the typed text, but the last render has the same value as now,
        // so render the typed text once and then the value again to make the browser replace it.
        Read();
        var shown = _text;
        if (text == shown)
            return;
        _text = text;
        StateHasChanged();
        await Task.Yield();
        Read();
    }

    private void Read()
    {
        _row = DataSet.CurrentRowKey;
        _text = _row is { } row && DataSet.GetValue(row, _column.Name) is string value ? _mask.Format(value) : null;
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
