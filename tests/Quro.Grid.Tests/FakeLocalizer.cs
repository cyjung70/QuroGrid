using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Quro.Grid.Tests;

/// <summary>CurrentUICulture에 따라 ko/en 문자열을 돌려주는 테스트용 리소스.</summary>
internal sealed class FakeLocalizer : IStringLocalizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["ko"] = new()
        {
            ["mm.item.code"] = "품목코드",
            ["mm.qty"] = "수량",
            ["app.stock.minus"] = "가용 재고를 초과했습니다",
            ["mm.detail.open"] = "열기",
            ["sd.so.line.qty"] = "수량",
        },
        ["en"] = new()
        {
            ["mm.item.code"] = "Item Code",
            ["mm.qty"] = "Quantity",
            ["app.stock.minus"] = "Exceeds available stock",
            ["mm.detail.open"] = "Open",
            ["sd.so.line.qty"] = "Quantity",
        },
    };

    public LocalizedString this[string name]
    {
        get
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Texts.TryGetValue(lang, out var map) && map.TryGetValue(name, out var text)
                ? new LocalizedString(name, text)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
