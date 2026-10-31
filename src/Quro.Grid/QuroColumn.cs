using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quro.Grid.Data;

namespace Quro.Grid;

/// <summary>
/// 컬럼 정의 (Spec 4.5). 직접 그리지 않고 부모 <see cref="QuroGrid{TItem}"/>에 등록만 한다.
/// </summary>
public sealed class QuroColumn : ComponentBase, IDisposable
{
    [CascadingParameter] internal IColumnHost? Host { get; set; }

    /// <summary>이 컬럼을 묶은 다단 헤더 그룹 (GS-070). 없으면 null</summary>
    [CascadingParameter] internal QuroColumnGroup? Group { get; set; }

    /// <summary>Property name of the row item bound to this column.</summary>
    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>헤더 리소스 키 (Spec 4.8). 문자열을 하드코딩하지 않는다.</summary>
    [Parameter, EditorRequired] public string HeaderKey { get; set; } = "";

    /// <summary>Cell type (Spec 4.6).</summary>
    [Parameter] public CellType Type { get; set; } = CellType.Text;

    /// <summary>.NET 서식 문자열 (예: N3). 표시는 그리드의 Culture를 따른다.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>True when the cells cannot be edited.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Editable only on added rows (state Inserted); read-only on saved rows. Screen definition <c>edit: new</c>, G-01.</summary>
    [Parameter] public bool NewRowOnly { get; set; }

    /// <summary>Header filter allowed (G-04). Default true.</summary>
    [Parameter] public bool Filterable { get; set; } = true;

    /// <summary>Header sort allowed (G-04). Default true.</summary>
    [Parameter] public bool Sortable { get; set; } = true;

    /// <summary>Grouping by this column allowed (G-04). Default true.</summary>
    [Parameter] public bool Groupable { get; set; } = true;

    /// <summary>열 너비 (px).</summary>
    [Parameter] public int Width { get; set; } = 120;

    /// <summary>Footer aggregate.</summary>
    [Parameter] public Aggregate Aggregate { get; set; }

    /// <summary>Currency 컬럼에서 행의 통화 코드를 읽을 필드 (Spec 4.6).</summary>
    [Parameter] public string? CurrencyField { get; set; }

    /// <summary>필수 입력 (DataSet 규칙으로 등록, Addendum A-10).</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Smallest allowed number (registered as a data set rule).</summary>
    [Parameter] public decimal? Min { get; set; }
    /// <summary>Largest allowed number (registered as a data set rule).</summary>
    [Parameter] public decimal? Max { get; set; }

    /// <summary>
    /// Popup 셀 코드 조회 (Addendum A-16). 입력 문자열 → 일치하는 코드 목록.
    /// 1건이면 자동 확정, 여러 건이면 OnPopupRequested, 0건이면 편집 유지 (A-05).
    /// </summary>
    [Parameter] public Func<string, Task<IReadOnlyList<string>>>? Lookup { get; set; }

    /// <summary>Combo 셀 목록 (Spec 4.6: 공통코드).</summary>
    [Parameter] public IReadOnlyList<ComboOption>? Options { get; set; }

    /// <summary>
    /// Combo list from a code data set (A-57), in place of <see cref="Options"/>: <see cref="ValueField"/> is stored,
    /// <see cref="TextField"/> is shown. Deleted code rows are left out. The grid redraws when the code data set changes.
    /// </summary>
    [Parameter] public IQuroDataSet? OptionsSource { get; set; }

    /// <summary>Field of <see cref="OptionsSource"/> that holds the code (the stored value).</summary>
    [Parameter] public string? ValueField { get; set; }

    /// <summary>Field of <see cref="OptionsSource"/> that holds the shown text.</summary>
    [Parameter] public string? TextField { get; set; }

    /// <summary>Linked combo: field of <see cref="OptionsSource"/> holding each code's parent value. Set with <see cref="ParentField"/>.</summary>
    [Parameter] public string? FilterField { get; set; }

    /// <summary>
    /// Linked combo: grid row field holding the parent value. Only codes whose <see cref="FilterField"/> equals it are
    /// listed (none while it is null). When it changes and the code left the list, the cell is cleared.
    /// </summary>
    [Parameter] public string? ParentField { get; set; }

    /// <summary>Check 셀에서 체크 상태로 저장할 값 (Y/N 매핑 등). 기본 true.</summary>
    [Parameter] public object? CheckedValue { get; set; } = true;

    /// <summary>Check 셀에서 체크 해제 상태로 저장할 값. 기본 false.</summary>
    [Parameter] public object? UncheckedValue { get; set; } = false;

    /// <summary>Button 셀 표시 문자열 리소스 키 (Spec 4.8). Button 컬럼의 Field는 식별자일 뿐 값을 읽지 않는다.</summary>
    [Parameter] public string? TextKey { get; set; }

    /// <summary>
    /// Display-only value from the row item (A-59). The grid doesn't evaluate expressions; the app gives the function.
    /// The column is read-only and not saved, and <see cref="Field"/> is only an identifier. Header filter, sort,
    /// grouping and export are off; the footer Sum adds the computed values.
    /// </summary>
    [Parameter] public Func<object, object?>? Compute { get; set; }

    /// <summary>Cell look by row item and cell value (the computed value on a <see cref="Compute"/> column), A-59.</summary>
    [Parameter] public Func<object, object?, CellTone>? CellStyle { get; set; }

    private object? _rendered;
    private IQuroDataSet? _codes;   // OptionsSource whose events this column follows
    private Dictionary<string, List<ComboOption>>? _lists;   // code lists by parent key (AllCodes = no filter), dropped on any code change

    private const string AllCodes = "\0";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Host is null)
            throw new InvalidOperationException($"{nameof(QuroColumn)}은 QuroGrid 안에서만 쓸 수 있습니다.");
        CheckCodeSource();
        BindCodes(OptionsSource);
        Host.AddColumn(this);
    }

    /// <summary>정의가 실제로 바뀐 경우에만 그리드 전체를 다시 그리게 한다 (R4).</summary>
    protected override void OnParametersSet()
    {
        object now = (Field, HeaderKey, Type, Format, ReadOnly, Width, Aggregate, CurrencyField, Required, Min, Max,
            (Lookup, Options, CheckedValue, UncheckedValue, TextKey), (NewRowOnly, Filterable, Sortable, Groupable),
            (Compute, CellStyle), (OptionsSource, ValueField, TextField, FilterField, ParentField));
        if (_rendered is not null && !Equals(now, _rendered))
        {
            CheckCodeSource();
            BindCodes(OptionsSource);
            _lists = null;
            Host?.ColumnChanged(this);
        }
        _rendered = now;
    }

    private void CheckCodeSource()
    {
        if (OptionsSource is not { } codes)
            return;
        if (Options is not null)
            throw new ArgumentException($"QuroColumn '{Field}': Options와 OptionsSource는 함께 쓸 수 없습니다.", nameof(OptionsSource));
        if (string.IsNullOrEmpty(ValueField) || string.IsNullOrEmpty(TextField))
            throw new ArgumentException($"QuroColumn '{Field}': OptionsSource에는 ValueField와 TextField가 필요합니다.", nameof(OptionsSource));
        if ((FilterField is null) != (ParentField is null))
            throw new ArgumentException($"QuroColumn '{Field}': FilterField와 ParentField는 함께 지정합니다.", nameof(FilterField));
        codes.GetColumn(ValueField);   // ArgumentException for a field the code data set doesn't have
        codes.GetColumn(TextField);
        if (FilterField is not null)
            codes.GetColumn(FilterField);
    }

    private void BindCodes(IQuroDataSet? codes)
    {
        if (ReferenceEquals(codes, _codes))
            return;
        if (_codes is { } old)
        {
            old.ValueChanged -= OnCodesChanged;
            old.RowAdded -= OnCodesChanged;
            old.RowDeleted -= OnCodesChanged;
            old.Loaded -= OnCodesChanged;
        }
        _codes = codes;
        _lists = null;
        if (codes is null)
            return;
        codes.ValueChanged += OnCodesChanged;
        codes.RowAdded += OnCodesChanged;
        codes.RowDeleted += OnCodesChanged;
        codes.Loaded += OnCodesChanged;
    }

    private void OnCodesChanged(object? sender, EventArgs e)
    {
        _lists = null;
        Host?.CodesChanged();
    }

    /// <summary>
    /// Combo list: <see cref="Options"/>, or the code rows of <see cref="OptionsSource"/> for the parent value
    /// (<paramref name="all"/> = every code, for filter values, export and texts without a row).
    /// </summary>
    internal IReadOnlyList<ComboOption>? ListFor(object? parent, bool all = false)
    {
        if (OptionsSource is not { } codes)
            return Options;
        var key = all || FilterField is null ? AllCodes : Convert.ToString(parent, CultureInfo.InvariantCulture);
        if (key is null)
            return [];
        _lists ??= [];
        if (!_lists.TryGetValue(key, out var list))
        {
            list = [];
            foreach (var row in codes.RowKeys)
                if (codes.GetState(row) != RowState.Deleted
                    && (key == AllCodes || Convert.ToString(codes.GetValue(row, FilterField!), CultureInfo.InvariantCulture) == key))
                    list.Add(new ComboOption(codes.GetValue(row, ValueField!),
                        Convert.ToString(codes.GetValue(row, TextField!), CultureInfo.CurrentCulture) ?? ""));
            _lists[key] = list;
        }
        return list;
    }

    internal bool CanFilter => Filterable && Type != CellType.Button && Compute is null;
    internal bool CanSort => Sortable && Type != CellType.Button && Compute is null;
    internal bool CanGroup => Groupable && Type != CellType.Button && Compute is null;

    /// <summary>Removes the column from the grid.</summary>
    public void Dispose()
    {
        BindCodes(null);
        Host?.RemoveColumn(this);
    }
}

internal interface IColumnHost
{
    void AddColumn(QuroColumn column);
    void ColumnChanged(QuroColumn column);
    void CodesChanged();
    void RemoveColumn(QuroColumn column);
    void AddGroup(QuroColumnGroup group);
    void GroupChanged();
    void RemoveGroup(QuroColumnGroup group);
}
