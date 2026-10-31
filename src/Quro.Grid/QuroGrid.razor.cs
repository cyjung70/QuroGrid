using System.Globalization;
using System.Text.Json;
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

    private readonly List<QuroColumn> _columns = [];   // 정의 순서 (그룹 안의 컬럼은 그룹 자리에)
    private readonly List<object> _definition = [];    // 정의 트리의 맨 위 (컬럼·그룹). 그룹 안 컬럼은 형제보다 늦게 등록되어 트리로 순서를 지킨다
    private (int RowKey, string Field)? _editing;
    private int _epoch;   // 그리드 전체를 다시 그려야 할 때 증가 (파라미터·컬럼 변경)

    /// <summary>모든 행을 다시 그리게 하고, 화면 컬럼 구성을 다시 계산하게 한다.</summary>
    private void Relayout()
    {
        _epoch++;
        _shown = null;
    }

    private const string ModulePath = "./_content/Quro.Grid/quro-grid.js";
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<QuroGrid<TItem>>? _self;
    private bool _ensureActive;   // 행 삭제 후 JS에 활성 셀을 남은 행으로 옮기게 한다 (렌더 반영 뒤)
    private QuroDataSet<TItem>? _bound;   // CurrentRowChanged를 구독한 DataSet (A-51)
    private int? _activateRow;            // 앱이 옮긴 현재 행: 렌더 반영 뒤 JS 활성 셀을 그 행으로
    private bool _movingFromGrid;         // 그리드가 옮긴 현재 행은 JS로 되돌려 보내지 않는다
    private bool _endEdit;        // 선택 목록에서 골라 서버가 편집을 끝냈을 때 JS 편집 상태를 정리한다 (렌더 반영 뒤)
    private int _batch;   // 일괄 처리(붙여넣기·여러 행 삭제·행 복사) 중이면 그리드를 다시 그리지 않는다 (R4)
    private readonly string _instanceId = Guid.NewGuid().ToString("N");   // 재연결 후 같은 회로인지 JS가 확인 (A-21)
    private (int RowKey, string Field)? _picker;   // 열린 선택 목록 (Date 달력·Combo 목록, Addendum A-17)
    private DateTime? _pickerDate;
    private readonly List<SortColumn> _sort = [];
    private List<int>? _order;           // 정렬한 화면 순서 (null = 조회 순서). 편집·추가해도 다시 정렬하지 않는다 (A-22)
    private int _orderGeneration;        // 정렬한 시점의 DataSet.Generation. 다시 조회되면 정렬을 다시 적용한다
    private readonly Dictionary<string, int> _widths = [];   // 사용자가 리사이즈한 너비 (A-23)
    private List<string>? _columnOrder;                       // 사용자가 옮긴 컬럼 순서 (필드, 숨긴 컬럼 포함). null = 정의 순서 (GS-068)
    private readonly HashSet<string> _hidden = [];            // 숨긴 컬럼 (GS-067)
    private List<QuroColumn>? _shown;                         // 화면에 보이는 컬럼 (화면 순서). 컬럼 구성이 바뀌면 null
    private int _frozenShown;                                 // 그중 앞쪽 고정 열 수
    private int _groupDepth;                                  // 보이는 컬럼의 그룹 단 수 (다단 헤더 행 수, GS-070)
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

    // 헤더 필터 (GS-071~GS-073, A-31)
    private readonly List<ColumnFilter> _filters = [];
    private readonly HashSet<int> _filterOut = [];               // 메모리 모드: 적용 시점에 걸러진 행
    private int _filterGeneration;                               // 거른 시점의 DataSet.Generation. 다시 조회되면 다시 거른다
    private string? _filterField;                                // 열린 필터 패널의 컬럼
    private FilterInput? _filterDraft;                           // 해석에 실패한 입력 (패널에 그대로 남긴다)
    private string? _filterInvalid;                              // 해석에 실패한 입력 이름 (data-filter)

    // 그룹핑 (GS-074·GS-075, A-32·A-42)
    private readonly List<string> _groupBy = [];
    private readonly Dictionary<int, object?[]> _groupOf = [];   // 행 키 → 그룹을 정한 시점의 값. 편집해도 다른 그룹으로 옮기지 않는다
    private int _groupGeneration;                                // 그룹을 정한 시점의 DataSet.Generation
    private readonly HashSet<string> _collapsed = [];            // 접힌 그룹 ID

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Rows, row states and validation behind the grid.</summary>
    [Parameter, EditorRequired] public QuroDataSet<TItem> DataSet { get; set; } = default!;
    /// <summary>Column definitions (QuroColumn, QuroColumnGroup).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>헤더·메시지 리소스 (Spec 4.8). 없으면 키를 그대로 표시한다.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    /// <summary>표시·입력 해석 로캘. 기본은 <see cref="CultureInfo.CurrentCulture"/>.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Number of columns frozen on the left.</summary>
    [Parameter] public int FrozenColumns { get; set; }
    /// <summary>Shows the footer (totals) row.</summary>
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

    /// <summary>Before a value is committed. Set Cancel to keep the old value.</summary>
    [Parameter] public EventCallback<CellValueChangingArgs<TItem>> OnCellValueChanging { get; set; }
    /// <summary>After a value is committed.</summary>
    [Parameter] public EventCallback<CellValueChangedArgs<TItem>> OnCellValueChanged { get; set; }
    /// <summary>Before a row is added. Fill default values here.</summary>
    [Parameter] public EventCallback<RowAddingArgs<TItem>> OnRowAdding { get; set; }
    /// <summary>After a row is added.</summary>
    [Parameter] public EventCallback<RowAddedArgs<TItem>> OnRowAdded { get; set; }
    /// <summary>Before each row is deleted. Set Cancel to keep the row.</summary>
    [Parameter] public EventCallback<RowDeletingArgs<TItem>> OnRowDeleting { get; set; }

    /// <summary>삭제 후 (Addendum A-14). 삭제 호출 1회에 한 번.</summary>
    [Parameter] public EventCallback<RowDeletedArgs> OnRowDeleted { get; set; }
    /// <summary>Before a paste is applied. Set Cancel to skip it.</summary>
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

    /// <summary>
    /// 엑셀 내보내기 요청 (GS-054, Addendum A-28). 파일은 저장 API 서버가 만들고, 그리드는 현재 화면의
    /// 컬럼(화면 순서, 사용자 언어 헤더, 너비)과 정렬만 준다. Button 셀은 값이 없어 뺀다.
    /// CSV도 같은 요청에 형식만 다르다 (GS-079, A-34·A-43).
    /// </summary>
    /// <param name="format">만들 파일 형식 (기본 xlsx)</param>
    public GridExportRequest GetExportRequest(ExportFormat format = ExportFormat.Xlsx) => new(
        Columns.Where(c => c.Type != CellType.Button && c.Compute is null)   // the server can't run a Compute function (A-59)
            .Select(c => new ExportColumn(c.Field, c.HeaderKey, Text(c.HeaderKey), c.Type, c.Format, WidthOf(c),
                c.CurrencyField, c.Aggregate, AllOptions(c)))
            .ToList(),
        SortColumns,
        EffectiveCulture.Name,
        Filters,
        format);

    /// <summary>화면에 보이는 컬럼 (화면 순서: 고정 열 → 일반 열, 숨긴 컬럼 제외). 렌더·붙여넣기·내보내기가 쓴다.</summary>
    internal IReadOnlyList<QuroColumn> Columns
    {
        get
        {
            if (_shown is null)
            {
                _shown = OrderedColumns().Where(c => !_hidden.Contains(c.Field)).ToList();
                _frozenShown = _shown.Count(IsFrozen);
                _groupDepth = _shown.Select(c => GroupPath(c).Count).DefaultIfEmpty(0).Max();
            }
            return _shown;
        }
    }
    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private object? _layoutParameters;

    /// <summary>
    /// 행 표시에 영향을 주는 파라미터가 바뀐 경우에만 전체 행을 다시 그린다.
    /// 부모가 이벤트 처리 후 다시 그려질 때마다 모든 행을 그리지 않기 위함 (R4).
    /// </summary>
    protected override void OnParametersSet()
    {
        BindDataSet();
        ApplyDefinition();
        object layout = (DataSet, Localizer, Culture, FrozenColumns, CurrencyDigits, DeletedRows);
        if (!Equals(layout, _layoutParameters))
        {
            _layoutParameters = layout;
            Relayout();
        }
    }

    /// <summary>일괄 처리 중에는 그리지 않는다. 끝나면 처리한 쪽이 StateHasChanged로 한 번 그린다 (R4).</summary>
    protected override bool ShouldRender() => _batch == 0;

    // ── JS 층 등록 (R1·R5): 키보드·포커스·클릭은 quro-grid.js가 그리드 루트 하나에서 받는다 ──

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_refreshRows && _virtualize is not null)
        {
            _refreshRows = false;
            await _virtualize.RefreshDataAsync();
            StateHasChanged();   // RefreshDataAsync는 스스로 다시 그리지 않는다 (호스트가 그린다). 행은 Revision이 같으면 건너뛴다 (R4)
        }

        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            var options = new GridJsOptions(
                EnterDirection == EnterDirection.Down ? "down" : "right",
                AddRowOnLastEnter,
                NotifySelection: true,   // DataSet 현재 행을 맞추므로 구독과 관계없이 (A-51)
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
            if (_activateRow is { } row)
            {
                _activateRow = null;
                if (DisplayIndexOf(row) is { } index)
                    await _module.InvokeVoidAsync("activateRow", _root, row, index);
            }
        }
    }

    // ── DataSet 현재 행 ↔ 그리드 활성 행 (A-51, GS-085) ──

    private void BindDataSet()
    {
        if (ReferenceEquals(_bound, DataSet))
            return;
        if (_bound is not null)
        {
            _bound.CurrentRowChanged -= OnCurrentRowChanged;
            _bound.ValueChanged -= OnDataSetValueChanged;
        }
        _bound = DataSet;
        _bound.CurrentRowChanged += OnCurrentRowChanged;
        _bound.ValueChanged += OnDataSetValueChanged;   // linked combo parent changes (A-57)
    }

    /// <summary>앱(또는 다른 컨트롤)이 현재 행을 옮기면 렌더 반영 뒤 그리드 활성 셀도 옮긴다. 포커스는 가져오지 않는다.</summary>
    private void OnCurrentRowChanged(object? sender, CurrentRowChangedEventArgs e)
    {
        if (_movingFromGrid || e.NewRowKey is null)
            return;
        _activateRow = e.NewRowKey;
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>화면 순번. 페이징 중이면 서버 기준 위치(모르면 -1, 그려져 있을 때만 옮김), 화면에 없는 행(걸러짐·접힘·숨긴 삭제 행)이면 null</summary>
    private int? DisplayIndexOf(int rowKey)
    {
        if (Paging)
            return _rowIndex.TryGetValue(rowKey, out var paged) ? paged : -1;
        var index = 0;
        foreach (var key in VisibleRowKeys())
        {
            if (key == rowKey)
                return index;
            index++;
        }
        return null;
    }

    /// <summary>Releases the JS module and its event handlers.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_bound is not null)
        {
            _bound.CurrentRowChanged -= OnCurrentRowChanged;
            _bound.ValueChanged -= OnDataSetValueChanged;
        }
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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
        var col = Column(field);
        if (col.ReadOnly || col.Compute is not null || NewRowLocked(rowKey, col))
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

        if (!TryParseCell(rowKey, col, text, out var value) || !await ApplyValue(rowKey, col.Field, value))
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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
        var col = Column(field);
        if (col.Type != CellType.Popup || !Editable(rowKey, col))
            return false;

        text ??= "";
        var matches = col.Lookup is null ? [] : await col.Lookup(text);
        var chosen = await RaisePopup(rowKey, col, text, matches, PopupTrigger.F4);
        if (chosen is null
            || !TryParseCell(rowKey, col, chosen, out var value)
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
    /// 편집 중 선택 목록 열기 (Alt+↓, Addendum A-17·A-67): Date는 달력(quro-grid.js가 그림), Combo는 목록.
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

    /// <summary>열린 달력의 시작 날짜 (yyyy-MM-dd). 값이 없으면 null (달력은 오늘의 달)</summary>
    internal string? PickerIso => _pickerDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>열린 달력에서 날짜를 골랐을 때 (quro-grid.js, 1회). <paramref name="iso"/>는 yyyy-MM-dd. 고르면 바로 확정한다.</summary>
    [JSInvokable]
    public Task PickDate(string? iso)
    {
        if (_picker is not { } p || Column(p.Field).Type != CellType.Date)
            return Task.CompletedTask;
        if (!DataSet.TryParseText(p.Field, iso, CultureInfo.InvariantCulture, out var value))
            return Task.CompletedTask;
        return PickValue(value);
    }

    /// <summary>열린 Combo 목록에서 항목을 골랐을 때 (quro-grid.js, 1회). <paramref name="index"/>는 그 행 목록(<see cref="OptionsOf"/>)의 순번.</summary>
    [JSInvokable]
    public Task PickOption(int index) =>
        _picker is { } p && OptionsOf(Column(p.Field), p.RowKey) is { } options && index >= 0 && index < options.Count
            ? PickValue(options[index].Value)
            : Task.CompletedTask;

    private async Task PickValue(object? value)
    {
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
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
        !col.ReadOnly && col.Type != CellType.Button && col.Compute is null && DataSet.GetState(rowKey) != RowState.Deleted
        && !NewRowLocked(rowKey, col);

    /// <summary>A <see cref="QuroColumn.NewRowOnly"/> column on a row that is not an added row (G-01).</summary>
    private bool NewRowLocked(int rowKey, QuroColumn col) => col.NewRowOnly && DataSet.GetState(rowKey) != RowState.Inserted;

    /// <summary>aria-readonly of a cell: read-only column, Button, Compute column, or edit-new column on a saved row.</summary>
    internal bool CellReadOnly(int rowKey, QuroColumn col) =>
        col.ReadOnly || col.Type == CellType.Button || col.Compute is not null || NewRowLocked(rowKey, col);

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
    private bool TryParseCell(int rowKey, QuroColumn col, string? text, out object? value)
    {
        value = null;
        switch (col.Type)
        {
            case CellType.Button:
                return false;
            case CellType.Combo when !string.IsNullOrEmpty(text):
                var option = OptionsOf(col, rowKey)?.FirstOrDefault(o => Same(o.Value?.ToString(), text) || Same(o.Text, text));
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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
        var copies = new List<int>();
        _batch++;
        try
        {
            foreach (var source in OrderedRowKeys().Where(rowKeys.Contains).ToList())
                copies.Add(await AddRowCore(DataSet.CopyOf(source), source, after: source));
        }
        finally
        {
            _batch--;
        }

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
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
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

    private async Task<int> AddRowCore(TItem? item = null, int? source = null, int? after = null, bool blank = false)
    {
        item ??= Activator.CreateInstance<TItem>();
        await OnRowAdding.InvokeAsync(new RowAddingArgs<TItem>(item, source));

        if (Paging)
            after = null;   // 페이징 중 추가 행은 맨 위에 모은다 (A-24)
        OrderedRowKeys();   // 화면 순서를 맞춘 뒤 넣는다
        var key = blank ? DataSet.AddBlankRow(item) : DataSet.AddRow(item, afterRowKey: after);
        PlaceInOrder(key, after);
        if (Grouped && after is { } a && _groupOf.TryGetValue(a, out var group))
            _groupOf[key] = group;   // 행 아래 추가·복사한 행은 그 행의 그룹에 (GS-075)
        RefreshRows();
        await OnRowAdded.InvokeAsync(new RowAddedArgs<TItem>(key, DataSet.GetItem(key)));
        return key;
    }

    /// <summary>
    /// 활성 행이 바뀌었을 때 JS가 호출한다 (Addendum A-13). DataSet 현재 행을 옮기고(A-51), 앱이 구독했으면 OnSelectionChanged.
    /// 그 사이 행이 없어졌으면 무시한다.
    /// </summary>
    [JSInvokable]
    public Task SelectionChanged(int rowKey)
    {
        _movingFromGrid = true;
        try
        {
            DataSet.MoveTo(rowKey);
        }
        catch (KeyNotFoundException)
        {
            return Task.CompletedTask;
        }
        finally
        {
            _movingFromGrid = false;
        }
        return OnSelectionChanged.InvokeAsync(new SelectionChangedArgs<TItem>(rowKey, DataSet.GetItem(rowKey)));
    }

    /// <summary>여러 행 삭제. OnRowDeleting은 행마다 발생하고, 취소한 행은 남는다. 삭제한 행 수를 돌려준다.</summary>
    [JSInvokable]
    public async Task<int> DeleteRows(IReadOnlyList<int> rowKeys)
    {
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
        var deleted = new List<int>();
        _batch++;
        try
        {
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
        }
        finally
        {
            _batch--;
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
        if (!Clipboard)
            return false;   // G-03: copy and paste are off
        using var step = DataSet.BeginUndoStep();   // 사용자 동작 하나 = Undo 한 단계 (앱의 이벤트 처리 포함, A-33)
        var args = new PastingArgs(startRowKey, startField, cells);
        await OnPasting.InvokeAsync(args);
        if (args.Cancel)
            return false;

        var rows = Paging ? await PagedRowsFrom(startRowKey, cells.Length) : VisibleRowKeys().ToList();
        var firstRow = rows.IndexOf(startRowKey);
        var columns = Columns;
        var firstCol = columns.ToList().IndexOf(Column(startField));
        if (firstRow < 0)
            throw new ArgumentException($"행 키 {startRowKey}가 화면에 없습니다.", nameof(startRowKey));

        var added = new List<int>();
        var skipped = new List<PasteSkip>();
        _batch++;   // 셀마다 이벤트를 받은 앱 화면이 다시 그려져도 그리드는 끝날 때 한 번만 그린다 (R4)
        try
        {
            for (var r = 0; r < cells.Length; r++)
            {
                int rowKey;
                if (firstRow + r < rows.Count)
                    rowKey = rows[firstRow + r];
                else if (AddRowsOnPaste)
                    added.Add(rowKey = await AddRowCore());
                else
                    break;

                for (var c = 0; c < cells[r].Length && firstCol + c < columns.Count; c++)
                {
                    var field = columns[firstCol + c].Field;
                    var reason = await PasteCell(rowKey, columns[firstCol + c], cells[r][c]);
                    if (reason is not null)
                        skipped.Add(new PasteSkip(rowKey, field, reason));
                }
            }
        }
        finally
        {
            _batch--;
        }

        await OnPasted.InvokeAsync(new PastedArgs(startRowKey, startField, added, skipped));
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// Adds rows from text, for a CSV upload (G-05, A-47). The app reads the file and maps its header to
    /// <paramref name="fields"/>; each row of <paramref name="rows"/> holds the texts in that order (extra texts are ignored).
    /// Rows go at the end (at the top while paging, A-24) with OnRowAdding/OnRowAdded, and each cell goes through the paste
    /// path: read with <see cref="Culture"/>, value-change events, read-only, unreadable and cancelled cells skipped. A row
    /// whose cells were all skipped stays. One undo step and one render (R3). Not affected by <see cref="Clipboard"/> or
    /// <see cref="AddRowsOnPaste"/>, and OnPasting/OnPasted are not raised.
    /// </summary>
    /// <exception cref="ArgumentException">A field is not a grid column or is a button column; nothing is changed.</exception>
    public async Task<ImportResult> ImportRows(IReadOnlyList<string> fields, IReadOnlyList<string[]> rows)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(rows);
        var columns = fields.Select(Column).ToList();
        if (columns.FirstOrDefault(c => c.Type == CellType.Button) is { } button)
            throw new ArgumentException($"Button column '{button.Field}' has no value to import.", nameof(fields));

        using var step = DataSet.BeginUndoStep();   // one user action = one undo step (A-33)
        var added = new List<int>();
        var skipped = new List<PasteSkip>();
        _batch++;   // the grid renders once at the end (R4)
        try
        {
            foreach (var cells in rows)
            {
                var rowKey = await AddRowCore();
                added.Add(rowKey);
                for (var c = 0; c < cells.Length && c < columns.Count; c++)
                {
                    var reason = await PasteCell(rowKey, columns[c], cells[c]);
                    if (reason is not null)
                        skipped.Add(new PasteSkip(rowKey, columns[c].Field, reason));
                }
            }
        }
        finally
        {
            _batch--;
        }

        StateHasChanged();
        return new ImportResult(added, skipped);
    }

    /// <summary>큰 붙여넣기 값의 최대 크기 (스트림).</summary>
    private const long MaxPasteBytes = 20 * 1024 * 1024;

    /// <summary>
    /// 큰 붙여넣기 (quro-grid.js가 1회 호출, R3). 값은 SignalR 메시지 크기 제한을 피해 스트림으로 가져온 뒤
    /// <see cref="Paste"/>와 같이 처리한다 (GS-057).
    /// </summary>
    [JSInvokable]
    public async Task<bool> PasteStream(int startRowKey, string startField)
    {
        if (_module is null || !Clipboard)
            return false;
        var data = await _module.InvokeAsync<IJSStreamReference>("pasteData", _root);
        await using var stream = await data.OpenReadStreamAsync(MaxPasteBytes);
        var cells = await JsonSerializer.DeserializeAsync<string[][]>(stream) ?? [];
        return await Paste(startRowKey, startField, cells);
    }

    /// <summary>셀 하나를 붙여넣는다. 건너뛰면 사유 리소스 키, 반영했으면 null.</summary>
    private async Task<string?> PasteCell(int rowKey, QuroColumn col, string text)
    {
        if (!Editable(rowKey, col))
            return PasteKeys.ReadOnly;
        if (!TryParseCell(rowKey, col, text, out var value))
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

    /// <summary>Cancels the cell edit and keeps the old value (called from quro-grid.js on Esc).</summary>
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
        if (!Column(field).CanSort)
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
        _sort.AddRange(sort.Where(s => Column(s.Field).CanSort));
        SortChanged();
        StateHasChanged();
    }

    /// <summary>Clears the sort.</summary>
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
        return col.Type == CellType.Combo ? OptionText(col, value, rowKey) : value;
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

            var result = await ItemsProvider!(new GridItemsRequest(page * PageSize, PageSize, SortColumns, cancellationToken, Filters));
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
        Relayout();   // 모든 행의 셀 너비가 바뀐다
        await OnColumnResized.InvokeAsync(new ColumnResizedArgs(col.Field, width));
        StateHasChanged();
    }

    // ── 헤더 필터 (GS-071~GS-073, Addendum A-31): 패널 안 입력은 브라우저에서만, 열기·적용 때 1회씩 (R2) ──

    /// <summary>사용자가 필터 패널에서 조건을 적용한 뒤 (Addendum A-31).</summary>
    [Parameter] public EventCallback<FilterChangedArgs> OnFilterChanged { get; set; }

    /// <summary>현재 필터 (컬럼마다 하나, 적용한 순서). 비어 있으면 거르지 않는다.</summary>
    public IReadOnlyList<ColumnFilter> Filters => _filters.ToList();

    /// <summary>필터 패널 열기 (필터 버튼·헤더에서 Alt+↓). Button 컬럼이면 열린 패널을 닫고 false.</summary>
    [JSInvokable]
    public bool OpenFilter(string field)
    {
        var col = Column(field);
        _filterDraft = null;
        _filterInvalid = null;
        _filterField = col.CanFilter ? col.Field : null;
        StateHasChanged();
        return _filterField is not null;
    }

    /// <summary>필터 패널을 닫는다 (Esc·바깥 클릭, quro-grid.js가 호출). 조건은 바꾸지 않는다.</summary>
    [JSInvokable]
    public void CloseFilter()
    {
        _filterField = null;
        _filterDraft = null;
        _filterInvalid = null;
        StateHasChanged();
    }

    /// <summary>
    /// 패널 입력을 적용한다 (JS가 1회 호출). 숫자는 사용자 로캘, 날짜는 ISO로 해석하고, 실패하면 그 입력을
    /// aria-invalid로 표시하고 패널을 연 채 false. 모두 비었거나 값 목록을 전부 고르면 그 컬럼의 필터를 푼다.
    /// 성공하면 패널을 닫고 OnFilterChanged.
    /// </summary>
    [JSInvokable]
    public async Task<bool> ApplyFilter(string field, FilterInput input)
    {
        var col = Column(field);
        if (!col.CanFilter)
            return false;
        if (!TryBuildFilter(col, input, out var filter, out var invalid))
        {
            _filterField = col.Field;
            _filterDraft = input;
            _filterInvalid = invalid;
            StateHasChanged();
            return false;
        }

        var index = _filters.FindIndex(f => f.Field == col.Field);
        if (filter is null)
        {
            if (index >= 0)
                _filters.RemoveAt(index);
        }
        else if (index >= 0)
            _filters[index] = filter;
        else
            _filters.Add(filter);

        _filterField = null;
        _filterDraft = null;
        _filterInvalid = null;
        FiltersChanged();
        await OnFilterChanged.InvokeAsync(new FilterChangedArgs(Filters));
        StateHasChanged();
        return true;
    }

    /// <summary>앱에서 필터를 지정한다 (OnFilterChanged는 발생하지 않음). 빈 목록이면 전체 해제.</summary>
    public void SetFilters(IReadOnlyList<ColumnFilter> filters)
    {
        _filters.Clear();
        _filters.AddRange(filters
            .Where(f => _columns.Any(c => c.Field == f.Field && c.CanFilter))
            .DistinctBy(f => f.Field));
        _filterField = null;
        FiltersChanged();
        StateHasChanged();
    }

    /// <summary>모든 필터를 푼다 (<c>SetFilters([])</c>와 같음, 이벤트 없음).</summary>
    public void ClearFilters() => SetFilters([]);

    /// <summary>필터가 바뀜: 페이징이면 조건으로 처음부터 다시 조회, 아니면 지금 행을 거른다.</summary>
    private void FiltersChanged()
    {
        if (Paging)
            ResetPages();
        else
            ApplyFilterToRows();
        _ensureActive = true;   // 활성 행이 걸러졌으면 남은 행으로 (렌더 반영 뒤)
    }

    /// <summary>
    /// 지금 행 중 조건에 맞지 않는 행을 기억한다 (메모리 모드). 적용한 뒤 편집·추가한 행은 다시 거르지 않는다
    /// (정렬 A-22와 같은 원칙). 다시 조회되면 다시 적용한다.
    /// </summary>
    private void ApplyFilterToRows()
    {
        _filterGeneration = DataSet.Generation;
        _filterOut.Clear();
        if (_filters.Count == 0)
            return;
        var filters = _filters.Select(f => (Filter: f, Column: Column(f.Field))).ToList();
        _filterOut.UnionWith(DataSet.RowKeys.Where(k => !filters.All(f => Matches(k, f.Column, f.Filter))));
    }

    private bool FilteredOut(int rowKey)
    {
        if (_filters.Count == 0 || Paging)
            return false;
        if (_filterGeneration != DataSet.Generation)
            ApplyFilterToRows();
        return _filterOut.Contains(rowKey);
    }

    private bool Matches(int rowKey, QuroColumn col, ColumnFilter filter)
    {
        var value = DataSet.GetValue(rowKey, col.Field);
        if (filter.Contains is { } text
            && (value is null || !Format(value, col).Contains(text, StringComparison.CurrentCultureIgnoreCase)
                && !(value.ToString() ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (filter.Min is not null || filter.Max is not null)
        {
            if (value is null)
                return false;
            var number = Number(value);
            if (number < filter.Min || number > filter.Max)
                return false;
        }
        if (filter.From is not null || filter.To is not null)
        {
            DateOnly? date = value switch
            {
                DateOnly d => d,
                DateTime d => DateOnly.FromDateTime(d),
                _ => null,
            };
            if (date is null || date < filter.From || date > filter.To)
                return false;
        }
        if (filter.Values is { } values && !values.Contains(value?.ToString() ?? ""))
            return false;
        return true;
    }

    /// <summary>패널 입력 → 조건. 비어 있으면 filter는 null. 해석 실패면 false와 그 입력 이름(data-filter).</summary>
    private bool TryBuildFilter(QuroColumn col, FilterInput input, out ColumnFilter? filter, out string? invalid)
    {
        filter = null;
        invalid = null;
        var result = new ColumnFilter(col.Field);
        switch (FilterKind(col))
        {
            case "number":
                if (!TryParseFilterNumber(input.Min, out var min))
                    invalid = "min";
                else if (!TryParseFilterNumber(input.Max, out var max))
                    invalid = "max";
                else if (min is not null || max is not null)
                    result = result with { Min = min, Max = max };
                break;
            case "date":
                if (!TryParseFilterDate(input.From, out var from))
                    invalid = "from";
                else if (!TryParseFilterDate(input.To, out var to))
                    invalid = "to";
                else if (from is not null || to is not null)
                    result = result with { From = from, To = to };
                break;
            case "values":
                var all = FilterValues(col).Select(v => v.Value).ToList();
                var chosen = (input.Values ?? []).Where(all.Contains).Distinct().ToList();
                if (chosen.Count > 0 && chosen.Count < all.Count)   // 전부 고름·아무것도 고르지 않음 = 거르지 않음
                    result = result with { Values = all.Where(chosen.Contains).ToList() };
                break;
            default:
                if (!string.IsNullOrWhiteSpace(input.Text))
                    result = result with { Contains = input.Text.Trim() };
                break;
        }
        if (invalid is not null)
            return false;
        if (result != new ColumnFilter(col.Field))
            filter = result;
        return true;
    }

    private bool TryParseFilterNumber(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, EffectiveCulture, out var number))
            return false;
        value = number;
        return true;
    }

    private static bool TryParseFilterDate(string? text, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        value = date;
        return true;
    }

    /// <summary>패널 입력 종류: text(Text·Popup) · number(Number·Currency) · date · values(Combo·Check)</summary>
    internal static string FilterKind(QuroColumn col) => col.Type switch
    {
        CellType.Number or CellType.Currency => "number",
        CellType.Date => "date",
        CellType.Combo or CellType.Check => "values",
        _ => "text",
    };

    /// <summary>값 목록 (Combo는 옵션, Check는 체크·해제). Value는 언어 무관 문자열</summary>
    internal IReadOnlyList<(string Value, string Text)> FilterValues(QuroColumn col) => col.Type switch
    {
        CellType.Combo => (AllOptions(col) ?? []).Select(o => (o.Value?.ToString() ?? "", o.Text)).ToList(),
        CellType.Check =>
        [
            (col.CheckedValue?.ToString() ?? "", Text(GridKeys.FilterChecked)),
            (col.UncheckedValue?.ToString() ?? "", Text(GridKeys.FilterUnchecked)),
        ],
        _ => [],
    };

    internal QuroColumn? FilterColumn => _filterField is null ? null : Column(_filterField);

    internal ColumnFilter? FilterOf(QuroColumn col) => _filters.Find(f => f.Field == col.Field);

    internal string? FilterInvalid(string input) => _filterInvalid == input ? "true" : null;

    /// <summary>패널에 보일 입력 값: 해석에 실패한 입력이 있으면 그 입력, 아니면 현재 조건 (숫자는 사용자 로캘, 날짜는 ISO)</summary>
    internal FilterInput FilterDraft(QuroColumn col)
    {
        if (_filterDraft is { } draft)
            return draft;
        var f = FilterOf(col);
        return new FilterInput(
            f?.Contains,
            f?.Min?.ToString(EffectiveCulture),
            f?.Max?.ToString(EffectiveCulture),
            f?.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            f?.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            f?.Values ?? FilterValues(col).Select(v => v.Value).ToList());
    }

    // ── 그룹핑·소계 (GS-074·GS-075, Addendum A-32·A-42): 메모리 모드에서만. 페이징 중에는 하지 않는다 ──

    private bool Grouped => !Paging && _groupBy.Count > 0;

    /// <summary>현재 그룹 컬럼 (목록 순서 = 단 순서). 비어 있으면 그룹핑하지 않는다.</summary>
    public IReadOnlyList<string> GroupColumns => _groupBy.ToList();

    /// <summary>
    /// 그 컬럼들로 그룹핑한다 (여러 단, 이벤트 없음). 모든 그룹을 펼치고, 지금 값으로 그룹을 다시 정한다.
    /// Button 컬럼은 무시한다. 페이징 중에는 아무것도 하지 않는다 (A-32).
    /// </summary>
    public void SetGroupBy(IReadOnlyList<string> fields)
    {
        if (Paging)
            return;
        _groupBy.Clear();
        _groupBy.AddRange(fields.Select(Column).Where(c => c.CanGroup).Select(c => c.Field).Distinct());
        _groupOf.Clear();
        _collapsed.Clear();
        _ensureActive = true;   // 활성 행이 접힌 곳에 있지 않도록 (렌더 반영 뒤)
        StateHasChanged();
    }

    /// <summary>그룹핑을 푼다 (<c>SetGroupBy([])</c>와 같음).</summary>
    public void ClearGroupBy() => SetGroupBy([]);

    /// <summary>모든 그룹을 펼친다.</summary>
    public void ExpandAll()
    {
        _collapsed.Clear();
        StateHasChanged();
    }

    /// <summary>모든 그룹을 접는다 (안쪽 단 포함). 머리 행과 소계 행만 남는다.</summary>
    public void CollapseAll()
    {
        _collapsed.UnionWith(BuildBody(all: true).Where(i => i.Kind == BodyItemKind.GroupHead).Select(i => i.GroupId!));
        StateHasChanged();
    }

    /// <summary>그룹 하나를 펼치거나 접는다 (Space·Enter·←→·클릭, quro-grid.js가 1회 호출). 없는 그룹이면 false.</summary>
    [JSInvokable]
    public bool ExpandGroup(string groupId, bool expanded)
    {
        if (!BuildBody(all: true).Any(i => i.Kind == BodyItemKind.GroupHead && i.GroupId == groupId))
            return false;
        if (expanded)
            _collapsed.Remove(groupId);
        else
            _collapsed.Add(groupId);
        StateHasChanged();
        return true;
    }

    internal enum BodyItemKind { Row, GroupHead, Subtotal }

    /// <summary>본문의 한 줄: 데이터 행, 그룹 머리 행, 소계 행. Index는 화면 위치 (aria-rowindex)</summary>
    internal sealed record BodyItem(BodyItemKind Kind, int Index, int RowKey = 0, string? GroupId = null, int Level = 0,
        string? Text = null, bool Expanded = true, IReadOnlyList<int>? Rows = null);

    /// <summary>메모리 모드 본문 (화면 순서). 그룹핑 중이면 그룹 머리 행·소계 행이 끼고, 접힌 그룹의 행은 빠진다</summary>
    internal List<BodyItem> BodyItems() => BuildBody(all: false);

    /// <summary><paramref name="all"/>이면 접힌 그룹 안까지 (모두 접기·그룹 확인용)</summary>
    private List<BodyItem> BuildBody(bool all)
    {
        var keys = FilteredRowKeys().ToList();
        var items = new List<BodyItem>(keys.Count);
        if (!Grouped)
        {
            foreach (var key in keys)
                items.Add(new BodyItem(BodyItemKind.Row, items.Count, key));
            return items;
        }

        if (_groupGeneration != DataSet.Generation)
        {
            _groupOf.Clear();   // 다시 조회됨: 새 값으로 그룹을 정한다
            _groupGeneration = DataSet.Generation;
        }
        var columns = _groupBy.Select(Column).ToList();
        foreach (var key in keys.Where(k => !_groupOf.ContainsKey(k)))
            _groupOf[key] = columns.Select(c => DataSet.GetValue(key, c.Field)).ToArray();

        var ordered = keys.OrderBy(k => k, Comparer<int>.Create((a, b) => CompareGroups(a, b, columns))).ToList();   // 안정 정렬: 그룹 안은 지금 순서
        AddGroups(items, ordered, 0, "", columns, all);
        return items;
    }

    private void AddGroups(List<BodyItem> items, List<int> rows, int level, string parentId, List<QuroColumn> columns, bool all)
    {
        var col = columns[level];
        for (var i = 0; i < rows.Count;)
        {
            var value = _groupOf[rows[i]][level];
            var j = i;
            while (j < rows.Count && Equals(_groupOf[rows[j]][level], value))
                j++;
            var run = rows.GetRange(i, j - i);
            var live = run.Where(k => DataSet.GetState(k) != RowState.Deleted).ToList();
            var id = $"{parentId}/{JsonSerializer.Serialize(value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture))}";
            var expanded = !_collapsed.Contains(id);

            items.Add(new BodyItem(BodyItemKind.GroupHead, items.Count, GroupId: id, Level: level,
                Text: string.Format(EffectiveCulture, Text(GridKeys.GroupHeader), Text(col.HeaderKey), GroupValueText(col, value), live.Count),
                Expanded: expanded));
            if (expanded || all)
            {
                if (level + 1 < columns.Count)
                    AddGroups(items, run, level + 1, id, columns, all);
                else
                    foreach (var key in run)
                        items.Add(new BodyItem(BodyItemKind.Row, items.Count, key));
            }
            if (Columns.Any(c => c.Aggregate == Aggregate.Sum))
                items.Add(new BodyItem(BodyItemKind.Subtotal, items.Count, GroupId: id, Level: level, Rows: live));
            i = j;
        }
    }

    /// <summary>그룹 순서: 단마다 값 오름차순(Combo는 명칭), 그 컬럼을 내림차순 정렬 중이면 내림차순. 빈 값은 맨 뒤</summary>
    private int CompareGroups(int a, int b, List<QuroColumn> columns)
    {
        for (var level = 0; level < columns.Count; level++)
        {
            var col = columns[level];
            var (x, y) = (_groupOf[a][level], _groupOf[b][level]);
            var result = (x, y) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                _ => (SortOf(col)?.Direction == SortDirection.Descending ? -1 : 1)
                     * CompareValues(col.Type == CellType.Combo ? OptionText(col, x) : x, col.Type == CellType.Combo ? OptionText(col, y) : y),
            };
            if (result != 0)
                return result;
        }
        return 0;
    }

    /// <summary>그룹 머리 행의 값 표시: 빈 값은 리소스 문구, Combo는 명칭, Check는 체크·해제 문구, 그 밖은 셀과 같은 서식</summary>
    private string GroupValueText(QuroColumn col, object? value) => value switch
    {
        null or "" => Text(GridKeys.GroupEmpty),
        _ when col.Type == CellType.Combo => OptionText(col, value),
        _ when col.Type == CellType.Check && Equals(value, col.CheckedValue) => Text(GridKeys.FilterChecked),
        _ when col.Type == CellType.Check && Equals(value, col.UncheckedValue) => Text(GridKeys.FilterUnchecked),
        _ => Format(value, col),
    };

    /// <summary>소계 행의 셀: Sum 컬럼은 그룹 합계, 첫 컬럼(합계가 아니면)은 "소계" 문구</summary>
    internal string SubtotalText(QuroColumn col, IReadOnlyList<int> rows) =>
        col.Aggregate == Aggregate.Sum ? SumText(col, rows)
        : Columns.Count > 0 && Columns[0] == col ? Text(GridKeys.GroupSubtotal)
        : "";

    // ── Undo / Redo (GS-076~GS-078, Addendum A-33): 이력은 DataSet에 있다. 이벤트는 내지 않는다 ──

    /// <summary>Ctrl+Z (JS가 호출, 1회). 마지막 사용자 동작을 되돌린다. 되돌릴 것이 없으면 false.</summary>
    [JSInvokable]
    public Task<bool> Undo() => Task.FromResult(Replay(DataSet.Undo));

    /// <summary>Ctrl+Y · Ctrl+Shift+Z (JS가 호출, 1회). 되돌린 동작을 다시 한다.</summary>
    [JSInvokable]
    public Task<bool> Redo() => Task.FromResult(Replay(DataSet.Redo));

    private bool Replay(Func<bool> action)
    {
        if (!action())
            return false;
        _ensureActive = true;        // 되살리거나 없앤 행 때문에 활성 셀이 사라졌을 수 있다
        _refreshRows = Paging;       // 추가 행이 생기거나 빠짐 → 가상 스크롤 다시 조회
        StateHasChanged();
        return true;
    }

    // ── 컬럼 숨김·이동·레이아웃 (GS-067~GS-069, P1) ──

    /// <summary>헤더 드래그로 컬럼을 옮긴 뒤 (GS-068).</summary>
    [Parameter] public EventCallback<ColumnMovedArgs> OnColumnMoved { get; set; }

    /// <summary>
    /// 컬럼을 숨기거나 다시 보인다 (GS-067). 숨긴 컬럼은 화면·합계·키보드 이동·붙여넣기·내보내기에서 빠지고,
    /// 값과 행 상태는 그대로다. 다시 보이면 제자리에 나온다.
    /// </summary>
    public void SetColumnVisible(string field, bool visible)
    {
        var col = Column(field);
        if (visible ? !_hidden.Remove(col.Field) : !_hidden.Add(col.Field))
            return;
        _ensureActive = !visible;   // 활성 셀이 숨긴 컬럼에 있었으면 같은 행의 다른 셀로 (렌더 반영 뒤)
        Relayout();
        StateHasChanged();
    }

    /// <summary>
    /// 헤더 드래그를 놓았을 때 JS가 호출한다 (GS-068, 호출 1회). <paramref name="toIndex"/>는 옮긴 뒤 화면 위치(보이는 컬럼 중).
    /// 고정 열 영역과 일반 영역 사이는 넘지 않는다: 영역 끝으로 맞추고, 제자리면 아무 것도 하지 않는다.
    /// </summary>
    [JSInvokable]
    public async Task MoveColumn(string field, int toIndex)
    {
        var col = Column(field);
        var shown = Columns.ToList();
        var from = shown.IndexOf(col);
        if (from < 0)
            return;
        var (first, last) = IsFrozen(col) ? (0, _frozenShown - 1) : (_frozenShown, shown.Count - 1);
        if (col.Group is { } group)
        {
            // 그룹 안의 컬럼은 그 그룹 안에서만 (GS-070)
            var members = shown.Select((c, i) => (c, i)).Where(x => GroupPath(x.c).Contains(group)).Select(x => x.i).ToList();
            (first, last) = (Math.Max(first, members[0]), Math.Min(last, members[^1]));
        }
        toIndex = Math.Clamp(toIndex, first, last);
        if (toIndex == from || SplitsGroup(shown, from, toIndex))
            return;

        // 숨긴 컬럼은 제자리를 지키도록 전체 순서(숨김 포함)에서 보이는 이웃 기준으로 넣는다
        var order = OrderedColumns().ToList();
        order.Remove(col);
        shown.RemoveAt(from);
        var at = toIndex < shown.Count ? order.IndexOf(shown[toIndex]) : order.IndexOf(shown[^1]) + 1;
        order.Insert(at, col);
        _columnOrder = order.Select(c => c.Field).ToList();

        Relayout();
        await OnColumnMoved.InvokeAsync(new ColumnMovedArgs(col.Field, toIndex));
        StateHasChanged();
    }

    /// <summary>현재 레이아웃 (GS-069): 컬럼 순서(숨김 포함)·리사이즈한 너비·숨김·정렬. 앱이 JSON으로 저장한다.</summary>
    public GridLayout GetLayout() => new(
        OrderedColumns()
            .Select(c => new LayoutColumn(c.Field, _widths.TryGetValue(c.Field, out var w) ? w : null, _hidden.Contains(c.Field)))
            .ToList(),
        SortColumns);

    /// <summary>
    /// 저장해 둔 레이아웃을 적용한다 (GS-069, 이벤트 없음). 없어진 필드는 무시하고, 레이아웃에 없는 새 컬럼은
    /// 정의상 앞 컬럼 뒤에 둔다. 고정 열은 고정 영역에 남는다.
    /// </summary>
    public void ApplyLayout(GridLayout layout)
    {
        var known = _columns.Select(c => c.Field).ToHashSet();
        var columns = layout.Columns.Where(c => known.Contains(c.Field)).ToList();

        _columnOrder = columns.Select(c => c.Field).Distinct().ToList();
        _widths.Clear();
        foreach (var c in columns.Where(c => c.Width is not null))
            _widths[c.Field] = Math.Max(MinColumnWidth, c.Width!.Value);
        _hidden.Clear();
        _hidden.UnionWith(columns.Where(c => c.Hidden).Select(c => c.Field));

        _sort.Clear();
        _sort.AddRange(layout.Sort.Where(s => known.Contains(s.Field) && Column(s.Field).Type != CellType.Button));
        SortChanged();

        Relayout();
        StateHasChanged();
    }

    /// <summary>
    /// 전체 컬럼 순서 (숨김 포함): 사용자가 옮긴 순서, 그 순서에 없는 컬럼은 정의상 앞 컬럼 뒤.
    /// 고정 열(정의상 앞 FrozenColumns개)은 항상 앞에 둔다.
    /// </summary>
    private IEnumerable<QuroColumn> OrderedColumns()
    {
        List<QuroColumn> result;
        if (_columnOrder is null)
            result = _columns;
        else
        {
            result = _columnOrder.Select(f => _columns.FirstOrDefault(c => c.Field == f)).OfType<QuroColumn>().Distinct().ToList();
            for (var i = 0; i < _columns.Count; i++)
                if (!result.Contains(_columns[i]))
                    result.Insert(i == 0 ? 0 : result.IndexOf(_columns[i - 1]) + 1, _columns[i]);
        }
        return result.Where(IsFrozen).Concat(result.Where(c => !IsFrozen(c)));
    }

    /// <summary>from에서 toIndex로 옮기면 어떤 그룹의 컬럼이 떨어지는지 (그룹 밖 컬럼이 그룹 가운데로 들어가는 경우)</summary>
    private static bool SplitsGroup(List<QuroColumn> shown, int from, int toIndex)
    {
        var moved = shown.ToList();
        var col = moved[from];
        moved.RemoveAt(from);
        moved.Insert(toIndex, col);
        var paths = moved.Select(GroupPath).ToList();
        foreach (var group in paths.SelectMany(p => p).Distinct())
        {
            var at = paths.Select((p, i) => p.Contains(group) ? i : -1).Where(i => i >= 0).ToList();
            if (at[^1] - at[0] + 1 != at.Count)
                return true;
        }
        return false;
    }

    /// <summary>컬럼의 그룹 경로 (바깥 → 안쪽). 그룹이 없으면 빈 목록</summary>
    private static List<QuroColumnGroup> GroupPath(QuroColumn col) => col.Group?.Path() ?? [];

    /// <summary>헤더 행 수: 그룹 단 + 컬럼 헤더 1 (GS-070). 본문 행의 aria-rowindex가 그만큼 밀린다</summary>
    internal int HeaderRows
    {
        get
        {
            _ = Columns;
            return _groupDepth + 1;
        }
    }

    /// <summary>그룹 헤더 칸 (GS-070): 같은 그룹이 이어지는 컬럼을 한 칸으로, 고정 열 경계에서는 나눈다. 그룹이 없는 컬럼은 빈 칸</summary>
    internal readonly record struct GroupCell(QuroColumnGroup? Group, int First, int Count, string Class, string Style);

    internal List<GroupCell> GroupCells(int level)
    {
        var columns = Columns;
        var cells = new List<GroupCell>();
        var left = 0;
        for (var i = 0; i < columns.Count;)
        {
            var path = GroupPath(columns[i]);
            var group = level < path.Count ? path[level] : null;
            var frozen = i < _frozenShown;
            var count = 1;
            while (group is not null && i + count < columns.Count && (i + count < _frozenShown) == frozen
                   && GroupPath(columns[i + count]) is var next && level < next.Count && next[level] == group)
                count++;

            var width = columns.Skip(i).Take(count).Sum(WidthOf);
            var kind = group is null ? "quro-cell quro-group-empty" : "quro-cell quro-group-header";
            cells.Add(new GroupCell(group, i, count,
                frozen ? kind + " quro-frozen" : kind,
                frozen ? $"width:{width}px;left:{left}px" : $"width:{width}px"));
            left += width;
            i += count;
        }
        return cells;
    }

    private bool IsFrozen(QuroColumn col) => _columns.IndexOf(col) is var i && i >= 0 && i < FrozenColumns;

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
        CheckParentField(column);
        (column.Group?.Members ?? _definition).Add(column);
        RebuildColumns();
        RegisterRule(column);
        Relayout();
        if (column.Group is not null)
            StateHasChanged();   // 그룹 안 컬럼은 그리드 본문(Deferred)이 그려진 뒤에 등록된다 (GS-070)
    }

    void IColumnHost.AddGroup(QuroColumnGroup group) => (group.Parent?.Members ?? _definition).Add(group);

    void IColumnHost.RemoveGroup(QuroColumnGroup group)
    {
        if ((group.Parent?.Members ?? _definition).Remove(group))
        {
            RebuildColumns();
            Relayout();
            StateHasChanged();
        }
    }

    /// <summary>정의 트리를 펼쳐 정의 순서의 컬럼 목록을 만든다.</summary>
    private void RebuildColumns()
    {
        _columns.Clear();
        Flatten(_definition);

        void Flatten(List<object> members)
        {
            foreach (var member in members)
                if (member is QuroColumn col)
                    _columns.Add(col);
                else if (member is QuroColumnGroup group)
                    Flatten(group.Members);
        }
    }

    void IColumnHost.ColumnChanged(QuroColumn column)
    {
        CheckParentField(column);
        RegisterRule(column);
        Relayout();
    }

    /// <summary>컬럼의 Required·Min·Max를 DataSet 규칙 API로 넘긴다 (Addendum A-10).</summary>
    private void RegisterRule(QuroColumn col)
    {
        if (_definitionRules.TryGetValue(col.Field, out var defined))
            DataSet.SetRule(col.Field, defined);   // column req/min/max merged with validate rows (QuroGrid.Definition.cs)
        else if (col.Type != CellType.Button && col.Compute is null && (col.Required || col.Min is not null || col.Max is not null)
)
            DataSet.SetRule(col.Field, new FieldRule { Required = col.Required, Min = col.Min, Max = col.Max });
    }

    void IColumnHost.GroupChanged()
    {
        Relayout();
        StateHasChanged();
    }

    void IColumnHost.RemoveColumn(QuroColumn column)
    {
        if ((column.Group?.Members ?? _definition).Remove(column))
        {
            RebuildColumns();
            Relayout();
            StateHasChanged();
        }
    }

    // ── 렌더링 보조 (QuroGridRow와 공유) ──

    /// <summary>본문 행 수 (aria-rowcount, GS-066). 페이징 중에는 그려지지 않은 행도 센다: 추가 행 + 서버 전체 건수</summary>
    private int BodyRowCount(List<BodyItem>? body) =>
        Paging ? InsertedRowKeys().Count + (_total ?? 0) : body!.Count;   // 그룹 머리 행·소계 행도 행이다

    /// <summary>메모리 모드의 화면 데이터 행 (화면 순서). 그룹핑 중이면 그룹 순서이고 접힌 그룹의 행은 빠진다</summary>
    private IEnumerable<int> VisibleRowKeys() =>
        Grouped
            ? BodyItems().Where(i => i.Kind == BodyItemKind.Row).Select(i => i.RowKey)
            : FilteredRowKeys();

    /// <summary>메모리 모드의 행 (화면 순서): 삭제 표시 행 처리(O-02)와 필터(A-31)를 거친다</summary>
    private IEnumerable<int> FilteredRowKeys() =>
        OrderedRowKeys().Where(k => !FilteredOut(k)
                                    && (DeletedRows != DeletedRowDisplay.Hide || DataSet.GetState(k) != RowState.Deleted));

    internal string Text(string key) => Localizer is null ? key : Localizer[key].Value;

    internal string CellClass(int index, string kind)
    {
        _ = Columns;   // _frozenShown 계산
        return index < _frozenShown ? $"quro-cell {kind} quro-frozen" : $"quro-cell {kind}";
    }

    internal string CellStyle(int index)
    {
        var columns = Columns;
        var width = $"width:{WidthOf(columns[index])}px";
        if (index >= _frozenShown)
            return width;
        var left = columns.Take(index).Sum(WidthOf);
        return $"{width};left:{left}px";
    }

    internal string DisplayText(int rowKey, QuroColumn col)
    {
        if (col.Type is CellType.Button or CellType.Check)
            return "";   // Button은 바인딩 값이 없고, Check는 체크 표시로 그린다

        var value = CellValue(rowKey, col);
        return col.Type switch
        {
            CellType.Currency => FormatNumber(value, CurrencyFormat(col, rowKey)),
            CellType.Combo => OptionText(col, value, rowKey),
            _ => Format(value, col),
        };
    }

    internal bool IsPickerOpen(int rowKey) => _picker?.RowKey == rowKey;

    internal int PickerIndex(QuroColumn col) =>
        _picker is { } p && OptionsOf(col, p.RowKey) is { } options
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
        col.Type == CellType.Button || col.Compute is not null ? null : DataSet.GetCellMark(rowKey, col.Field);

    /// <summary>Cell value: the DataSet value, or the app's <see cref="QuroColumn.Compute"/> result (A-59).</summary>
    private object? CellValue(int rowKey, QuroColumn col) =>
        col.Compute is { } compute ? compute(DataSet.GetItem(rowKey)) : DataSet.GetValue(rowKey, col.Field);

    /// <summary>Classes for the app's <see cref="QuroColumn.CellStyle"/> tones (A-59). Empty when there is none.</summary>
    internal string ToneClass(int rowKey, QuroColumn col)
    {
        if (col.CellStyle is not { } style || col.Type == CellType.Button)
            return "";
        var tone = style(DataSet.GetItem(rowKey), CellValue(rowKey, col));
        if (tone == CellTone.None)
            return "";
        var cls = new System.Text.StringBuilder();
        foreach (var flag in Enum.GetValues<CellTone>())
            if (flag != CellTone.None && tone.HasFlag(flag))
                cls.Append(" quro-tone-").Append(flag.ToString().ToLowerInvariant());
        return cls.ToString();
    }


    /// <summary>에디터 초기 값. 천 단위 구분 없이 사용자 로캘로. Combo는 명칭.</summary>
    internal string EditText(int rowKey, QuroColumn col) =>
        DataSet.GetValue(rowKey, col.Field) switch
        {
            var v when col.Type == CellType.Combo => OptionText(col, v, rowKey),
            null => "",
            var date when date is DateOnly or DateTime => Format(date, col),
            IFormattable f => f.ToString(null, EffectiveCulture),
            var v => v.ToString() ?? "",
        };

    private string FooterText(QuroColumn col)
    {
        if (col.Aggregate != Aggregate.Sum)
            return "";

        var keys = DataSet.RowKeys.Where(k => DataSet.GetState(k) != RowState.Deleted && !FilteredOut(k)).ToList();   // 보이는 행 (A-31, 접힌 그룹 포함)
        if (!Paging)
            return SumText(col, keys);

        // 적재되지 않은 행이 있으므로 서버 합계 + 저장 전 변경분 (A-24). 서버 합계가 없으면 비운다.
        // A Compute column has no server total (A-59)
        if (col.Compute is not null || _totals is null || !_totals.TryGetValue(col.Field, out var total))
            return "";
        return FormatNumber(total + PendingDelta(col.Field), SumFormat(col, keys));
    }

    private string SumText(QuroColumn col, IReadOnlyList<int> keys) =>
        FormatNumber(keys.Sum(k => Number(CellValue(k, col))), SumFormat(col, keys));

    // 통화가 섞이면 합계 의미가 없으므로 첫 행의 통화 자릿수를 쓴다. 혼합 통화 합계 규칙은 Spec에 없다.
    private string? SumFormat(QuroColumn col, IReadOnlyList<int> keys) =>
        col.Type == CellType.Currency && keys.Count > 0 ? CurrencyFormat(col, keys[0]) : col.Format;

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
