namespace Quro.Grid.E2E;

/// <summary>
/// GS-003: Enter 확정·이동 (Spec 4.7).
/// O-05 결정: 기본 오른쪽, 옵션으로 아래. 2026-10-28 결정: 마지막 셀 Enter 시 새 행 추가는 옵션 (기본 끔, 잠정).
/// 샘플 화면 쿼리: enter=down, newrow=1
/// </summary>
public class GS_003_EnterTests(SampleServer server) : GridPageTest(server)
{
    [Fact]
    public async Task GS_003_EnterDefault_MovesRight()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();

        await Page.Keyboard.PressAsync("Enter");
        await ExpectActiveAsync(1, "Qty");

        await Page.Keyboard.TypeAsync("4");
        await Page.Keyboard.PressAsync("Enter");              // 확정 후 오른쪽
        await Expect(Cell(1, "Qty")).ToHaveTextAsync("4");
        await ExpectActiveAsync(1, "UnitPrice");

        await Page.Keyboard.PressAsync("Enter");              // 행 끝 → 다음 행 첫 편집 가능 셀
        await ExpectActiveAsync(2, "ItemCode");
    }

    [Fact]
    public async Task GS_003_EnterDownOption_MovesDown()
    {
        await OpenAsync("?enter=down");
        await Cell(1, "Qty").ClickAsync();

        await Page.Keyboard.TypeAsync("6");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("6");
        await ExpectActiveAsync(2, "Qty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?enter=down")]
    public async Task GS_003_EnterOnLastCell_NewRowOff_Stays(string query)
    {
        await OpenAsync(query);
        await Cell(3, "UnitPrice").ClickAsync();

        await Page.Keyboard.PressAsync("Enter");

        await Page.WaitForTimeoutAsync(300);
        await ExpectActiveAsync(3, "UnitPrice");
        await Expect(Page.Locator("[role=row][data-row]")).ToHaveCountAsync(3);
    }

    [Theory]
    [InlineData("?newrow=1")]
    [InlineData("?enter=down&newrow=1")]
    public async Task GS_003_EnterOnLastCell_NewRowOn_AddsRow(string query)
    {
        await OpenAsync(query);
        await Cell(3, "UnitPrice").ClickAsync();
        await Page.Keyboard.TypeAsync("9");

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Cell(3, "UnitPrice")).ToHaveTextAsync("9");
        await Expect(Page.Locator("[role=row][data-row]")).ToHaveCountAsync(4);
        await ExpectActiveAsync(-1, "ItemCode");               // 새 행(임시 키 -1)의 첫 편집 가능 셀
    }
}
