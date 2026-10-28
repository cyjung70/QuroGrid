using System.Net;

namespace Quro.Grid.Data;

public enum SaveStatus
{
    /// <summary>변경분이 없어 API를 호출하지 않음</summary>
    NoChanges,

    /// <summary>저장 성공 (2xx). AcceptChanges 완료, 재조회는 앱이 한다.</summary>
    Saved,

    /// <summary>버전 충돌 (409). 변경분 유지, 앱이 재조회를 안내한다.</summary>
    Conflict,

    /// <summary>그 밖의 오류 응답. 변경분 유지.</summary>
    Failed,
}

public sealed record SaveResult(SaveStatus Status, HttpStatusCode? StatusCode = null);
