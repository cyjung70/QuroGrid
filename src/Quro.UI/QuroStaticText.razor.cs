using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quro.Grid.Data;

namespace Quro.UI;

/// <summary>
/// Read-only value of a data set column (Control Spec 11, Addendum A-66). Shows the data set's current row (A-51) and
/// follows its changes. No browser script.
/// </summary>
public partial class QuroStaticText<TItem> where TItem : class
{
    private static readonly HashSet<Type> Integers =
        [typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong)];

    private static readonly HashSet<Type> Reals = [typeof(decimal), typeof(double), typeof(float)];

    private QuroDataSet<TItem>? _bound;
    private int? _row;
    private string? _text;
    private DataSetColumn _column = null!;
    private QuroMask? _mask;
    private string? _maskText;

    /// <summary>Data set whose current row is shown.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = null!;

    /// <summary>Field of <typeparamref name="TItem"/> (matched like <see cref="QuroDataSet{T}.SetValue"/>).</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>
    /// .NET format string. Null formats by column type: numbers with group separators (decimal places from the column
    /// <see cref="DataSetColumn.Scale"/>, else the value's own digits), dates in the culture's short date.
    /// </summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>Culture of the shown value. Null uses the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Mask for a string value stored without its fixed characters (like <see cref="QuroMaskBox{TItem}.Mask"/>,
    /// A-69). A value that doesn't fit is shown as it is.
    /// </summary>
    [Parameter] public string? Mask { get; set; }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private bool IsNumber => Integers.Contains(Under(_column.Type)) || Reals.Contains(Under(_column.Type));

    private string RootClass => IsNumber ? "quro-ui-static quro-ui-static-number" : "quro-ui-static";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _column = DataSet.GetColumn(Field);
        if (Mask != _maskText)
        {
            _mask = Mask is null ? null : QuroMask.Parse(Mask, nameof(Mask));
            _maskText = Mask;
        }
        Bind(DataSet);
        Read();
    }

    private void Read()
    {
        _row = DataSet.CurrentRowKey;
        _text = Text(_row is { } row ? DataSet.GetValue(row, _column.Name) : null);
    }

    private string? Text(object? value)
    {
        var culture = EffectiveCulture;
        if (value is null)
            return null;
        if (_mask is not null && value is string s)
            return _mask.Format(s);
        if (Format is not null)
            return value is IFormattable f ? f.ToString(Format, culture) : Convert.ToString(value, culture);
        return value switch
        {
            DateOnly d => d.ToString("d", culture),
            DateTime t => t.ToString("d", culture),
            decimal m => m.ToString("N" + (_column.Scale ?? (decimal.GetBits(m)[3] >> 16) & 0xFF), culture),
            double or float when _column.Scale is { } scale => ((IFormattable)value).ToString("N" + scale, culture),
            double or float => ((IFormattable)value).ToString("#,##0.################", culture),
            IFormattable f when Integers.Contains(value.GetType()) => f.ToString("N0", culture),
            _ => Convert.ToString(value, culture),
        };
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
        if (e.RowKey == _row && e.Field == _column.Name)
            Refresh();
    }

    // a row brought back by Redo may be the current row
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
    public void Dispose() => Bind(null);
}
