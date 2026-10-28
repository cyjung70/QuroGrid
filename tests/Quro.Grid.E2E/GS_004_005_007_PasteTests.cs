namespace Quro.Grid.E2E;

/// <summary>
/// GS-004 · GS-005 · GS-007: 엑셀 붙여넣기 (Spec 4.7·4.8, Addendum A-03·A-04·A-06).
/// 샘플 쿼리: dates=1 (UnitPrice 뒤에 DueDate 열), pasterows=0 (부족 행 추가 끔), culture=de-DE
/// </summary>
public class GS_004_005_007_PasteTests(SampleServer server) : GridPageTest(server)
{
    /// <summary>GS-004: 3행 × 4열 붙여넣기 → 값 반영, 부족 행 자동 추가, 서버 호출 1회 (R3)</summary>
    [Fact]
    public async Task GS_004_ExcelPaste_3x4()
    {
        await OpenAsync("?dates=1");
        await Cell(2, "ItemCode").ClickAsync();
        var baseline = InvokeFrames;

        await PasteAsync("B2\t20\t100\t10/1/2026\r\nB3\t30\t200\t10/2/2026\r\nB4\t40\t300\t10/3/2026\r\n");

        await Expect(Page.Locator("[role=row][data-row]")).ToHaveCountAsync(4);
        await Expect(Cell(2, "ItemCode")).ToHaveTextAsync("B2");
        await Expect(Cell(3, "DueDate")).ToHaveTextAsync("10/2/2026");
        await Expect(Cell(-1, "ItemCode")).ToHaveTextAsync("B4");
        await Expect(Cell(-1, "UnitPrice")).ToHaveTextAsync("300");
        await Expect(Cell(-1, "Amount")).ToHaveTextAsync("12,000");     // 붙여넣기에도 금액 재계산
        await Expect(Status("pastes")).ToHaveTextAsync("1");
        Assert.Equal(1, InvokeFrames - baseline);
    }

    /// <summary>GS-004: 옵션 끔 → 넘치는 행 버림</summary>
    [Fact]
    public async Task GS_004_ExcelPaste_AddRowsOff()
    {
        await OpenAsync("?pasterows=0");
        await Cell(3, "ItemCode").ClickAsync();

        await PasteAsync("X3\r\nX4");

        await Expect(Cell(3, "ItemCode")).ToHaveTextAsync("X3");
        await Page.WaitForTimeoutAsync(300);
        await Expect(Page.Locator("[role=row][data-row]")).ToHaveCountAsync(3);
    }

    /// <summary>GS-004: 범위를 선택했으면 왼쪽 위 셀부터 붙여넣는다</summary>
    [Fact]
    public async Task GS_004_Paste_StartsAtSelectionTopLeft()
    {
        await OpenAsync();
        await Cell(2, "Qty").ClickAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowUp");     // 선택: 1,Qty ~ 2,Qty

        await PasteAsync("11\r\n12");

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("11");
        await Expect(Cell(2, "Qty")).ToHaveTextAsync("12");
    }

    /// <summary>편집 중 붙여넣기는 에디터가 그대로 받는다 (그리드 붙여넣기 아님)</summary>
    [Fact]
    public async Task GS_004_PasteWhileEditing_GoesToEditor()
    {
        await OpenAsync();
        await Cell(1, "ItemCode").ClickAsync();
        await Page.Keyboard.PressAsync("F2");
        await Expect(Editor).ToBeFocusedAsync();

        await PasteAsync("IGNORED");                  // 그리드는 편집 중 paste 이벤트를 가로채지 않는다
        await Page.Keyboard.InsertTextAsync("-X");    // 에디터 쪽 입력은 그대로

        await Expect(Editor).ToHaveValueAsync("A1001-X");
        await Expect(Status("pastes")).ToHaveTextAsync("0");
    }

    /// <summary>GS-005: 읽기 전용 셀은 건너뛰고 앱이 안내 (A-04)</summary>
    [Fact]
    public async Task GS_005_PasteOverReadOnly_SkipsAndNotifies()
    {
        await OpenAsync();
        await Cell(1, "UnitPrice").ClickAsync();

        await PasteAsync("100\t999");

        await Expect(Cell(1, "UnitPrice")).ToHaveTextAsync("100");
        await Expect(Cell(1, "Amount")).ToHaveTextAsync("1,000");       // 999가 아니라 재계산 값
        await Expect(Status("paste-skipped")).ToHaveTextAsync("1");
        await Expect(Status("paste-message")).ToHaveTextAsync("Read-only cells were skipped");
    }

    /// <summary>GS-007: 독일어 로캘 붙여넣기 → 숫자·날짜 해석, 저장 값은 언어 무관 (A-06)</summary>
    [Fact]
    public async Task GS_007_PasteDeLocale_ParsesDecimalAndDate()
    {
        await OpenAsync("?culture=de-DE&dates=1");
        await Cell(1, "Qty").ClickAsync();

        await PasteAsync("1.234,5\t2,5\t28.10.2026");

        await Expect(Cell(1, "Qty")).ToHaveTextAsync("1.234,5");
        await Expect(Cell(1, "DueDate")).ToHaveTextAsync("28.10.2026");
        await Expect(Status("json")).ToContainTextAsync("\"qty\":1234.5");
        await Expect(Status("json")).ToContainTextAsync("\"unitPrice\":2.5");
        await Expect(Status("json")).ToContainTextAsync("\"dueDate\":\"2026-10-28\"");
    }
}
