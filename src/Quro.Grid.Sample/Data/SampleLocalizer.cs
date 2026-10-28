using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Quro.Grid.Sample.Data;

/// <summary>샘플용 리소스 (ko/en). 실제 앱은 resx 등 IStringLocalizer 구현을 쓴다.</summary>
public sealed class SampleLocalizer : IStringLocalizer
{
    private static readonly Dictionary<string, (string Ko, string En)> Texts = new()
    {
        ["mm.line_no"] = ("행", "Line"),
        ["mm.item.code"] = ("품목코드", "Item Code"),
        ["mm.qty"] = ("수량", "Quantity"),
        ["mm.unit_price"] = ("단가", "Unit Price"),
        ["mm.amount"] = ("금액", "Amount"),
        ["sample.po_lines.title"] = ("구매오더 라인", "Purchase Order Lines"),
        ["mm.due_date"] = ("납기일", "Due Date"),
        ["sample.changes"] = ("변경 행", "Changed rows"),
        ["sample.pastes"] = ("붙여넣기", "Pastes"),
        ["sample.paste_skipped"] = ("건너뛴 셀", "Skipped cells"),
        ["sample.message"] = ("안내", "Message"),
        ["sample.loaded_rows"] = ("적재 행 (DataSet)", "Loaded rows (DataSet)"),
        ["sample.provider_calls"] = ("조회 호출", "Query calls"),
        ["sample.refresh_status"] = ("상태 갱신", "Refresh status"),
        ["sample.payload"] = ("저장 payload", "Save payload"),
        ["sample.copy_rows"] = ("행 복사", "Copy rows"),
        ["sample.delete_rows"] = ("행 삭제", "Delete rows"),
        ["sample.selected_row"] = ("선택 행", "Selected row"),
        ["mm.remark"] = ("비고", "Remark"),
        ["mm.spec"] = ("규격", "Spec"),
        ["mm.maker"] = ("제조사", "Maker"),
        ["mm.origin"] = ("원산지", "Origin"),
        ["mm.lot"] = ("로트", "Lot"),
        ["mm.location"] = ("위치", "Location"),
        ["mm.unit"] = ("단위", "Unit"),
        ["mm.urgent"] = ("긴급", "Urgent"),
        ["mm.detail"] = ("상세", "Detail"),
        ["mm.detail.open"] = ("열기", "Open"),
        ["sample.unit.ea"] = ("개", "Each"),
        ["sample.unit.box"] = ("상자", "Box"),
        ["sample.unit.kg"] = ("킬로그램", "Kilogram"),
        ["sample.item_help"] = ("품목 코드도움", "Item lookup"),
        ["sample.cancel"] = ("취소", "Cancel"),
        ["sample.popup_text"] = ("코드도움 입력", "Lookup text"),
        ["sample.button_row"] = ("상세 요청 행", "Detail row"),
        ["sample.home"] = ("처음 화면", "Home"),
        ["quro.connection.offline"] = ("서버 연결이 끊겼습니다. 다시 연결하는 동안 입력할 수 없습니다.", "Connection lost. Input is locked while reconnecting."),
        ["quro.connection.unsaved"] = ("연결이 복구되지 않으면 저장하지 않은 변경이 사라집니다.", "Unsaved changes will be lost if the connection is not restored."),
        ["quro.connection.changes_lost"] = ("연결이 끊겨 저장하지 않은 변경이 사라졌습니다. (눌러서 닫기)", "The connection was lost and unsaved changes were discarded. (Click to close)"),
        ["quro.leave.unsaved"] = ("저장하지 않은 변경이 있습니다. 이 화면을 떠날까요?", "You have unsaved changes. Leave this page?"),
        ["quro.paste.readonly"] = ("읽기 전용 셀은 건너뛰었습니다", "Read-only cells were skipped"),
        ["quro.paste.invalid"] = ("형식이 맞지 않는 값은 건너뛰었습니다", "Invalid values were skipped"),
        ["quro.paste.cancelled"] = ("변경이 취소된 셀이 있습니다", "Some changes were cancelled"),
    };

    public LocalizedString this[string name] =>
        Texts.TryGetValue(name, out var t)
            ? new LocalizedString(name, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? t.Ko : t.En)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Texts.Keys.Select(k => this[k]);
}
