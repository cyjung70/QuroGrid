namespace Quro.Grid;

/// <summary>quro-grid.js에 넘기는 옵션. 직렬화되어 JS 객체가 된다 (camelCase).</summary>
internal sealed record GridJsOptions(string EnterDirection, bool AddRowOnLastEnter, bool NotifySelection = false, string InstanceId = "", bool Paging = false);
