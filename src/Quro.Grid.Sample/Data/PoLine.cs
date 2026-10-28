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
    public string Unit { get; set; } = "EA";        // 단위 (Combo, 공통코드)
    public string Urgent { get; set; } = "N";       // 긴급 (Check, Y/N)
    public int Version { get; set; }

    // 붙여넣기 성능 화면(?wide=1, GS-057)용 텍스트 열
    public string Remark { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Maker { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Lot { get; set; } = "";
    public string Location { get; set; } = "";
}
