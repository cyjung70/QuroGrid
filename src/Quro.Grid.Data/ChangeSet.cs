using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quro.Grid.Data;

/// <summary>변경분의 한 행. 추가 행의 <see cref="RowKey"/>는 음수 임시 키 (Spec 4.4 tempKey).</summary>
public sealed record ChangedRow<T>(int RowKey, T Item) where T : class;

/// <summary>저장용 변경분 (Spec 4.4). 서버는 삭제 → 수정 → 추가 순서로 처리한다.</summary>
public sealed class ChangeSet<T> where T : class
{
    /// <summary>Added rows (temporary negative keys).</summary>
    public IReadOnlyList<ChangedRow<T>> Inserted { get; init; } = [];
    /// <summary>Modified rows.</summary>
    public IReadOnlyList<ChangedRow<T>> Updated { get; init; } = [];
    /// <summary>Rows marked for deletion.</summary>
    public IReadOnlyList<ChangedRow<T>> Deleted { get; init; } = [];

    /// <summary>True when there is nothing to save.</summary>
    public bool IsEmpty => Inserted.Count == 0 && Updated.Count == 0 && Deleted.Count == 0;

    /// <summary>
    /// 저장 payload <c>{ inserted[], updated[], deleted[] }</c>. 로캘과 무관하게 ISO 날짜, 소수점 '.'.
    /// 추가 행에는 <c>tempKey</c>를 붙인다.
    /// </summary>
    public string ToJson()
    {
        var payload = new JsonObject
        {
            ["inserted"] = ToArray(Inserted, withTempKey: true),
            ["updated"] = ToArray(Updated, withTempKey: false),
            ["deleted"] = ToArray(Deleted, withTempKey: false),
        };
        return payload.ToJsonString(JsonOptions);
    }

    // camelCase 이름. System.Text.Json은 숫자·DateOnly를 로캘과 무관하게 쓴다.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static JsonArray ToArray(IReadOnlyList<ChangedRow<T>> rows, bool withTempKey)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            var node = JsonSerializer.SerializeToNode(row.Item, JsonOptions)!.AsObject();
            if (withTempKey)
                node["tempKey"] = row.RowKey;
            array.Add(node);
        }
        return array;
    }
}
