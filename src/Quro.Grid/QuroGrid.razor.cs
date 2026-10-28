using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
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

    private const string ModulePath = "./_content/Quro.Grid/quro-grid.js";
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<QuroGrid<TItem>>? _self;
    private bool _ensureActive;   // 행 삭제 후 JS에 활성 셀을 남은 행으로 옮기게 한다 (렌더 반영 뒤)

    [Inject] private IJSRuntime JS { get; set; } = default!;

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

    /// <summary>Enter 확정 후 이동 방향 (O-05 결정: 기본 오른쪽).</summary>
    [Parameter] public EnterDirection EnterDirection { get; set; }

    /// <summary>마지막 편집 가능 셀에서 Enter 시 새 행 추가 (2026-10-28 결정: 옵션, 기본 끔 잠정).</summary>
    [Parameter] public bool AddRowOnLastEnter { get; set; }

    /// <summary>붙여넣기 행이 모자라면 자동 추가 (Spec 4.7, Addendum A-03: 기본 켬).</summary>
    [Parameter] public bool AddRowsOnPaste { get; set; } = true;

    // ── 이벤트 (Spec 4.10). OnPopupRequested·OnSelectionChanged는 JS 층과 함께 추가한다. ──

    [Parameter] public EventCallback<CellValueChangingArgs<TItem>> OnCellValueChanging { get; set; }
    [Parameter] public EventCallback<CellValueChangedArgs<TItem>> OnCellValueChanged { get; set; }
    [Parameter] public EventCallback<RowAddingArgs<TItem>> OnRowAdding { get; set; }
    [Parameter] public EventCallback<RowAddedArgs<TItem>> OnRowAdded { get; set; }
    [Parameter] public EventCallback<RowDeletingArgs<TItem>> OnRowDeleting { get; set; }

    /// <summary>삭제 후 (제안 A-14 — Spec 이벤트 목록에 없음). 삭제 호출 1회에 한 번.</summary>
    [Parameter] public EventCallback<RowDeletedArgs> OnRowDeleted { get; set; }
    [Parameter] public EventCallback<PastingArgs> OnPasting { get; set; }

    /// <summary>붙여넣기 후: 추가된 행, 건너뛴 셀과 사유 (Addendum A-04).</summary>
    [Parameter] public EventCallback<PastedArgs> OnPasted { get; set; }

    /// <summary>활성 행 변경 (Addendum A-13). 구독한 경우에만 JS가 보낸다.</summary>
    [Parameter] public EventCallback<SelectionChangedArgs<TItem>> OnSelectionChanged { get; set; }

    internal IReadOnlyList<QuroColumn> Columns => _columns;
    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private object? _layoutParameters;

    /// <summary>
    /// 행 표시에 영향을 주는 파라미터가 바뀐 경우에만 전체 행을 다시 그린다.
    /// 부모가 이벤트 처리 후 다시 그려질 때마다 모든 행을 그리지 않기 위함 (R4).
    /// </summary>
    protected override void OnParametersSet()
    {
        object layout = (DataSet, Localizer, Culture, FrozenColumns, CurrencyDigits, DeletedRows);
        if (!Equals(layout, _layoutParameters))
        {
            _layoutParameters = layout;
            _epoch++;
        }
    }

    // ── JS 층 등록 (R1·R5): 키보드·포커스·클릭은 quro-grid.js가 그리드 루트 하나에서 받는다 ──

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            var options = new GridJsOptions(
                EnterDirection == EnterDirection.Down ? "down" : "right",
                AddRowOnLastEnter,
                NotifySelection: OnSelectionChanged.HasDelegate);
            await _module.InvokeVoidAsync("init", _root, _self, options);
        }
        else if (_ensureActive && _module is not null)
        {
            _ensureActive = false;
            await _module.InvokeVoidAsync("ensureActive", _root);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _root);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // 회로가 이미 끊긴 경우 (R7): 브라우저 쪽 정리는 필요 없다
        }
        _self?.Dispose();
    }

    // ── 편집 (quro-grid.js가 호출) ──

    /// <summary>편집 시작. 읽기 전용 컬럼이나 삭제 표시 행이면 false.</summary>
    [JSInvokable]
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
    /// 값이 그대로면 이벤트 없이 편집만 닫는다. OnCellValueChanging에서 취소해도 false.
    /// </summary>
    [JSInvokable]
    public async Task<bool> CommitEdit(int rowKey, string field, string? text)
    {
        var col = Column(field);
        if (col.ReadOnly
            || !DataSet.TryParseText(col.Field, text, EffectiveCulture, out var value)
            || !await ApplyValue(rowKey, col.Field, value))
            return false;

        _editing = null;
        StateHasChanged();
        return true;
    }

    // ── 행 추가·삭제·붙여넣기 (단축키·메뉴에서 호출, 일괄 처리는 호출 1회: R3) ──

    /// <summary>행 추가. OnRowAdding에서 기본값을 넣고, 추가 후 OnRowAdded.</summary>
    [JSInvokable]
    public async Task<int> AddRow()
    {
        var key = await AddRowCore();
        StateHasChanged();
        return key;
    }

    /// <summary>
    /// 행 복사 (Addendum A-12): 표시 순서대로 각 행 바로 아래에 같은 값의 새 행. 여러 행도 호출 1회 (R3).
    /// 복사 행도 OnRowAdding을 거치며 <see cref="RowAddingArgs{TItem}.SourceRowKey"/>로 원본을 알린다.
    /// </summary>
    [JSInvokable]
    public async Task<IReadOnlyList<int>> CopyRows(IReadOnlyList<int> rowKeys)
    {
        var copies = new List<int>();
        foreach (var source in DataSet.RowKeys.Where(rowKeys.Contains).ToList())
            copies.Add(await AddRowCore(DataSet.CopyOf(source), source));

        StateHasChanged();
        return copies;
    }

    /// <summary>선택 범위(JS 소유)의 행을 복사한다. 툴바 버튼 등 그리드 밖에서 호출.</summary>
    public async Task<IReadOnlyList<int>> CopySelectedRows() => await CopyRows(await SelectedRowKeys());

    /// <summary>선택 범위(JS 소유)의 행을 삭제한다. 툴바 버튼 등 그리드 밖에서 호출.</summary>
    public async Task<int> DeleteSelectedRows() => await DeleteRows(await SelectedRowKeys());

    private async Task<IReadOnlyList<int>> SelectedRowKeys() =>
        _module is null ? [] : await _module.InvokeAsync<int[]>("selectedRowKeys", _root);

    private async Task<int> AddRowCore(TItem? item = null, int? source = null)
    {
        item ??= Activator.CreateInstance<TItem>();
        await OnRowAdding.InvokeAsync(new RowAddingArgs<TItem>(item, source));

        var key = DataSet.AddRow(item, afterRowKey: source);
        await OnRowAdded.InvokeAsync(new RowAddedArgs<TItem>(key, DataSet.GetItem(key)));
        return key;
    }

    /// <summary>활성 행이 바뀌었을 때 JS가 호출한다 (Addendum A-13, 구독한 경우에만).</summary>
    [JSInvokable]
    public Task SelectionChanged(int rowKey) =>
        OnSelectionChanged.InvokeAsync(new SelectionChangedArgs<TItem>(rowKey, DataSet.GetItem(rowKey)));

    /// <summary>여러 행 삭제. OnRowDeleting은 행마다 발생하고, 취소한 행은 남는다. 삭제한 행 수를 돌려준다.</summary>
    [JSInvokable]
    public async Task<int> DeleteRows(IReadOnlyList<int> rowKeys)
    {
        var deleted = new List<int>();
        foreach (var key in rowKeys)
        {
            if (DataSet.GetState(key) == RowState.Deleted)
                continue;

            var args = new RowDeletingArgs<TItem>(key, DataSet.GetItem(key));
            await OnRowDeleting.InvokeAsync(args);
            if (args.Cancel)
                continue;

            DataSet.DeleteRow(key);
            if (_editing?.RowKey == key)
                _editing = null;
            deleted.Add(key);
        }

        if (deleted.Count > 0)
            await OnRowDeleted.InvokeAsync(new RowDeletedArgs(deleted));

        _ensureActive = deleted.Count > 0;
        StateHasChanged();
        return deleted.Count;
    }

    /// <summary>
    /// 붙여넣기 (엑셀 형식을 행 × 열로 나눈 값, quro-grid.js가 1회 호출: R3). OnPasting에서 취소하면 false.
    /// 시작 셀부터 표시 순서대로 채우고, 셀마다 값 변경 이벤트가 발생한다 (Spec 4.7, Addendum A-03·A-04).
    /// 행이 모자라면 <see cref="AddRowsOnPaste"/>에 따라 추가하고, 열이 넘치면 버린다.
    /// 읽기 전용·해석 실패·변경 취소 셀은 건너뛰고 OnPasted로 사유 키를 넘긴다.
    /// </summary>
    [JSInvokable]
    public async Task<bool> Paste(int startRowKey, string startField, string[][] cells)
    {
        var args = new PastingArgs(startRowKey, startField, cells);
        await OnPasting.InvokeAsync(args);
        if (args.Cancel)
            return false;

        var rows = VisibleRowKeys().ToList();
        var firstRow = rows.IndexOf(startRowKey);
        var firstCol = _columns.IndexOf(Column(startField));
        if (firstRow < 0)
            throw new ArgumentException($"행 키 {startRowKey}가 화면에 없습니다.", nameof(startRowKey));

        var added = new List<int>();
        var skipped = new List<PasteSkip>();
        for (var r = 0; r < cells.Length; r++)
        {
            int rowKey;
            if (firstRow + r < rows.Count)
                rowKey = rows[firstRow + r];
            else if (AddRowsOnPaste)
                added.Add(rowKey = await AddRowCore());
            else
                break;

            for (var c = 0; c < cells[r].Length && firstCol + c < _columns.Count; c++)
            {
                var field = _columns[firstCol + c].Field;
                var reason = await PasteCell(rowKey, _columns[firstCol + c], cells[r][c]);
                if (reason is not null)
                    skipped.Add(new PasteSkip(rowKey, field, reason));
            }
        }

        await OnPasted.InvokeAsync(new PastedArgs(startRowKey, startField, added, skipped));
        StateHasChanged();
        return true;
    }

    /// <summary>셀 하나를 붙여넣는다. 건너뛰면 사유 리소스 키, 반영했으면 null.</summary>
    private async Task<string?> PasteCell(int rowKey, QuroColumn col, string text)
    {
        if (col.ReadOnly || DataSet.GetState(rowKey) == RowState.Deleted)
            return PasteKeys.ReadOnly;
        if (!DataSet.TryParseText(col.Field, text, EffectiveCulture, out var value))
            return PasteKeys.Invalid;
        return await ApplyValue(rowKey, col.Field, value) ? null : PasteKeys.Cancelled;
    }

    /// <summary>OnCellValueChanging → SetValue → OnCellValueChanged. 값이 같으면 아무것도 하지 않는다.</summary>
    private async Task<bool> ApplyValue(int rowKey, string field, object? value)
    {
        var old = DataSet.GetValue(rowKey, field);
        if (Equals(old, value))
            return true;

        var item = DataSet.GetItem(rowKey);
        var changing = new CellValueChangingArgs<TItem>(rowKey, field, item, old, value);
        await OnCellValueChanging.InvokeAsync(changing);
        if (changing.Cancel)
            return false;

        DataSet.SetValue(rowKey, field, value);
        await OnCellValueChanged.InvokeAsync(new CellValueChangedArgs<TItem>(rowKey, field, item, old, value));
        return true;
    }

    [JSInvokable]
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
        RegisterRule(column);
        _epoch++;
    }

    void IColumnHost.ColumnChanged(QuroColumn column)
    {
        RegisterRule(column);
        _epoch++;
    }

    /// <summary>컬럼의 Required·Min·Max를 DataSet 규칙 API로 넘긴다 (Addendum A-10).</summary>
    private void RegisterRule(QuroColumn col)
    {
        if (col.Required || col.Min is not null || col.Max is not null)
            DataSet.SetRule(col.Field, new FieldRule { Required = col.Required, Min = col.Min, Max = col.Max });
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
