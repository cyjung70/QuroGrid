using System.Globalization;
using System.Text.Json.Nodes;

namespace Quro.Grid.Data.Tests;

/// <summary>GS-020: 저장 값 직렬화 → 날짜는 ISO, 소수점은 '.' (로캘 무관, Spec 4.8)</summary>
public class GS_020_ChangeSetJsonTests
{
    [Fact]
    public void GS_020_ChangeSetJson_IsCultureInvariant()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // 소수점 ',' 로캘

            var ds = TestData.TwoLines();
            ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 1234.5m);
            ds.DeleteRow(ds.RowKeys[1]);
            var row = ds.AddRow();
            ds.SetValue(row, nameof(PoLine.DueDate), new DateOnly(2026, 10, 28));

            var json = JsonNode.Parse(ds.GetChanges().ToJson())!;

            Assert.Equal("1234.5", json["updated"]![0]!["qty"]!.ToJsonString());
            Assert.Equal(7, (int)json["updated"]![0]!["version"]!);
            Assert.Equal(4, (int)json["deleted"]![0]!["version"]!);
            Assert.Equal("2026-10-28", (string)json["inserted"]![0]!["dueDate"]!);
            Assert.Equal(row, (int)json["inserted"]![0]!["tempKey"]!);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
