namespace Quro.Grid.Data;

/// <summary>저장용 변경분 (Spec 4.4). 서버는 삭제 → 수정 → 추가 순서로 처리한다.</summary>
public sealed class ChangeSet<T> where T : class
{
    public IReadOnlyList<T> Inserted { get; init; } = [];
    public IReadOnlyList<T> Updated { get; init; } = [];
    public IReadOnlyList<T> Deleted { get; init; } = [];

    public bool IsEmpty => Inserted.Count == 0 && Updated.Count == 0 && Deleted.Count == 0;
}
