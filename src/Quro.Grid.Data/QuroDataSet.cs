using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;

namespace Quro.Grid.Data;

/// <summary>
/// 그리드 데이터 모델 (Spec 4.4). 행 상태·원본값·변경분을 관리한다. UI를 참조하지 않는다.
/// </summary>
public sealed class QuroDataSet<T> : IQuroDataSet where T : class
{
    private static readonly Dictionary<string, PropertyInfo> Fields =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name);

    private static readonly Dictionary<string, PropertyInfo> FieldsIgnoreCase =
        Fields.Values.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)   // ambiguous names (Qty and QTY) only match exactly
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);

    // Column metadata from T (A-52), in declaration order
    private static readonly DataSetColumn[] ColumnsOfT =
        Fields.Values.OrderBy(p => p.MetadataToken).Select(ColumnOf).ToArray();

    private readonly DataSetColumn[] _columns = (DataSetColumn[])ColumnsOfT.Clone();
    private readonly List<Row> _rows = [];
    private readonly Dictionary<int, Row> _byKey = [];
    private readonly Dictionary<string, FieldRule> _rules = [];
    private int _nextLoadedKey = 1;
    private int _nextInsertedKey = -1;   // 추가 행은 음수 임시 키 (Spec 4.4 tempKey)
    private readonly Func<T, object>? _idOf;                 // 행 ID (서버측 페이징, A-24)
    private readonly Dictionary<object, int> _keyById = [];  // 적재한(추가 행 아닌) 행의 ID → 행 키
    private readonly Dictionary<int, object> _idByKey = [];

    // Undo / Redo (GS-076~GS-078, Addendum A-33): 단계마다 건드린 행의 전후 모습(값·상태·목록 위치)을 둔다
    private const int UndoLimit = 50;
    private readonly LinkedList<Step> _undo = [];
    private readonly Stack<Step> _redo = [];
    private Step? _step;      // 열린 단계
    private int _stepDepth;   // BeginUndoStep 중첩 깊이

    private int? _current;    // 현재 행 (A-51). 페이징으로 놓인 행이어도 유지

    /// <summary>Creates an empty data set.</summary>
    public QuroDataSet()
    {
    }

    /// <summary>
    /// 서버측 페이징용 (R6, Addendum A-24). <paramref name="idSelector"/>는 행 ID(기본 키).
    /// 같은 행을 다시 적재해도 같은 행 키를 쓰고, 변경 행은 다시 적재해도 변경 값을 유지한다.
    /// </summary>
    public QuroDataSet(Func<T, object> idSelector) => _idOf = idSelector;

    // ── Spec 4.4에 없는 보조 API (행 적재·조회, 검증 규칙 등록) ──

    /// <summary>
    /// 조회한 한 페이지를 합친다 (서버측 페이징, GS-052). 행 키를 조회 순서대로 돌려준다.
    /// 이미 가진 행(같은 ID)은 그 행 키를 쓰고 값은 바꾸지 않는다 (변경 유지). 행 ID 선택자가 필요하다.
    /// </summary>
    public IReadOnlyList<int> LoadPage(IEnumerable<T> rows)
    {
        if (_idOf is null)
            throw new InvalidOperationException("LoadPage에는 행 ID 선택자가 필요합니다 (QuroDataSet(idSelector)).");

        var keys = new List<int>();
        foreach (var item in rows)
        {
            if (_keyById.TryGetValue(_idOf(item), out var known))
            {
                // 가진 행(변경 행 포함)은 그대로, 놓았던 행은 같은 행 키로 다시 적재
                if (!_byKey.ContainsKey(known))
                    Append(new Row(known, item, RowState.Normal, Snapshot(item)));
                keys.Add(known);
                continue;
            }
            var row = new Row(_nextLoadedKey++, item, RowState.Normal, Snapshot(item));
            Append(row);
            keys.Add(row.Key);
        }
        return keys;
    }

    /// <summary>
    /// 화면에서 벗어난 행을 놓는다 (R6). 변경 행(Inserted·Updated·Deleted)은 남긴다.
    /// 행 ID → 행 키 색인은 남겨, 다시 적재하면 같은 행 키를 쓴다 (화면의 활성 셀·선택 유지).
    /// </summary>
    public void Release(IEnumerable<int> rowKeys)
    {
        var drop = rowKeys.ToHashSet();
        _rows.RemoveAll(row =>
        {
            if (row.State != RowState.Normal || !drop.Contains(row.Key))
                return false;
            _byKey.Remove(row.Key);
            return true;
        });
    }

    /// <summary>조회 결과를 적재한다. 모든 행은 Normal.</summary>
    public void Load(IEnumerable<T> rows)
    {
        _rows.Clear();
        _byKey.Clear();
        _keyById.Clear();
        _idByKey.Clear();
        _nextLoadedKey = 1;
        _nextInsertedKey = -1;
        Generation++;
        ClearHistory();

        foreach (var item in rows)
            Append(new Row(_nextLoadedKey++, item, RowState.Normal, Snapshot(item)));

        SetCurrent(_rows.Count > 0 ? _rows[0].Key : null);
        Loaded?.Invoke(this, EventArgs.Empty);
    }

    // ── 현재 행·변경 알림 (A-51): 한 DataSet을 여러 컨트롤이 바인딩한다 ──

    /// <summary>
    /// 바인딩한 모든 컨트롤이 함께 쓰는 현재 행. <see cref="Load"/> 뒤 첫 행(없으면 null).
    /// 현재 행이 목록에서 빠지면 같은 위치의 다음 행, 없으면 앞 행으로 옮긴다.
    /// </summary>
    public int? CurrentRowKey => _current;

    /// <summary>현재 행을 옮긴다. 바뀌면 <see cref="CurrentRowChanged"/>.</summary>
    public void MoveTo(int rowKey)
    {
        Find(rowKey);
        SetCurrent(rowKey);
    }

    /// <summary>Raised when <see cref="CurrentRowKey"/> changes.</summary>
    public event EventHandler<CurrentRowChangedEventArgs>? CurrentRowChanged;

    /// <summary>값이 실제로 바뀔 때마다 (SetValue, 되돌리기 포함).</summary>
    public event EventHandler<DataSetValueChangedEventArgs>? ValueChanged;

    /// <summary>행이 목록에 들어올 때 (AddRow·AddBlankRow, Redo 등으로 되살아난 행).</summary>
    public event EventHandler<DataSetRowEventArgs>? RowAdded;

    /// <summary>행이 삭제될 때 (Deleted 표시, 추가 행 제거, 되돌리기로 빠진 행). 저장 후 Deleted 행 제거에서는 다시 내지 않는다.</summary>
    public event EventHandler<DataSetRowEventArgs>? RowDeleted;

    /// <summary>Raised after <see cref="Load"/>.</summary>
    public event EventHandler? Loaded;

    /// <summary>SaveAsync가 성공해 AcceptChanges를 마친 뒤.</summary>
    public event EventHandler<DataSetSavedEventArgs>? Saved;

    private void SetCurrent(int? rowKey)
    {
        if (_current == rowKey)
            return;
        var old = _current;
        _current = rowKey;
        CurrentRowChanged?.Invoke(this, new CurrentRowChangedEventArgs(old, rowKey));
    }

    private void RaiseValueChanges(int rowKey, Dictionary<string, object?> before, T item)
    {
        if (ValueChanged is null)
            return;
        foreach (var prop in Fields.Values)
            if (prop.GetValue(item) is var now && !Equals(before[prop.Name], now))
                ValueChanged(this, new DataSetValueChangedEventArgs(rowKey, prop.Name, before[prop.Name], now));
    }

    /// <summary>Load 때마다 바뀐다. 화면이 다시 조회됐는지 알 때 쓴다 (정렬 재적용, Addendum A-22).</summary>
    public int Generation { get; private set; }

    /// <summary>현재 목록에 있는 행 키 (표시 순서). Deleted 행도 포함한다 (화면 처리는 O-02).</summary>
    public IReadOnlyList<int> RowKeys => _rows.Select(r => r.Key).ToList();

    /// <summary>Current value of a field in a row.</summary>
    public object? GetValue(int rowKey, string field) => Field(field).GetValue(Find(rowKey).Item);

    /// <summary>행의 현재 값을 복사한 새 객체 (행 복사용, 목록에는 넣지 않는다).</summary>
    public T CopyOf(int rowKey) => Clone(Find(rowKey).Item);

    /// <summary>행 객체. 값은 <see cref="SetValue"/>로 바꿔야 행 상태가 갱신된다.</summary>
    public T GetItem(int rowKey) => Find(rowKey).Item;

    /// <summary>행의 값·상태·셀 표시가 바뀔 때마다 증가한다. 그리드가 바뀐 행만 다시 그리는 데 쓴다 (R4).</summary>
    public int GetRevision(int rowKey) => Find(rowKey).Revision;

    // ── 열 메타 (A-52) ──

    /// <summary>Column metadata in <c>T</c>'s declaration order. Built from <c>T</c>, changed by <see cref="SetColumn"/>.</summary>
    public IReadOnlyList<DataSetColumn> Columns => _columns;

    /// <summary>Metadata of one column. The field is matched like <see cref="SetValue"/> (exact name, then ignoring case).</summary>
    public DataSetColumn GetColumn(string field) => _columns[ColumnIndex(Field(field))];

    /// <summary>
    /// 화면 정의로 열 메타를 보완한다 (이 DataSet에만). 이름은 속성 이름으로 바꿔 둔다.
    /// 형식이 속성과 다르거나 기본값을 속성 형식으로 바꿀 수 없으면 <see cref="ArgumentException"/>.
    /// </summary>
    public void SetColumn(DataSetColumn column)
    {
        var prop = Field(column.Name);
        if (column.Type != prop.PropertyType)
            throw new ArgumentException($"열 '{prop.Name}'의 형식은 {prop.PropertyType.Name}입니다 ({column.Type.Name} 아님).", nameof(column));

        _columns[ColumnIndex(prop)] = column with { Name = prop.Name, Default = ConvertDefault(column.Default, prop) };
    }

    private int ColumnIndex(PropertyInfo prop) => Array.FindIndex(_columns, c => c.Name == prop.Name);

    private static DataSetColumn ColumnOf(PropertyInfo prop) => new(
        prop.Name,
        prop.PropertyType,
        Key: prop.IsDefined(typeof(KeyAttribute)),
        Required: prop.IsDefined(typeof(RequiredAttribute)),
        Default: ConvertDefault(prop.GetCustomAttribute<DefaultValueAttribute>()?.Value, prop),
        MaxLength: prop.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? prop.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength);

    /// <summary>Default value as the property type (<c>[DefaultValue(1)]</c> on a decimal gives <c>1m</c>).</summary>
    private static object? ConvertDefault(object? value, PropertyInfo prop)
    {
        var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (value is null || target.IsInstanceOfType(value))
            return value;
        try
        {
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
        {
            throw new ArgumentException($"열 '{prop.Name}'의 기본값 '{value}'을(를) {target.Name}(으)로 바꿀 수 없습니다.", nameof(value), e);
        }
    }

    /// <summary>New row object for AddRow·AddBlankRow: a copy of the template, or T with the column defaults.</summary>
    private T NewItem(T? template)
    {
        if (template is not null)
            return Clone(template);
        var item = Activator.CreateInstance<T>();
        foreach (var column in _columns.Where(c => c.Default is not null))
            Fields[column.Name].SetValue(item, column.Default);
        return item;
    }

    /// <summary>필드 검증 규칙 등록. 같은 필드에 다시 등록하면 교체한다.</summary>
    public void SetRule(string field, FieldRule rule) => _rules[Field(field).Name] = rule;

    /// <summary>
    /// 붙여넣기·입력 문자열을 사용자 로캘로 해석해 저장한다 (Spec 4.8, GS-007).
    /// 해석에 실패하면 값을 바꾸지 않고 false. 빈 문자열은 nullable 필드에서만 null로 받는다.
    /// </summary>
    public bool TrySetText(int rowKey, string field, string? text, IFormatProvider culture)
    {
        if (!TryParseText(field, text, culture, out var value))
            return false;

        SetValue(rowKey, field, value);
        return true;
    }

    /// <summary>값을 바꾸지 않고 해석만 한다. 그리드가 OnCellValueChanging에 새 값을 넘길 때 쓴다.</summary>
    public bool TryParseText(string field, string? text, IFormatProvider culture, out object? value) =>
        TryParse(text, Field(field).PropertyType, culture, out value);

    /// <summary>앱이 판정한 셀 표시를 지정한다. null이면 지운다 (Spec 4.9, GS-008).</summary>
    public void SetCellMark(int rowKey, string field, CellMark? mark)
    {
        var name = Field(field).Name;
        var row = Find(rowKey);
        if (mark is null)
            row.Marks.Remove(name);
        else
            row.Marks[name] = mark;
        row.Revision++;
    }

    /// <summary>Mark on a cell, or null when the cell has none.</summary>
    public CellMark? GetCellMark(int rowKey, string field)
    {
        var name = Field(field).Name;
        return Find(rowKey).Marks.GetValueOrDefault(name);
    }

    /// <summary>
    /// 변경분을 저장 API에 POST한다 (GS-009). 2xx면 <see cref="AcceptChanges"/>, 그 밖에는 변경분을 유지한다.
    /// 저장 후 재조회(<see cref="Load"/>)는 앱이 한다.
    /// </summary>
    public async Task<SaveResult> SaveAsync(HttpClient http, string requestUri, CancellationToken cancellationToken = default)
    {
        var changes = GetChanges();
        if (changes.IsEmpty)
            return new SaveResult(SaveStatus.NoChanges);

        using var content = new StringContent(changes.ToJson(), Encoding.UTF8, "application/json");
        // ConfigureAwait(false)를 쓰지 않는다: AcceptChanges는 호출한 회로의 동기화 컨텍스트에서 실행돼야 한다.
        using var response = await http.PostAsync(requestUri, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            AcceptChanges();
            var saved = new SaveResult(SaveStatus.Saved, response.StatusCode);
            Saved?.Invoke(this, new DataSetSavedEventArgs(saved));
            return saved;
        }

        var status = response.StatusCode == HttpStatusCode.Conflict ? SaveStatus.Conflict : SaveStatus.Failed;
        return new SaveResult(status, response.StatusCode);
    }

    // ── Spec 4.4 API ──

    /// <summary>Row state (Spec 4.4).</summary>
    public RowState GetState(int rowKey) => Find(rowKey).State;

    /// <summary>원본값. 추가 행은 원본이 없으므로 null.</summary>
    public object? GetOriginal(int rowKey, string field)
    {
        var prop = Field(field);
        var row = Find(rowKey);
        return row.Original is null ? null : row.Original[prop.Name];
    }

    /// <summary>Sets a field value, updates the row state and records the change for undo.</summary>
    public void SetValue(int rowKey, string field, object? value)
    {
        var prop = Field(field);
        var row = Find(rowKey);
        if (row.State == RowState.Deleted)
            throw new InvalidOperationException($"삭제 표시된 행({rowKey})은 수정할 수 없습니다.");

        using var _ = Record();
        Touch(row);
        var old = prop.GetValue(row.Item);
        prop.SetValue(row.Item, value);
        row.Revision++;

        if (row.State is RowState.Normal or RowState.Updated)
            row.State = MatchesOriginal(row) ? RowState.Normal : RowState.Updated;

        if (!Equals(old, value))
            ValueChanged?.Invoke(this, new DataSetValueChangedEventArgs(rowKey, prop.Name, old, value));
    }

    /// <summary>
    /// 행 추가. 상태는 Inserted. <paramref name="afterRowKey"/>가 있으면 그 행 바로 아래, 없으면 끝.
    /// 템플릿이 없으면 열 기본값(<see cref="DataSetColumn.Default"/>)을 채운다 (A-52).
    /// </summary>
    public int AddRow(T? template = null, int? afterRowKey = null)
    {
        var item = NewItem(template);
        var row = new Row(_nextInsertedKey--, item, RowState.Inserted, original: null);
        using var _ = Record();
        Touch(row);   // 목록에 넣기 전: 되돌리면 빠진다

        if (afterRowKey is null)
            Append(row);
        else
        {
            // 행 복사: 원래 행 바로 아래 (Addendum A-12)
            _rows.Insert(_rows.IndexOf(Find(afterRowKey.Value)) + 1, row);
            _byKey.Add(row.Key, row);
        }
        RowAdded?.Invoke(this, new DataSetRowEventArgs(row.Key));
        return row.Key;
    }

    /// <summary>
    /// Adds a blank row at the end to type into (screen definition <c>initRows</c>). While its values are still the
    /// ones it was added with (from <paramref name="template"/>, or the column defaults without one), it is not a change: <see cref="Validate"/>,
    /// <see cref="GetChanges"/> and <see cref="HasChanges"/> skip it and <see cref="AcceptChanges"/> keeps it as an
    /// added row. Not an undo step.
    /// </summary>
    public int AddBlankRow(T? template = null)
    {
        var item = NewItem(template);
        var row = new Row(_nextInsertedKey--, item, RowState.Inserted, original: null) { Blank = Snapshot(item) };
        Append(row);
        RowAdded?.Invoke(this, new DataSetRowEventArgs(row.Key));
        return row.Key;
    }

    /// <summary>Inserted면 목록에서 제거, 아니면 Deleted.</summary>
    public void DeleteRow(int rowKey)
    {
        var row = Find(rowKey);
        using var _ = Record();
        Touch(row);
        if (row.State == RowState.Inserted)
            Remove(row);
        else
        {
            row.State = RowState.Deleted;
            row.Revision++;
            RowDeleted?.Invoke(this, new DataSetRowEventArgs(row.Key));
        }
    }

    /// <summary>
    /// 행 단위 변경 취소 (O-01 결정: MVP Undo). Updated·Deleted는 원본값으로 Normal 복귀, Inserted는 목록에서 제거.
    /// 이것도 Undo 한 단계다 (되돌리면 취소 전 값·상태).
    /// </summary>
    public void RevertRow(int rowKey)
    {
        var row = Find(rowKey);
        using var _ = Record();
        Touch(row);
        Revert(row);
    }

    /// <summary>
    /// 필수·범위 검증. 저장 대상인 Inserted·Updated 행만 검사한다 (손대지 않은 빈 행 제외).
    /// 필수는 <see cref="SetRule"/> 규칙 또는 열 메타 <see cref="DataSetColumn.Required"/> (A-63).
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<ValidationError>();
        var required = _columns.Where(c => c.Required).Select(c => c.Name).ToHashSet();
        var checks = _rules.Concat(required.Where(f => !_rules.ContainsKey(f)).Select(f => KeyValuePair.Create(f, new FieldRule()))).ToList();

        foreach (var row in _rows.Where(r => r.State is RowState.Inserted or RowState.Updated && !Untouched(r)))
        foreach (var (field, rule) in checks)
        {
            var value = Fields[field].GetValue(row.Item);

            if ((rule.Required || required.Contains(field)) && (value is null || value is string s && string.IsNullOrWhiteSpace(s)))
                errors.Add(new ValidationError(row.Key, field, ValidationKeys.Required));

            if (ToDecimal(value) is { } number)
            {
                if (rule.Min is { } min && (rule.MinExclusive ? number <= min : number < min))
                    errors.Add(new ValidationError(row.Key, field, rule.MessageKey ?? (rule.MinExclusive ? ValidationKeys.MinExclusive : ValidationKeys.Min)));
                if (rule.Max is { } max && (rule.MaxExclusive ? number >= max : number > max))
                    errors.Add(new ValidationError(row.Key, field, rule.MessageKey ?? (rule.MaxExclusive ? ValidationKeys.MaxExclusive : ValidationKeys.Max)));
                if (number == rule.NotEqual)
                    errors.Add(new ValidationError(row.Key, field, rule.MessageKey ?? ValidationKeys.NotEqual));
            }
        }

        return new ValidationResult { Errors = errors };
    }

    /// <summary>미저장 변경이 있는지 (Normal이 아닌 행). 이탈 경고·연결 끊김 경고에 쓴다 (R7).</summary>
    public bool HasChanges => _rows.Exists(r => r.State != RowState.Normal && !Untouched(r));

    /// <summary>저장용 변경분.</summary>
    public ChangeSet<T> GetChanges() => new()
    {
        Inserted = ItemsIn(RowState.Inserted),
        Updated = ItemsIn(RowState.Updated),
        Deleted = ItemsIn(RowState.Deleted),
    };

    /// <summary>
    /// 저장 성공 응답을 받은 뒤에만 호출한다.
    /// Deleted 행은 제거하고, 나머지는 현재 값을 원본으로 삼아 Normal.
    /// </summary>
    public void AcceptChanges()
    {
        ClearHistory();   // 저장된 상태가 새 기준 (A-33)
        foreach (var row in _rows.ToList())
        {
            if (row.State == RowState.Deleted)
            {
                Remove(row, notify: false);   // RowDeleted was raised when it was marked
                continue;
            }

            if (Untouched(row))
                continue;   // nothing was sent for it; it stays a blank row
            row.Original = Snapshot(row.Item);
            row.Blank = null;
            row.State = RowState.Normal;
            row.Revision++;
        }
    }

    /// <summary>전체 변경 취소. 각 행에 RevertRow와 같은 규칙을 적용한다.</summary>
    public void RejectChanges()
    {
        ClearHistory();
        foreach (var row in _rows.ToList())
            Revert(row);
    }

    // ── Undo / Redo (P1, GS-076~GS-078, Addendum A-33) ──

    /// <summary>True when there is a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>True when there is a step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// 여러 변경을 Undo 한 단계로 묶는다 (붙여넣기·일괄 삭제·앱의 재계산 포함 커밋 등). 범위를 닫을 때 단계가 끝난다.
    /// 중첩하면 바깥 범위에 합쳐진다. 범위 밖의 SetValue·AddRow·DeleteRow·RevertRow는 호출마다 한 단계.
    /// </summary>
    public IDisposable BeginUndoStep()
    {
        if (_stepDepth++ == 0)
            _step = new Step();
        return new StepScope(this);
    }

    /// <summary>마지막 단계를 되돌린다. 되돌릴 것이 없으면 false. 행 상태 규칙(Spec 4.4)은 행 모습을 그대로 되살려 지킨다.</summary>
    public bool Undo()
    {
        if (_undo.Last is not { } node)
            return false;
        _undo.RemoveLast();
        Restore(node.Value.Before);
        _redo.Push(node.Value);
        return true;
    }

    /// <summary>되돌린 단계를 다시 적용한다. 없으면 false.</summary>
    public bool Redo()
    {
        if (!_redo.TryPop(out var step))
            return false;
        Restore(step.After);
        _undo.AddLast(step);
        return true;
    }

    private void EndUndoStep()
    {
        if (--_stepDepth > 0)
            return;
        var step = _step!;
        _step = null;
        if (step.Before.Count == 0)
            return;

        foreach (var row in step.Before.Keys)
            step.After[row] = Image(row);
        _undo.AddLast(step);
        if (_undo.Count > UndoLimit)
            _undo.RemoveFirst();
        _redo.Clear();
    }

    private void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>열린 단계에서 처음 건드리는 행이면 바뀌기 전 모습을 남긴다.</summary>
    private void Touch(Row row)
    {
        if (_step is not null && !_step.Before.ContainsKey(row))
            _step.Before[row] = Image(row);
    }

    private RowImage Image(Row row) => new(_rows.IndexOf(row), Snapshot(row.Item), row.State);

    /// <summary>행들을 남긴 모습으로 되살린다: 목록에 없던 행은 빼고, 있던 행은 그 위치에 값·상태와 함께.</summary>
    private void Restore(Dictionary<Row, RowImage> images)
    {
        foreach (var (row, image) in images.Where(x => x.Value.Index < 0))
            if (_byKey.TryGetValue(row.Key, out var current))
                Remove(current);

        foreach (var (row, image) in images.Where(x => x.Value.Index >= 0).OrderBy(x => x.Value.Index))
        {
            // 페이징으로 놓았다가 다시 적재한 행은 새 객체일 수 있어 행 키로 찾는다
            var returned = !_byKey.TryGetValue(row.Key, out var target);
            var before = returned ? null : Snapshot(target!.Item);
            if (returned)
            {
                target = row;
                _rows.Insert(Math.Min(image.Index, _rows.Count), target);
                _byKey.Add(target.Key, target);
                if (_idOf is not null && target.State != RowState.Inserted && image.State != RowState.Inserted)
                {
                    var id = _idOf(target.Item);
                    _keyById[id] = target.Key;
                    _idByKey[target.Key] = id;
                }
            }
            foreach (var (field, value) in image.Values)
                Fields[field].SetValue(target!.Item, value);
            target!.State = image.State;
            target.Revision++;

            if (returned)
                RowAdded?.Invoke(this, new DataSetRowEventArgs(target.Key));
            else
                RaiseValueChanges(target.Key, before!, target.Item);
        }
    }

    /// <summary>호출 하나를 한 단계로 (열린 범위가 있으면 거기에 합쳐진다).</summary>
    private StepScope Record() => (StepScope)BeginUndoStep();

    private sealed class Step
    {
        public Dictionary<Row, RowImage> Before { get; } = [];
        public Dictionary<Row, RowImage> After { get; } = [];
    }

    /// <summary>행 모습. Index가 -1이면 목록에 없음.</summary>
    private sealed record RowImage(int Index, Dictionary<string, object?> Values, RowState State);

    private sealed class StepScope(QuroDataSet<T> owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            owner.EndUndoStep();
        }
    }

    // ── 내부 ──

    private void Append(Row row)
    {
        _rows.Add(row);
        _byKey.Add(row.Key, row);
        if (_idOf is not null && row.State != RowState.Inserted)
        {
            var id = _idOf(row.Item);
            _keyById[id] = row.Key;
            _idByKey[row.Key] = id;
        }
    }

    private void Remove(Row row, bool notify = true)
    {
        var index = _rows.IndexOf(row);
        _rows.RemoveAt(index);
        Forget(row);
        if (_current == row.Key)
            SetCurrent(index < _rows.Count ? _rows[index].Key : index > 0 ? _rows[index - 1].Key : null);
        if (notify)
            RowDeleted?.Invoke(this, new DataSetRowEventArgs(row.Key));
    }

    /// <summary>키·ID 색인에서 뺀다 (목록에서는 호출한 쪽이 뺀다).</summary>
    private void Forget(Row row)
    {
        _byKey.Remove(row.Key);
        if (_idByKey.Remove(row.Key, out var id))
            _keyById.Remove(id);
    }

    private void Revert(Row row)
    {
        if (row.State == RowState.Inserted)
        {
            Remove(row);
            return;
        }

        var before = Snapshot(row.Item);
        foreach (var (field, value) in row.Original!)
            Fields[field].SetValue(row.Item, value);
        row.State = RowState.Normal;
        row.Revision++;
        RaiseValueChanges(row.Key, before, row.Item);
    }

    private Row Find(int rowKey) =>
        _byKey.TryGetValue(rowKey, out var row)
            ? row
            : throw new KeyNotFoundException($"행 키 {rowKey}가 없습니다.");

    // Screen definition ids are camelCase while DTO properties are PascalCase (design v0.2 P2): exact name first, then ignoring case.
    private static PropertyInfo Field(string field) =>
        Fields.TryGetValue(field, out var prop) || FieldsIgnoreCase.TryGetValue(field, out prop)
            ? prop
            : throw new ArgumentException($"{typeof(T).Name}에 필드 '{field}'가 없습니다.", nameof(field));

    private static T Clone(T source)
    {
        var item = Activator.CreateInstance<T>();
        foreach (var prop in Fields.Values)
            prop.SetValue(item, prop.GetValue(source));
        return item;
    }

    private static Dictionary<string, object?> Snapshot(T item) =>
        Fields.Values.ToDictionary(p => p.Name, p => p.GetValue(item));

    /// <summary>A blank row (AddBlankRow) whose values are still the ones it was added with.</summary>
    private static bool Untouched(Row row) =>
        row.Blank is { } blank && row.State == RowState.Inserted && Fields.Values.All(p => Equals(p.GetValue(row.Item), blank[p.Name]));

    private static bool MatchesOriginal(Row row) =>
        Fields.Values.All(p => Equals(p.GetValue(row.Item), row.Original![p.Name]));

    private static decimal? ToDecimal(object? value) => value switch
    {
        decimal d => d,
        int or long or short or byte or float or double => Convert.ToDecimal(value),
        _ => null,
    };

    private static bool TryParse(string? text, Type type, IFormatProvider culture, out object? value)
    {
        value = null;
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(string))
        {
            value = text;
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
            return Nullable.GetUnderlyingType(type) is not null;

        const NumberStyles Integer = NumberStyles.Integer | NumberStyles.AllowThousands;
        const NumberStyles Float = NumberStyles.Float | NumberStyles.AllowThousands;
        text = text.Trim();

        bool ok;
        (ok, value) = target switch
        {
            _ when target == typeof(decimal) => (decimal.TryParse(text, NumberStyles.Number, culture, out var v), v),
            _ when target == typeof(int) => (int.TryParse(text, Integer, culture, out var v), v),
            _ when target == typeof(long) => (long.TryParse(text, Integer, culture, out var v), v),
            _ when target == typeof(double) => (double.TryParse(text, Float, culture, out var v), v),
            _ when target == typeof(DateOnly) => (DateOnly.TryParse(text, culture, DateTimeStyles.None, out var v), v),
            _ when target == typeof(DateTime) => (DateTime.TryParse(text, culture, DateTimeStyles.None, out var v), v),
            _ when target == typeof(bool) => (bool.TryParse(text, out var v), v),
            _ => (false, (object?)null),
        };
        if (!ok)
            value = null;
        return ok;
    }

    private List<ChangedRow<T>> ItemsIn(RowState state) =>
        _rows.Where(r => r.State == state && !Untouched(r)).Select(r => new ChangedRow<T>(r.Key, r.Item)).ToList();

    private sealed class Row(int key, T item, RowState state, Dictionary<string, object?>? original)
    {
        public int Key { get; } = key;
        public T Item { get; } = item;
        public RowState State { get; set; } = state;
        public int Revision { get; set; }

        /// <summary>조회(또는 마지막 AcceptChanges) 시점 값. 추가 행은 null.</summary>
        public Dictionary<string, object?>? Original { get; set; } = original;

        /// <summary>Values a blank row was added with (AddBlankRow), or null.</summary>
        public Dictionary<string, object?>? Blank { get; set; }

        /// <summary>앱이 지정한 셀 표시. 행과 함께 사라진다.</summary>
        public Dictionary<string, CellMark> Marks { get; } = [];
    }
}
