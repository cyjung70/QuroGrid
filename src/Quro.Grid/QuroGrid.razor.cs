using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Quro.Grid.Data;

namespace Quro.Grid;

/// <summary>
/// QuroGrid 본체 (Spec 4.2·4.5). 데이터 값·행 상태는 <see cref="QuroDataSet{T}"/>가 소유하고,
/// 포커스·선택·키 입력은 quro-grid.js가 소유한다 (R1). 서버는 편집 시작·커밋 시점에만 호출된다 (R2).
/// </summary>
public partial class QuroGrid<TItem> where TItem : class
{
    /// <summary>숫자 기본 서식: 천 단위 구분 + 의미 있는 소수만.</summary>
    private const string DefaultNumberFormat = "#,0.##########";

    private readonly List<QuroColumn> _columns = [];
    private (int RowKey, string Field)? _editing;
    private int _epoch;   // 그리드 전체를 다시 그려야 할 때 증가 (파라미터·컬럼 변경)

    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>헤더·메시지 리소스 (Spec 4.8). 없으면 키를 그대로 표시한다.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>표시·입력 해석 로캘. 기본은 <see cref="CultureInfo.CurrentCulture"/>.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    [Parameter] public int FrozenColumns { get; set; }
    [Parameter] public bool ShowFooter { get; set; }

    /// <summary>통화 코드 → 소수 자릿수 (통화 마스터, Spec 4.6). Currency 컬럼에 필요하다.</summary>
    [Parameter] public Func<string, int>? CurrencyDigits { get; set; }

    /// <summary>삭제 표시 행의 화면 처리 (O-02 잠정안: 숨김).</summary>
    [Parameter] public DeletedRowDisplay DeletedRows { get; set; }

    internal IReadOnlyList<QuroColumn> Columns => _columns;
    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    protected override void OnParametersSet() => _epoch++;

    // ── 편집 (quro-grid.js가 호출) ──

    /// <summary>편집 시작. 읽기 전용 컬럼이나 삭제 표시 행이면 false.</summary>
    public bool BeginEdit(int rowKey, string field)
    {
        var col = Column(field);
        if (col.ReadOnly || DataSet.GetState(rowKey) == RowState.Deleted)
            return false;

        _editing = (rowKey, col.Field);
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// 셀 이탈 시 1회 호출 (R2). 문자열은 <see cref="EffectiveCulture"/>로 해석한다.
    /// 해석에 실패하면 값을 바꾸지 않고 편집 상태를 유지한 채 false.
    /// </summary>
    public Task<bool> CommitEdit(int rowKey, string field, string? text)
    {
        var col = Column(field);
        if (col.ReadOnly || !DataSet.TrySetText(rowKey, col.Field, text, EffectiveCulture))
            return Task.FromResult(false);

        _editing = null;
        StateHasChanged();
        return Task.FromResult(true);
    }

    public void CancelEdit()
    {
        _editing = null;
        StateHasChanged();
    }

    /// <summary>앱이 DataSet을 직접 바꾼 뒤 (예: SetCellMark) 화면에 반영한다. 바뀐 행만 다시 그린다.</summary>
    public void Refresh() => StateHasChanged();

    // ── 컬럼 등록 ──

    void IColumnHost.AddColumn(QuroColumn column)
    {
        _columns.Add(column);
        _epoch++;
    }

    void IColumnHost.RemoveColumn(QuroColumn column)
    {
        if (_columns.Remove(column))
        {
            _epoch++;
            StateHasChanged();
        }
    }

    // ── 렌더링 보조 (QuroGridRow와 공유) ──

    private IEnumerable<int> VisibleRowKeys() =>
        DeletedRows == DeletedRowDisplay.Hide
            ? DataSet.RowKeys.Where(k => DataSet.GetState(k) != RowState.Deleted)
            : DataSet.RowKeys;

    internal string Text(string key) => Localizer is null ? key : Localizer[key].Value;

    internal string CellClass(int index, string kind) =>
        index < FrozenColumns ? $"quro-cell {kind} quro-frozen" : $"quro-cell {kind}";

    internal string CellStyle(int index)
    {
        var width = $"width:{_columns[index].Width}px";
        if (index >= FrozenColumns)
            return width;
        var left = _columns.Take(index).Sum(c => c.Width);
        return $"{width};left:{left}px";
    }

    internal string DisplayText(int rowKey, QuroColumn col)
    {
        var value = DataSet.GetValue(rowKey, col.Field);
        return col.Type == CellType.Currency
            ? FormatNumber(value, CurrencyFormat(col, rowKey))
            : Format(value, col);
    }

    /// <summary>에디터 초기 값. 천 단위 구분 없이 사용자 로캘로.</summary>
    internal string EditText(int rowKey, QuroColumn col) =>
        DataSet.GetValue(rowKey, col.Field) switch
        {
            null => "",
            var date when date is DateOnly or DateTime => Format(date, col),
            IFormattable f => f.ToString(null, EffectiveCulture),
            var v => v.ToString() ?? "",
        };

    private string FooterText(QuroColumn col)
    {
        if (col.Aggregate != Aggregate.Sum)
            return "";

        var keys = DataSet.RowKeys.Where(k => DataSet.GetState(k) != RowState.Deleted).ToList();
        var sum = keys.Sum(k => DataSet.GetValue(k, col.Field) is { } v ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : 0m);
        // 통화가 섞이면 합계 의미가 없으므로 첫 행의 통화 자릿수를 쓴다. 혼합 통화 합계 규칙은 Spec에 없다.
        var format = col.Type == CellType.Currency && keys.Count > 0 ? CurrencyFormat(col, keys[0]) : col.Format;
        return FormatNumber(sum, format);
    }

    private string CurrencyFormat(QuroColumn col, int rowKey)
    {
        if (col.Format is not null)
            return col.Format;
        if (CurrencyDigits is null)
            throw new InvalidOperationException($"Currency 컬럼 '{col.Field}'에는 {nameof(CurrencyDigits)}가 필요합니다.");

        var code = col.CurrencyField is null ? null : DataSet.GetValue(rowKey, col.CurrencyField) as string;
        return $"N{CurrencyDigits(code ?? "")}";
    }

    private string Format(object? value, QuroColumn col) => value switch
    {
        null => "",
        DateOnly d => d.ToString(col.Format ?? "d", EffectiveCulture),
        DateTime d => d.ToString(col.Format ?? "d", EffectiveCulture),
        _ when col.Type == CellType.Number => FormatNumber(value, col.Format),
        IFormattable f => f.ToString(col.Format, EffectiveCulture),
        _ => value.ToString() ?? "",
    };

    private string FormatNumber(object? value, string? format) =>
        value is IFormattable f ? f.ToString(format ?? DefaultNumberFormat, EffectiveCulture) : "";

    private QuroColumn Column(string field) =>
        _columns.FirstOrDefault(c => c.Field == field)
        ?? throw new ArgumentException($"컬럼 '{field}'가 없습니다.", nameof(field));
}
