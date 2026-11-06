using System.Net;

namespace Quro.Grid.Data;

/// <summary>Outcome of a save call.</summary>
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

/// <summary>Result of a save call.</summary>
/// <param name="Status">Outcome</param>
/// <param name="StatusCode">HTTP status of the storage API response, or null when no call was made</param>
public sealed record SaveResult(SaveStatus Status, HttpStatusCode? StatusCode = null);

/// <summary>Outcome of a load call (A-75).</summary>
public enum LoadStatus
{
    /// <summary>The rows were replaced with the response (2xx with a JSON array).</summary>
    Loaded,

    /// <summary>An error response. Nothing changed.</summary>
    Failed,

    /// <summary>A 2xx response that isn't a JSON array of rows. Nothing changed.</summary>
    Invalid,

    /// <summary>A newer LoadAsync, Load or Clear came first, so this response was dropped.</summary>
    Superseded,
}

/// <summary>Result of a load call (A-75).</summary>
/// <param name="Status">Outcome</param>
/// <param name="StatusCode">HTTP status of the response</param>
/// <param name="RowCount">Rows loaded; 0 unless <see cref="LoadStatus.Loaded"/></param>
public sealed record LoadResult(LoadStatus Status, HttpStatusCode? StatusCode = null, int RowCount = 0);
