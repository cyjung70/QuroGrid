using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web.Virtualization;
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
    private bool _endEdit;        // 선택 목록에서 골라 서버가 편집을 끝냈을 때 JS 편집 상태를 정리한다 (렌더 반영 뒤)
    private readonly string _instanceId = Guid.NewGuid().ToString("N");   // 재연결 후 같은 회로인지 JS가 확인 (A-21)
    private (int RowKey, string Field)? _picker;   // 열린 선택 목록 (Date 달력·Combo 목록, Addendum A-17)
    private DateTime? _pickerDate;
    private readonly List<SortColumn> _sort = [];
    private List<int>? _order;           // 정렬한 화면 순서 (null = 조회 순서). 편집·추가해도 다시 정렬하지 않는다 (A-22)
    private int _orderGeneration;        // 정렬한 시점의 DataSet.Generation. 다시 조회되면 정렬을 다시 적용한다
    private readonly Dictionary<string, int> _widths = [];   // 사용자가 리사이즈한 너비 (A-23)
    private const int MinColumnWidth = 40;

    // 서버측 페이징 (R6, A-24): 화면 위치 = [추가 행(맨 위)] + [서버 조회 순서]. 보이는 범위의 페이지만 적재한다.
    private Virtualize<int>? _virtualize;
    private readonly Dictionary<int, int[]> _pages = [];        // 페이지 번호 → 행 키 (조회 순서)
    private int _pagesGeneration;
    private int? _total;                                         // 서버 전체 건수 (첫 조회 전 null)
    private IReadOnlyDictionary<string, decimal>? _totals;       // 서버 전체 합계
    private readonly Dictionary<int, int> _rowIndex = [];        // 행 키 → 화면 위치 (aria-rowindex)
    private readonly List<int> _staleRows = [];                  // 정렬·다시 조회로 버린 페이지의 행. 새 범위를 받은 뒤 놓는다
    private bool _refreshRows;                                   // 행 구성이 바뀜 → 렌더 뒤 Virtualize 다시 조회

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

    // ── 이벤트 (Spec 4.10, Addendum A-04·A-13·A-14·A-16·A-19) ──

    [Parameter] public EventCallback<CellValueChangingArgs<TItem>> OnCellValueChanging { get; set; }
    [Parameter] public EventCallback<CellValueChangedArgs<TItem>> OnCellValueChanged { get; set; }
    [Parameter] public EventCallback<RowAddingArgs<TItem>> OnRowAdding { get; set; }
    [Parameter] public EventCallback<RowAddedArgs<TItem>> OnRowAdded { get; set; }
    [Parameter] public EventCallback<RowDeletingArgs<TItem>> OnRowDeleting { get; set; }

    /// <summary>삭제 후 (Addendum A-14). 삭제 호출 1회에 한 번.</summary>
    [Parameter] public EventCallback<RowDeletedArgs> OnRowDeleted { get; set; }
    [Parameter] public EventCallback<PastingArgs> OnPasting { get; set; }

    /// <summary>붙여넣기 후: 추가된 행, 건너뛴 셀과 사유 (Addendum A-04).</summary>
    [Parameter] public EventCallback<PastedArgs> OnPasted { get; set; }

    /// <summary>활성 행 변경 (Addendum A-13). 구독한 경우에만 JS가 보낸다.</summary>
    [Parameter] public EventCallback<SelectionChangedArgs<TItem>> OnSelectionChanged { get; set; }

    /// <summary>코드도움 요청 (Spec 4.10, Addendum A-16). 앱이 팝업을 띄우고 <see cref="PopupRequestedArgs{TItem}.Value"/>로 결과를 돌려준다.</summary>
    [Parameter] public EventCallback<PopupRequestedArgs<TItem>> OnPopupRequested { get; set; }

    /// <summary>Button 셀 클릭 (Addendum A-19).</summary>
    [Parameter] public EventCallback<CellButtonClickArgs<TItem>> OnCellButtonClick { get; set; }

    /// <summary>
    /// 미저장 변경이 있으면 페이지 이탈을 확인한다 (R7, Addendum A-20: 기본 켬).
    /// 창 닫기·새로고침은 브라우저 경고, 앱 안 화면 이동은 확인창(또는 <see cref="OnLeaving"/>).
    /// </summary>
    [Parameter] public bool ConfirmLeave { get; set; } = true;

    /// <summary>앱 안 화면 이동 확인을 앱이 직접 할 때 구독한다 (Addendum A-20).</summary>
    [Parameter] public EventCallback<LeavingArgs> OnLeaving { get; set; }

    /// <summary>
    /// 서버측 페이징 조회 함수 (R6, O-04: 화면별 설정, Addendum A-24). 주면 보이는 범위만 PageSize 단위로 적재하고,
    /// 정렬은 조회로 넘긴다. DataSet은 행 ID 선택자로 만들어야 한다. 없으면 DataSet 전체를 그린다.
    /// </summary>
    [Parameter] public Func<GridItemsRequest, Task<GridItemsResult<TItem>>>? ItemsProvider { get; set; }

    /// <summary>한 번에 조회할 행 수 (O-04: 화면별 설정).</summary>
    [Parameter] public int PageSize { get; set; } = 100;

    /// <summary>페이징 중 행 높이 (px). 가상 스크롤이 위치를 계산하는 데 쓴다.</summary>
    [Parameter] public int RowHeight { get; set; } = 28;

    /// <summary>그리드 높이 (CSS 값, 예: 480px). 주면 그리드 안에서 스크롤한다 (페이징 화면 권장).</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>다시 조회한다 (저장 후 등). 페이징 중이면 변경 없는 적재 행을 놓고 처음부터 다시 부른다.</summary>
    public Task ReloadAsync()
    {
        if (Paging)
        {
            _total = null;
            ResetPages();
        }
        StateHasChanged();
        return Task.CompletedTask;
    }

    private bool Paging => ItemsProvider is not null;

    /// <summary>사용자가 헤더를 눌러 정렬을 바꾼 뒤 (Addendum A-22).</summary>
    [Parameter] public EventCallback<SortChangedArgs> OnSortChanged { get; set; }

    /// <summary>컬럼 경계 드래그가 끝난 뒤 (Addendum A-23).</summary>
    [Parameter] public EventCallback<ColumnResizedArgs> OnColumnResized { get; set; }

    /// <summary>현재 정렬 (1차·2차… 순서). 비어 있으면 조회 순서.</summary>
    public IReadOnlyList<SortColumn> SortColumns => _sort.ToList();

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
        if (_refreshRows && _virtualize is not null)
        {
            _refreshRows = false;
            await _virtualize.RefreshDataAsync();
        }

        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            var options = new GridJsOptions(
                EnterDirection == EnterDirection.Down ? "down" : "right",
                AddRowOnLastEnter,
                NotifySelection: OnSelectionChanged.HasDelegate,
                InstanceId: _instanceId,
                Paging: Paging);
            await _module.InvokeVoidAsync("init", _root, _self, options);
        }
        else if (_module is not null)
        {
            if (_ensureActive)
            {
                _ensureActive = false;
                await _module.InvokeVoidAsync("ensureActive", _root);
            }
            if (_endEdit)
            {
                _endEdit = false;
                await _module.InvokeVoidAsync("endEdit", _root);
            }
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

    /// <summary>편집 시작. 읽기 전용 컬럼, 삭제 표시 행, 에디터가 없는 셀(Check·Button)이면 false.</summary>
    [JSInvokable]
    public bool BeginEdit(int rowKey, string field)
    {
        var col = Column(field);
        if (!Editable(rowKey, col) || col.Type is CellType.Check or CellType.Button)
            return false;

        _editing = (rowKey, col.Field);
        _picker = null;
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// 셀 이탈 시 1회 호출 (R2). 문자열은 <see cref="EffectiveCulture"/>로 해석한다.
    /// 해석에 실패하면 값을 바꾸지 않고 편집 상태를 유지한 채 false.
    /// 값이 그대로면 이벤트 없이 편집만 닫는다. OnCellValueChanging에서 취소해도 false.
    /// </summary>
    /// <remarks>
    /// Popup 셀은 먼저 컬럼 Lookup으로 조회한다 (GS-006, Addendum A-16): 1건이면 그 코드로 확정,
    /// 여러 건이면 OnPopupRequested로 앱이 고른 코드로 확정(고르지 않으면 편집 유지), 0건이면 편집 유지 (A-05).
    /// 빈 입력은 조회 없이 확정한다 (코드 지우기, 잠정).
    /// </remarks>
    [JSInvokable]
    public async Task<bool> CommitEdit(int rowKey, string field, string? text)
    {
        var col = Column(field);
        if (col.ReadOnly)
            return false;

        if (col.Type == CellType.Popup && col.Lookup is not null && !string.IsNullOrEmpty(text))
        {
            var matches = await col.Lookup(text);
            text = matches.Count switch
            {
                0 => null,
                1 => matches[0],
                _ => await RaisePopup(rowKey, col, text, matches, PopupTrigger.Commit),
            };
            if (text is null)
                return false;
        }

        if (!TryParseCell(col, text, out var value) || !await ApplyValue(rowKey, col.Field, value))
            return false;

        EndEditing();
        return true;
    }

    /// <summary>
    /// F4 코드도움 (GS-046). Popup 셀이면 Lookup 결과(후보)와 함께 OnPopupRequested를 발생시킨다.
    /// 1건 일치여도 자동 확정하지 않는다. 앱이 고른 코드로 확정했으면 true (편집 중이었다면 편집 종료).
    /// </summary>
    [JSInvokable]
    public async Task<bool> RequestPopup(int rowKey, string field, string? text)
    {
        var col = Column(field);
        if (col.Type != CellType.Popup || !Editable(rowKey, col))
            return false;

        text ??= "";
        var matches = col.Lookup is null ? [] : await col.Lookup(text);
        var chosen = await RaisePopup(rowKey, col, text, matches, PopupTrigger.F4);
        if (chosen is null
            || !TryParseCell(col, chosen, out var value)
            || !await ApplyValue(rowKey, col.Field, value))
            return false;

        EndEditing();
        return true;
    }

    private async Task<string?> RaisePopup(int rowKey, QuroColumn col, string text, IReadOnlyList<string> matches, PopupTrigger trigger)
    {
        var args = new PopupRequestedArgs<TItem>(rowKey, col.Field, DataSet.GetItem(rowKey), text, matches, trigger);
        await OnPopupRequested.InvokeAsync(args);
        return args.Value;
    }

    /// <summary>
    /// 편집 중 선택 목록 열기 (Alt+↓, Addendum A-17): Date는 달력(MudDatePicker), Combo는 목록(MudList).
    /// 직접 입력은 브라우저 에디터에서 하고(R2), 목록에서 고르면 바로 확정한다.
    /// </summary>
    [JSInvokable]
    public bool OpenPicker(int rowKey, string field, string? text)
    {
        var col = Column(field);
        if (col.Type is not (CellType.Date or CellType.Combo) || !Editable(rowKey, col))
            return false;

        if (col.Type == CellType.Date)
        {
            // 입력 중인 문자열을 우선, 해석할 수 없으면 현재 값으로 달력을 연다
            var value = DataSet.TryParseText(col.Field, text, EffectiveCulture, out var typed) && typed is not null
                ? typed
                : DataSet.GetValue(rowKey, col.Field);
            _pickerDate = value switch
            {
                DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                DateTime d => d,
                _ => null,
            };
        }

        _editing = (rowKey, col.Field);
        _picker = (rowKey, col.Field);
        StateHasChanged();
        return true;
    }

    internal DateTime? PickerDate => _pickerDate;

    internal Task PickDate(DateTime? date)
    {
        if (_picker is not { } p)
            return Task.CompletedTask;
        object? value = null;
        if (date is { } d)
            DataSet.TryParseText(p.Field, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out value);
        return PickValue(value);
    }

    internal Task PickOption(int index) =>
        _picker is { } p && Column(p.Field).Options is { } options && index >= 0 && index < options.Count
            ? PickValue(options[index].Value)
            : Task.CompletedTask;

    private async Task PickValue(object? value)
    {
        var (rowKey, field) = _picker!.Value;
        _picker = null;
        if (await ApplyValue(rowKey, field, value))
        {
            _editing = null;
            _endEdit = true;
        }
        StateHasChanged();
    }

    /// <summary>Check 셀 토글 (클릭·Space, GS-062). 매핑 값(CheckedValue/UncheckedValue)을 저장한다.</summary>
    [JSInvokable]
    public async Task<bool> ToggleCheck(int rowKey, string field)
    {
        var col = Column(field);
        if (col.Type != CellType.Check || !Editable(rowKey, col))
            return false;

        var ok = await ApplyValue(rowKey, col.Field, IsChecked(rowKey, col) ? col.UncheckedValue : col.CheckedValue);
        StateHasChanged();
        return ok;
    }

    /// <summary>Button 셀 클릭 (클릭·Enter·Space, GS-063). 삭제 표시 행에서는 발생하지 않는다.</summary>
    [JSInvokable]
    public async Task<bool> ClickButton(int rowKey, string field)
    {
        var col = Column(field);
        if (col.Type != CellType.Button || DataSet.GetState(rowKey) == RowState.Deleted)
            return false;

        await OnCellButtonClick.InvokeAsync(new CellButtonClickArgs<TItem>(rowKey, col.Field, DataSet.GetItem(rowKey)));
        return true;
    }

    private bool Editable(int rowKey, QuroColumn col) =>
        !col.ReadOnly && col.Type != CellType.Button && DataSet.GetState(rowKey) != RowState.Deleted;

    private void EndEditing()
    {
        _editing = null;
        _picker = null;
        StateHasChanged();
    }

    /// <summary>
    /// 입력·붙여넣기 문자열을 셀 값으로. Combo는 코드나 명칭, Check는 매핑 값이나 true/false (대소문자 무시),
    /// 그 밖은 DataSet이 사용자 로캘로 해석한다.
    /// </summary>
    private bool TryParseCell(QuroColumn col, string? text, out object? value)
    {
        value = null;
        switch (col.Type)
        {
            case CellType.Button:
                return false;
            case CellType.Combo when !string.IsNullOrEmpty(text):
                var option = col.Options?.FirstOrDefault(o => Same(o.Value?.ToString(), text) || Same(o.Text, text));
                value = option?.Value;
                return option is not null;
            case CellType.Check:
                if (Same(text, col.CheckedValue?.ToString()) || Same(text, bool.TrueString))
                    value = col.CheckedValue;
                else if (Same(text, col.UncheckedValue?.ToString()) || Same(text, bool.FalseString) || string.IsNullOrEmpty(text))
                    value = col.UncheckedValue;
                else
                    return false;
                return true;
            default:
                return DataSet.TryParseText(col.Field, text, EffectiveCulture, out value);
        }
    }

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    // ── 행 추가·삭제·붙여넣기 (단축키·메뉴에서 호출, 일괄 처리는 호출 1회: R3) ──

    /// <summary>
    /// 행 추가. OnRowAdding에서 기본값을 넣고, 추가 후 OnRowAdded.
    /// <paramref name="afterRowKey"/>가 있으면 그 행 바로 아래 (Insert 키: 활성 행 아래, A-15), 없으면 목록 끝.
    /// </summary>
    [JSInvokable]
    public async Task<int> AddRow(int? afterRowKey = null)
    {
        var key = await AddRowCore(after: afterRowKey);
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
        foreach (var source in OrderedRowKeys().Where(rowKeys.Contains).ToList())
            copies.Add(await AddRowCore(DataSet.CopyOf(source), source, after: source));

        StateHasChanged();
        return copies;
    }

    /// <summary>선택 범위(JS 소유)의 행을 복사한다. 툴바 버튼 등 그리드 밖에서 호출.</summary>
    public async Task<IReadOnlyList<int>> CopySelectedRows() => await CopyRows(await SelectedRowKeys());

    /// <summary>
    /// 화면 위치 범위 [<paramref name="fromIndex"/>, <paramref name="toIndex"/>]의 행을 삭제한다 (호출 1회, R3, Addendum A-25).
    /// 페이징 중 적재되지 않은 범위는 조회한 뒤 삭제한다.
    /// </summary>
    [JSInvokable]
    public async Task<int> DeleteRowRange(int fromIndex, int toIndex)
    {
        var (from, to) = (Math.Min(fromIndex, toIndex), Math.Max(fromIndex, toIndex));
        var keys = Paging
            ? await PagedRowsBetween(from, to)
            : VisibleRowKeys().Skip(from).Take(to - from + 1).ToList();
        return await DeleteRows(keys);
    }

    /// <summary>
    /// 선택 범위(JS 소유)의 행을 삭제한다. 툴바 버튼 등 그리드 밖에서 호출.
    /// 페이징 중에는 화면 밖까지 이어진 범위도 위치로 받아 삭제한다 (A-25).
    /// </summary>
    public async Task<int> DeleteSelectedRows()
    {
        if (!Paging)
            return await DeleteRows(await SelectedRowKeys());
        var range = _module is null ? null : await _module.InvokeAsync<int[]?>("selectedRange", _root);
        return range is [var from, var to] ? await DeleteRowRange(from, to) : 0;
    }

    private async Task<IReadOnlyList<int>> SelectedRowKeys() =>
        _module is null ? [] : await _module.InvokeAsync<int[]>("selectedRowKeys", _root);

    private async Task<int> AddRowCore(TItem? item = null, int? source = null, int? after = null)
    {
        item ??= Activator.CreateInstance<TItem>();
        await OnRowAdding.InvokeAsync(new RowAddingArgs<TItem>(item, source));

        if (Paging)
            after = null;   // 페이징 중 추가 행은 맨 위에 모은다 (A-24)
        OrderedRowKeys();   // 화면 순서를 맞춘 뒤 넣는다
        var key = DataSet.AddRow(item, afterRowKey: after);
        PlaceInOrder(key, after);
        RefreshRows();
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
        RefreshRows();
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

        var rows = Paging ? await PagedRowsFrom(startRowKey, cells.Length) : VisibleRowKeys().ToList();
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
        if (!Editable(rowKey, col))
            return PasteKeys.ReadOnly;
        if (!TryParseCell(col, text, out var value))
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
    public void CancelEdit() => EndEditing();

    // ── 정렬 (GS-050, Addendum A-22): 적재된 행의 화면 순서만 바꾼다. DataSet 순서·상태는 그대로 ──

    /// <summary>
    /// 헤더 클릭 (JS가 호출, 호출 1회). 오름 → 내림 → 해제 순환.
    /// <paramref name="additive"/>(Shift+클릭)면 기존 정렬에 2차·3차로 더하거나 그 컬럼만 순환한다.
    /// </summary>
    [JSInvokable]
    public async Task Sort(string field, bool additive)
    {
        if (Column(field).Type == CellType.Button)
            return;

        var index = _sort.FindIndex(s => s.Field == field);
        SortColumn? next = index < 0 ? new(field, SortDirection.Ascending)
            : _sort[index].Direction == SortDirection.Ascending ? new(field, SortDirection.Descending)
            : null;

        if (!additive)
        {
            _sort.Clear();
            if (next is not null)
                _sort.Add(next);
        }
        else if (index < 0)
            _sort.Add(next!);
        else if (next is null)
            _sort.RemoveAt(index);
        else
            _sort[index] = next;

        SortChanged();
        await OnSortChanged.InvokeAsync(new SortChangedArgs(SortColumns));
        StateHasChanged();
    }

    /// <summary>앱에서 정렬을 지정한다 (OnSortChanged는 발생하지 않음). 빈 목록이면 조회 순서.</summary>
    public void SetSort(IReadOnlyList<SortColumn> sort)
    {
        _sort.Clear();
        _sort.AddRange(sort.Where(s => Column(s.Field).Type != CellType.Button));
        SortChanged();
        StateHasChanged();
    }

    public void ClearSort() => SetSort([]);

    /// <summary>정렬이 바뀜: 페이징이면 정렬 조건으로 다시 조회, 아니면 메모리 정렬.</summary>
    private void SortChanged()
    {
        if (Paging)
            ResetPages();
        else
            ApplySort();
    }

    private void ApplySort()
    {
        _orderGeneration = DataSet.Generation;
        _order = _sort.Count == 0
            ? null
            : DataSet.RowKeys.OrderBy(k => k, Comparer<int>.Create(CompareRows)).ToList();   // 안정 정렬: 같으면 조회 순서
    }

    private int CompareRows(int a, int b)
    {
        foreach (var s in _sort)
        {
            var col = Column(s.Field);
            var result = CompareValues(SortValue(a, col), SortValue(b, col));
            if (result != 0)
                return s.Direction == SortDirection.Ascending ? result : -result;
        }
        return 0;
    }

    /// <summary>Combo는 화면에 보이는 명칭으로, 그 밖은 값으로 정렬한다.</summary>
    private object? SortValue(int rowKey, QuroColumn col)
    {
        var value = DataSet.GetValue(rowKey, col.Field);
        return col.Type == CellType.Combo ? OptionText(col, value) : value;
    }

    /// <summary>빈 값이 가장 작다. 문자열은 표시 로캘 기준(대소문자 무시).</summary>
    private int CompareValues(object? x, object? y) => (x, y) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        (string sx, string sy) => EffectiveCulture.CompareInfo.Compare(sx, sy, CompareOptions.IgnoreCase),
        _ => Comparer<object>.Default.Compare(x, y),
    };

    /// <summary>
    /// 화면 순서의 행 키 (삭제 표시 행 포함). 정렬 중이면 정렬한 순서를 유지하고, 사라진 행은 빼고,
    /// 그리드 밖에서 추가된 행은 끝에 둔다. 다시 조회되면 정렬을 다시 적용한다.
    /// </summary>
    private List<int> OrderedRowKeys()
    {
        if (_order is null)
            return DataSet.RowKeys.ToList();
        if (_orderGeneration != DataSet.Generation)
            ApplySort();

        var current = DataSet.RowKeys;
        var exists = current.ToHashSet();
        _order!.RemoveAll(k => !exists.Contains(k));
        var known = _order.ToHashSet();
        _order.AddRange(current.Where(k => !known.Contains(k)));
        return _order;
    }

    /// <summary>그리드가 추가한 행을 화면 순서에도 넣는다: <paramref name="after"/> 바로 아래, 없으면 끝.</summary>
    private void PlaceInOrder(int rowKey, int? after)
    {
        if (_order is null)
            return;
        _order.Remove(rowKey);
        var index = after is { } a ? _order.IndexOf(a) : -1;
        if (index < 0)
            _order.Add(rowKey);
        else
            _order.Insert(index + 1, rowKey);
    }

    internal SortColumn? SortOf(QuroColumn col) => _sort.Find(s => s.Field == col.Field);

    private static string? AriaSort(SortColumn? sort) => sort?.Direction switch
    {
        SortDirection.Ascending => "ascending",
        SortDirection.Descending => "descending",
        _ => null,
    };

    internal string? SortOrder(QuroColumn col) =>
        _sort.Count > 1 ? (_sort.FindIndex(s => s.Field == col.Field) + 1).ToString(CultureInfo.InvariantCulture) : null;

    // ── 서버측 페이징 (GS-051·GS-052, R6, Addendum A-24) ──

    /// <summary>Virtualize가 보이는 범위를 요청한다. 필요한 페이지만 조회하고, 범위에서 먼 페이지는 놓는다.</summary>
    private async ValueTask<ItemsProviderResult<int>> ProvideRows(ItemsProviderRequest request)
    {
        if (_pagesGeneration != DataSet.Generation)
        {
            _pages.Clear();   // 앱이 다시 Load함: 이전 행 키는 이미 없다
            _pagesGeneration = DataSet.Generation;
        }

        var inserted = InsertedRowKeys();
        var end = request.StartIndex + request.Count;
        var keys = new List<int>(request.Count);
        for (var v = request.StartIndex; v < Math.Min(end, inserted.Count); v++)
            keys.Add(inserted[v]);

        var from = Math.Max(0, request.StartIndex - inserted.Count);
        var to = end - inserted.Count;
        if (to > from)
        {
            await EnsureLoaded(from, to, request.CancellationToken);
            keys.AddRange(ServerRowKeys(from, to));
            ReleaseOutside(from, to, keys);
        }

        for (var i = 0; i < keys.Count; i++)
            _rowIndex[keys[i]] = request.StartIndex + i;
        return new ItemsProviderResult<int>(keys, inserted.Count + (_total ?? 0));
    }

    /// <summary>서버 위치 [from, to)를 덮는 페이지 중 없는 것을 조회해 DataSet에 합친다 (페이지마다 1회).</summary>
    private async Task EnsureLoaded(int from, int to, CancellationToken cancellationToken)
    {
        for (var page = from / PageSize; page * PageSize < to; page++)
        {
            if (_total is { } total && page * PageSize >= total)
                break;
            if (_pages.ContainsKey(page))
                continue;

            var result = await ItemsProvider!(new GridItemsRequest(page * PageSize, PageSize, SortColumns, cancellationToken));
            var totalsChanged = !Equals(_totals, result.Totals) || _total != result.TotalCount;
            _total = result.TotalCount;
            _totals = result.Totals;
            _pages[page] = DataSet.LoadPage(result.Items).ToArray();
            if (totalsChanged)
                StateHasChanged();   // 합계 행은 Virtualize 밖이라 그리드가 다시 그린다
        }
    }

    private IEnumerable<int> ServerRowKeys(int from, int to)
    {
        for (var i = from; i < Math.Min(to, _total ?? 0); i++)
            if (_pages.TryGetValue(i / PageSize, out var page) && i % PageSize < page.Length)
                yield return page[i % PageSize];
    }

    /// <summary>요청 범위 앞뒤 한 페이지만 남기고 놓는다 (R6). 변경 행은 DataSet이 남긴다.</summary>
    private void ReleaseOutside(int from, int to, IReadOnlyCollection<int> shown)
    {
        var first = from / PageSize - 1;
        var last = (to - 1) / PageSize + 1;
        var drop = _pages.Keys.Where(p => p < first || p > last).ToList();
        var release = drop.SelectMany(p => _pages[p]).Concat(_staleRows).ToList();
        foreach (var page in drop)
            _pages.Remove(page);
        _staleRows.Clear();

        var keep = _pages.Values.SelectMany(k => k).Concat(shown).ToHashSet();
        if (_editing is { } editing)
            keep.Add(editing.RowKey);
        DataSet.Release(release.Where(k => !keep.Contains(k)));
        foreach (var key in release.Where(k => !keep.Contains(k)))
            _rowIndex.Remove(key);
    }

    /// <summary>정렬·다시 조회: 페이지를 버리고 처음부터 다시 부른다. 행은 새 범위를 받은 뒤 놓는다 (그리는 중인 행 보호).</summary>
    private void ResetPages()
    {
        _staleRows.AddRange(DataSet.RowKeys);   // 변경 없는 적재 행 전부 (저장 후 남은 행 포함). 변경 행은 DataSet이 남긴다
        _pages.Clear();
        RefreshRows();
    }

    private void RefreshRows() => _refreshRows = Paging;

    /// <summary>페이징 중 추가 행 (화면 맨 위, 추가 순서).</summary>
    private List<int> InsertedRowKeys() =>
        DataSet.RowKeys.Where(k => DataSet.GetState(k) == RowState.Inserted).ToList();

    internal int RowIndexOf(int rowKey) => _rowIndex.GetValueOrDefault(rowKey);

    /// <summary>
    /// 페이징 중 붙여넣기 대상 행: 시작 행부터 화면 순서로 <paramref name="count"/>행.
    /// 적재되지 않은 범위는 조회해서 채운다 (A-24, 붙여넣기 호출 1회 안에서).
    /// </summary>
    private async Task<List<int>> PagedRowsFrom(int startRowKey, int count)
    {
        if (!_rowIndex.TryGetValue(startRowKey, out var start))
            throw new ArgumentException($"행 키 {startRowKey}가 화면에 없습니다.", nameof(startRowKey));
        return await PagedRowsBetween(start, start + count - 1);
    }

    /// <summary>페이징 중 화면 위치 [first, last]의 행 키. 적재되지 않은 범위는 조회한다.</summary>
    private async Task<List<int>> PagedRowsBetween(int first, int last)
    {
        var inserted = InsertedRowKeys();
        var keys = inserted.Skip(first).Take(last - first + 1).ToList();
        var from = Math.Max(0, first - inserted.Count);
        var to = last + 1 - inserted.Count;
        if (to > from)
        {
            await EnsureLoaded(from, to, CancellationToken.None);
            keys.AddRange(ServerRowKeys(from, to));
        }
        return keys;
    }

    private decimal PendingDelta(string field)
    {
        var delta = 0m;
        foreach (var key in DataSet.RowKeys)
        {
            delta += DataSet.GetState(key) switch
            {
                RowState.Inserted => Number(DataSet.GetValue(key, field)),
                RowState.Updated => Number(DataSet.GetValue(key, field)) - Number(DataSet.GetOriginal(key, field)),
                RowState.Deleted => -Number(DataSet.GetOriginal(key, field)),
                _ => 0m,
            };
        }
        return delta;
    }

    private static decimal Number(object? value) =>
        value is null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    internal string? RootStyle =>
        Paging || Height is not null
            ? $"{(Height is null ? "" : $"height:{Height};")}--quro-row-height:{RowHeight}px"
            : null;

    // ── 컬럼 리사이즈 (GS-053, Addendum A-23): 드래그는 JS가, 끝날 때 1회 호출 (R1) ──

    /// <summary>경계 드래그가 끝났을 때 JS가 호출한다. 최소 40px.</summary>
    [JSInvokable]
    public async Task ColumnResized(string field, int width)
    {
        var col = Column(field);
        width = Math.Max(MinColumnWidth, width);
        _widths[col.Field] = width;
        _epoch++;   // 모든 행의 셀 너비가 바뀐다
        await OnColumnResized.InvokeAsync(new ColumnResizedArgs(col.Field, width));
        StateHasChanged();
    }

    internal int WidthOf(QuroColumn col) => _widths.TryGetValue(col.Field, out var width) ? width : col.Width;

    /// <summary>재연결 후 JS가 호출한다. 같은 회로(같은 그리드 인스턴스)면 init 때 받은 값과 같다 (A-21).</summary>
    [JSInvokable]
    public string InstanceId() => _instanceId;

    /// <summary>
    /// 앱 안 화면 이동 전 (Addendum A-20). 미저장 변경이 있으면 OnLeaving(구독 시) 또는 확인창으로 묻고,
    /// 취소하면 이동을 막는다.
    /// </summary>
    private async Task ConfirmInternalNavigation(LocationChangingContext context)
    {
        if (!ConfirmLeave || !DataSet.HasChanges)
            return;

        bool leave;
        if (OnLeaving.HasDelegate)
        {
            var args = new LeavingArgs(context.TargetLocation);
            await OnLeaving.InvokeAsync(args);
            leave = !args.Cancel;
        }
        else
        {
            leave = _module is not null && await _module.InvokeAsync<bool>("confirmLeave", _root, Text(GridKeys.LeaveUnsaved));
        }

        if (!leave)
            context.PreventNavigation();
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
        if (col.Type != CellType.Button && (col.Required || col.Min is not null || col.Max is not null)
)
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
            ? OrderedRowKeys().Where(k => DataSet.GetState(k) != RowState.Deleted)
            : OrderedRowKeys();

    internal string Text(string key) => Localizer is null ? key : Localizer[key].Value;

    internal string CellClass(int index, string kind) =>
        index < FrozenColumns ? $"quro-cell {kind} quro-frozen" : $"quro-cell {kind}";

    internal string CellStyle(int index)
    {
        var width = $"width:{WidthOf(_columns[index])}px";
        if (index >= FrozenColumns)
            return width;
        var left = _columns.Take(index).Sum(WidthOf);
        return $"{width};left:{left}px";
    }

    internal string DisplayText(int rowKey, QuroColumn col)
    {
        if (col.Type is CellType.Button or CellType.Check)
            return "";   // Button은 바인딩 값이 없고, Check는 체크 표시로 그린다

        var value = DataSet.GetValue(rowKey, col.Field);
        return col.Type switch
        {
            CellType.Currency => FormatNumber(value, CurrencyFormat(col, rowKey)),
            CellType.Combo => OptionText(col, value),
            _ => Format(value, col),
        };
    }

    internal bool IsPickerOpen(int rowKey) => _picker?.RowKey == rowKey;

    internal int PickerIndex(QuroColumn col) =>
        _picker is { } p && col.Options is { } options
            ? options.ToList().FindIndex(o => Equals(o.Value, DataSet.GetValue(p.RowKey, col.Field)))
            : -1;

    internal bool IsChecked(int rowKey, QuroColumn col) => Equals(DataSet.GetValue(rowKey, col.Field), col.CheckedValue);

    /// <summary>복사(Ctrl+C)용 값. 표시 문자열과 다른 셀(Check·Button)만, 그 밖은 null.</summary>
    internal string? CopyText(int rowKey, QuroColumn col) => col.Type switch
    {
        CellType.Check => DataSet.GetValue(rowKey, col.Field)?.ToString() ?? "",
        CellType.Button => "",
        _ => null,
    };

    /// <summary>JS가 셀 종류별 키 처리(Space 토글, F4, Alt+↓)를 하도록 표시. Text·Number·Currency는 없음.</summary>
    internal string? TypeAttribute(QuroColumn col) => col.Type switch
    {
        CellType.Date => "date",
        CellType.Combo => "combo",
        CellType.Check => "check",
        CellType.Popup => "popup",
        CellType.Button => "button",
        _ => null,
    };

    internal CellMark? Mark(int rowKey, QuroColumn col) =>
        col.Type == CellType.Button ? null : DataSet.GetCellMark(rowKey, col.Field);

    private static string OptionText(QuroColumn col, object? value) =>
        col.Options?.FirstOrDefault(o => Equals(o.Value, value))?.Text ?? value?.ToString() ?? "";

    /// <summary>에디터 초기 값. 천 단위 구분 없이 사용자 로캘로. Combo는 명칭.</summary>
    internal string EditText(int rowKey, QuroColumn col) =>
        DataSet.GetValue(rowKey, col.Field) switch
        {
            var v when col.Type == CellType.Combo => OptionText(col, v),
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
        decimal sum;
        if (Paging)
        {
            // 적재되지 않은 행이 있으므로 서버 합계 + 저장 전 변경분 (A-24). 서버 합계가 없으면 비운다
            if (_totals is null || !_totals.TryGetValue(col.Field, out var total))
                return "";
            sum = total + PendingDelta(col.Field);
        }
        else
            sum = keys.Sum(k => Number(DataSet.GetValue(k, col.Field)));
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
