using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;

namespace Quro.Grid.Data;

/// <summary>
/// 그리드 데이터 모델 (Spec 4.4). 행 상태·원본값·변경분을 관리한다. UI를 참조하지 않는다.
/// </summary>
public sealed class QuroDataSet<T> where T : class
{
    private static readonly Dictionary<string, PropertyInfo> Fields =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name);

    private readonly List<Row> _rows = [];
    private readonly Dictionary<int, Row> _byKey = [];
    private readonly Dictionary<string, FieldRule> _rules = [];
    private int _nextLoadedKey = 1;
    private int _nextInsertedKey = -1;   // 추가 행은 음수 임시 키 (Spec 4.4 tempKey)

    // ── Spec 4.4에 없는 보조 API (행 적재·조회, 검증 규칙 등록) ──

    /// <summary>조회 결과를 적재한다. 모든 행은 Normal.</summary>
    public void Load(IEnumerable<T> rows)
    {
        _rows.Clear();
        _byKey.Clear();
        _nextLoadedKey = 1;
        _nextInsertedKey = -1;

        foreach (var item in rows)
            Append(new Row(_nextLoadedKey++, item, RowState.Normal, Snapshot(item)));
    }

    /// <summary>현재 목록에 있는 행 키 (표시 순서). Deleted 행도 포함한다 (화면 처리는 O-02).</summary>
    public IReadOnlyList<int> RowKeys => _rows.Select(r => r.Key).ToList();

    public object? GetValue(int rowKey, string field) => Field(field).GetValue(Find(rowKey).Item);

    /// <summary>행의 현재 값을 복사한 새 객체 (행 복사용, 목록에는 넣지 않는다).</summary>
    public T CopyOf(int rowKey) => Clone(Find(rowKey).Item);

    /// <summary>행 객체. 값은 <see cref="SetValue"/>로 바꿔야 행 상태가 갱신된다.</summary>
    public T GetItem(int rowKey) => Find(rowKey).Item;

    /// <summary>행의 값·상태·셀 표시가 바뀔 때마다 증가한다. 그리드가 바뀐 행만 다시 그리는 데 쓴다 (R4).</summary>
    public int GetRevision(int rowKey) => Find(rowKey).Revision;

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
            return new SaveResult(SaveStatus.Saved, response.StatusCode);
        }

        var status = response.StatusCode == HttpStatusCode.Conflict ? SaveStatus.Conflict : SaveStatus.Failed;
        return new SaveResult(status, response.StatusCode);
    }

    // ── Spec 4.4 API ──

    public RowState GetState(int rowKey) => Find(rowKey).State;

    /// <summary>원본값. 추가 행은 원본이 없으므로 null.</summary>
    public object? GetOriginal(int rowKey, string field)
    {
        var prop = Field(field);
        var row = Find(rowKey);
        return row.Original is null ? null : row.Original[prop.Name];
    }

    public void SetValue(int rowKey, string field, object? value)
    {
        var prop = Field(field);
        var row = Find(rowKey);
        if (row.State == RowState.Deleted)
            throw new InvalidOperationException($"삭제 표시된 행({rowKey})은 수정할 수 없습니다.");

        prop.SetValue(row.Item, value);
        row.Revision++;

        if (row.State is RowState.Normal or RowState.Updated)
            row.State = MatchesOriginal(row) ? RowState.Normal : RowState.Updated;
    }

    /// <summary>행 추가. 상태는 Inserted. <paramref name="afterRowKey"/>가 있으면 그 행 바로 아래, 없으면 끝.</summary>
    public int AddRow(T? template = null, int? afterRowKey = null)
    {
        var item = template is null ? Activator.CreateInstance<T>() : Clone(template);
        var row = new Row(_nextInsertedKey--, item, RowState.Inserted, original: null);

        if (afterRowKey is null)
            Append(row);
        else
        {
            // 행 복사: 원래 행 바로 아래 (Addendum A-12)
            _rows.Insert(_rows.IndexOf(Find(afterRowKey.Value)) + 1, row);
            _byKey.Add(row.Key, row);
        }
        return row.Key;
    }

    /// <summary>Inserted면 목록에서 제거, 아니면 Deleted.</summary>
    public void DeleteRow(int rowKey)
    {
        var row = Find(rowKey);
        if (row.State == RowState.Inserted)
            Remove(row);
        else
        {
            row.State = RowState.Deleted;
            row.Revision++;
        }
    }

    /// <summary>
    /// 행 단위 변경 취소 (MVP Undo 대체, O-01 잠정안).
    /// Updated·Deleted는 원본값으로 Normal 복귀, Inserted는 목록에서 제거.
    /// </summary>
    public void RevertRow(int rowKey) => Revert(Find(rowKey));

    /// <summary>필수·범위 검증. 저장 대상인 Inserted·Updated 행만 검사한다.</summary>
    public ValidationResult Validate()
    {
        var errors = new List<ValidationError>();

        foreach (var row in _rows.Where(r => r.State is RowState.Inserted or RowState.Updated))
        foreach (var (field, rule) in _rules)
        {
            var value = Fields[field].GetValue(row.Item);

            if (rule.Required && (value is null || value is string s && string.IsNullOrWhiteSpace(s)))
                errors.Add(new ValidationError(row.Key, field, ValidationKeys.Required));

            if (ToDecimal(value) is { } number)
            {
                if (number < rule.Min)
                    errors.Add(new ValidationError(row.Key, field, ValidationKeys.Min));
                if (number > rule.Max)
                    errors.Add(new ValidationError(row.Key, field, ValidationKeys.Max));
            }
        }

        return new ValidationResult { Errors = errors };
    }

    /// <summary>미저장 변경이 있는지 (Normal이 아닌 행). 이탈 경고·연결 끊김 경고에 쓴다 (R7).</summary>
    public bool HasChanges => _rows.Exists(r => r.State != RowState.Normal);

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
        foreach (var row in _rows.ToList())
        {
            if (row.State == RowState.Deleted)
            {
                Remove(row);
                continue;
            }

            row.Original = Snapshot(row.Item);
            row.State = RowState.Normal;
            row.Revision++;
        }
    }

    /// <summary>전체 변경 취소. 각 행에 RevertRow와 같은 규칙을 적용한다.</summary>
    public void RejectChanges()
    {
        foreach (var row in _rows.ToList())
            Revert(row);
    }

    // ── 내부 ──

    private void Append(Row row)
    {
        _rows.Add(row);
        _byKey.Add(row.Key, row);
    }

    private void Remove(Row row)
    {
        _rows.Remove(row);
        _byKey.Remove(row.Key);
    }

    private void Revert(Row row)
    {
        if (row.State == RowState.Inserted)
        {
            Remove(row);
            return;
        }

        foreach (var (field, value) in row.Original!)
            Fields[field].SetValue(row.Item, value);
        row.State = RowState.Normal;
        row.Revision++;
    }

    private Row Find(int rowKey) =>
        _byKey.TryGetValue(rowKey, out var row)
            ? row
            : throw new KeyNotFoundException($"행 키 {rowKey}가 없습니다.");

    private static PropertyInfo Field(string field) =>
        Fields.TryGetValue(field, out var prop)
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
        _rows.Where(r => r.State == state).Select(r => new ChangedRow<T>(r.Key, r.Item)).ToList();

    private sealed class Row(int key, T item, RowState state, Dictionary<string, object?>? original)
    {
        public int Key { get; } = key;
        public T Item { get; } = item;
        public RowState State { get; set; } = state;
        public int Revision { get; set; }

        /// <summary>조회(또는 마지막 AcceptChanges) 시점 값. 추가 행은 null.</summary>
        public Dictionary<string, object?>? Original { get; set; } = original;

        /// <summary>앱이 지정한 셀 표시. 행과 함께 사라진다.</summary>
        public Dictionary<string, CellMark> Marks { get; } = [];
    }
}
