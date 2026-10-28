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
        ["sample.changes"] = ("변경 행", "Changed rows"),
    };

    public LocalizedString this[string name] =>
        Texts.TryGetValue(name, out var t)
            ? new LocalizedString(name, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? t.Ko : t.En)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Texts.Keys.Select(k => this[k]);
}
