namespace Quro.Grid.Sample.Data;

/// <summary>구매오더 라인 (Spec 4.4·4.5 예시).</summary>
public sealed class PoLine
{
    public int LineNo { get; set; }
    public string ItemCode { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? DueDate { get; set; }
    public int Version { get; set; }
}
