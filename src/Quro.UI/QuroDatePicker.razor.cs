using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Quro.Grid.Data;

namespace Quro.UI;

/// <summary>
/// Date picker bound to data set columns (Control Spec 10, Addendum A-65). Shows and edits the data set's current row
/// (A-51) in the culture's short date. Typed text is read when committed (blur or Enter) and set once; text that is not
/// a date goes back to the last value. The calendar is drawn and moved in the browser (quro-ui.js).
/// </summary>
public partial class QuroDatePicker<TItem> where TItem : class
{
    private const string ModulePath = "./_content/Quro.UI/quro-ui.js";
    private const string IsoDate = "yyyy-MM-dd";
    private const string IsoMonth = "yyyy-MM";

    private ElementReference _root;
    private IJSObjectReference? _module;
    private QuroDataSet<TItem>? _bound;
    private int? _row;
    private DataSetColumn _column = null!;
    private DataSetColumn? _toColumn;
    private Shown _from = new(null, null);
    private Shown _to = new(null, null);

    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Data set whose current row is shown.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = null!;

    /// <summary>
    /// Date field (<c>DateOnly</c>, <c>DateTime</c>, nullable or not), or the string field of a <see cref="Month"/>
    /// picker. With <see cref="ToField"/> it is the start of the range.
    /// </summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>End of a range: a second box bound to this field, the same kind of column as <see cref="Field"/>. A change that puts the end before the start is refused.</summary>
    [Parameter] public string? ToField { get; set; }

    /// <summary>Month mode: year and month only, stored as a <c>yyyy-MM</c> string.</summary>
    [Parameter] public bool Month { get; set; }

    /// <summary>Culture of the shown and typed dates and of the calendar. Null uses the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Resource key of the hint shown while the (start) box is empty.</summary>
    [Parameter] public string? PlaceholderKey { get; set; }

    /// <summary>Resolves <see cref="PlaceholderKey"/>. Without it the key itself is shown.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>Not editable (grey). A current row marked Deleted is read-only too.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private bool IsReadOnly => ReadOnly || _row is { } row && DataSet.GetState(row) == RowState.Deleted;

    private string RootClass => "quro-ui-date"
        + (IsReadOnly ? " quro-ui-readonly" : _column.Required || _toColumn?.Required == true ? " quro-ui-required" : "");

    private string? PlaceholderText => PlaceholderKey is null ? null : Localizer?[PlaceholderKey].Value ?? PlaceholderKey;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _column = Check(DataSet.GetColumn(Field), nameof(Field));
        _toColumn = ToField is null ? null : Check(DataSet.GetColumn(ToField), nameof(ToField));
        if (_toColumn is not null && Under(_toColumn.Type) != Under(_column.Type))
            throw new ArgumentException($"QuroDatePicker ToField '{_toColumn.Name}'은 '{_column.Name}'과 같은 형식이어야 합니다.", nameof(ToField));
        Bind(DataSet);
        Read();
    }

    private DataSetColumn Check(DataSetColumn column, string parameter)
    {
        var type = Under(column.Type);
        var ok = Month ? type == typeof(string) : type == typeof(DateOnly) || type == typeof(DateTime);
        if (!ok)
            throw new ArgumentException(Month
                ? $"월 모드 QuroDatePicker는 문자열 열에만 바인딩합니다 ('{column.Name}'은 {column.Type.Name})."
                : $"QuroDatePicker는 DateOnly·DateTime 열에만 바인딩합니다 ('{column.Name}'은 {column.Type.Name}).", parameter);
        return column;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("attach", _root);
    }

    private Task OnFromChange(ChangeEventArgs e) => CommitAsync(_column, e.Value as string);

    private Task OnToChange(ChangeEventArgs e) => CommitAsync(_toColumn!, e.Value as string);

    private async Task CommitAsync(DataSetColumn column, string? text)
    {
        if (_row is not { } row)
            return;
        if (!IsReadOnly && TryRead(column, text, out var value) && InRange(row, column, value)
            && !Equals(value, DataSet.GetValue(row, column.Name)))
        {
            DataSet.SetValue(row, column.Name, value);   // ValueChanged refreshes the box
            return;
        }

        // Refused or unchanged: the input still shows the typed text, but the last render has the same value as now,
        // so render the typed text once and then the value again to make the browser replace it.
        Read();
        var shown = column == _column ? _from : _to;
        if (shown.Text == (text ?? "").Trim() || shown.Text is null && string.IsNullOrWhiteSpace(text))
            return;
        SetShown(column, shown with { Text = text });
        StateHasChanged();
        await Task.Yield();
        Read();
    }

    private void SetShown(DataSetColumn column, Shown shown)
    {
        if (column == _column)
            _from = shown;
        else
            _to = shown;
    }

    /// <summary>Typed text → column value. Empty is null on a nullable column.</summary>
    private bool TryRead(DataSetColumn column, string? text, out object? value)
    {
        value = null;
        text = text?.Trim() ?? "";
        if (text.Length == 0)
            return Month || Nullable.GetUnderlyingType(column.Type) is not null;
        if (Month)
        {
            if (!TryParseMonth(text, out var month))
                return false;
            value = month.ToString(IsoMonth, CultureInfo.InvariantCulture);
            return true;
        }
        if (!TryParseDate(text, out var date))
            return false;
        value = Under(column.Type) == typeof(DateTime) ? date.ToDateTime(TimeOnly.MinValue) : date;
        return true;
    }

    private bool TryParseDate(string text, out DateOnly date)
    {
        var culture = EffectiveCulture;
        if (text.Length == 8 && text.All(char.IsAsciiDigit))
            return DateOnly.TryParseExact(text, DigitFormat(true), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        return DateOnly.TryParseExact(text, IsoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private bool TryParseMonth(string text, out DateOnly month)
    {
        if (text.Length == 6 && text.All(char.IsAsciiDigit))
            return DateOnly.TryParseExact(text, DigitFormat(false), CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
        return DateOnly.TryParseExact(text, IsoMonth, CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
            || DateOnly.TryParseExact(text, MonthPattern, EffectiveCulture, DateTimeStyles.AllowWhiteSpaces, out month);
    }

    /// <summary>Digits-only format in the culture's order: ko <c>yyyyMMdd</c>, en-US <c>MMddyyyy</c>.</summary>
    private string DigitFormat(bool withDay)
    {
        var pattern = EffectiveCulture.DateTimeFormat.ShortDatePattern;
        var parts = new[] { ('y', "yyyy"), ('M', "MM"), ('d', "dd") }
            .Where(p => withDay || p.Item1 != 'd')
            .OrderBy(p => pattern.IndexOf(p.Item1) is var i and >= 0 ? i : int.MaxValue);
        return string.Concat(parts.Select(p => p.Item2));
    }

    /// <summary>The culture's short date without the day: ko <c>yyyy-MM</c>, en-US <c>M/yyyy</c>.</summary>
    private string MonthPattern
    {
        get
        {
            var pattern = EffectiveCulture.DateTimeFormat.ShortDatePattern;
            var separator = Regex.Match(pattern, "[yMd]+([^yMd]+)[yMd]") is { Success: true } m ? m.Groups[1].Value : "-";
            var tokens = Regex.Matches(pattern, "y+|M+").Select(t => t.Value);
            return string.Join(separator, tokens);
        }
    }

    /// <summary>A change that puts the end before the start is refused (A-65 ④).</summary>
    private bool InRange(int row, DataSetColumn column, object? value)
    {
        if (_toColumn is null || value is null)
            return true;
        var other = DataSet.GetValue(row, column == _column ? _toColumn.Name : _column.Name);
        if (AsDate(other) is not { } otherDate || AsDate(value) is not { } date)
            return true;
        return column == _column ? date <= otherDate : date >= otherDate;
    }

    private static DateOnly? AsDate(object? value) => value switch
    {
        DateOnly d => d,
        DateTime t => DateOnly.FromDateTime(t),
        string s when DateOnly.TryParseExact(s, IsoMonth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var m) => m,
        _ => null,
    };

    private void Read()
    {
        _row = DataSet.CurrentRowKey;
        _from = ShownOf(_column);
        _to = _toColumn is null ? new(null, null) : ShownOf(_toColumn);
    }

    private Shown ShownOf(DataSetColumn column)
    {
        var value = _row is { } row ? DataSet.GetValue(row, column.Name) : null;
        if (value is string text && !Month)
            return new(text, null);
        if (AsDate(value) is not { } date)
            return new(value as string, null);   // month text that is not yyyy-MM is shown as is
        return Month
            ? new(date.ToString(MonthPattern, EffectiveCulture), date.ToString(IsoMonth, CultureInfo.InvariantCulture))
            : new(date.ToString("d", EffectiveCulture), date.ToString(IsoDate, CultureInfo.InvariantCulture));
    }

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

    private void OnCurrentRowChanged(object? sender, CurrentRowChangedEventArgs e) => Refresh();

    private void OnValueChanged(object? sender, DataSetValueChangedEventArgs e)
    {
        if (e.RowKey == _row && (e.Field == _column.Name || e.Field == _toColumn?.Name))
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

    /// <summary>Text in the box and the ISO value the calendar opens on.</summary>
    private sealed record Shown(string? Text, string? Iso);
}
