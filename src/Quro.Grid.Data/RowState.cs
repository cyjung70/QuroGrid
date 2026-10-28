namespace Quro.Grid.Data;

/// <summary>행 상태 (Spec 4.4).</summary>
public enum RowState
{
    /// <summary>조회된 그대로.</summary>
    Normal,

    /// <summary>새로 추가된 행. 값을 바꿔도 Inserted 유지.</summary>
    Inserted,

    /// <summary>조회 후 값이 바뀐 행. 모든 값이 원본과 같아지면 Normal 복귀.</summary>
    Updated,

    /// <summary>삭제 표시된 기존 행. 저장 시 DELETE.</summary>
    Deleted,
}
