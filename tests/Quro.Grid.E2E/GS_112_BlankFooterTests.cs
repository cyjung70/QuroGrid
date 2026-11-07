using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Quro.Grid.E2E;

/// <summary>
/// GS-112: a footer keeps its height when all its cells are blank, so the layout doesn't jump when a value appears. /valign has a paged
/// grid whose footer shows a Sum; with ?blank=true its only aggregate is Count, which stays blank on a paged grid (A-76).
/// </summary>
[Collection(SampleCollection.Name)]
public class GS_112_BlankFooterTests(SampleServer server) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { Locale = "en-US" };

    private async Task<double> FooterHeight(string query)
    {
        await Page.GotoAsync($"{server.BaseUrl}/valign{query}");
        await Page.WaitForSelectorAsync("[role=grid][data-quro-grid] [role=row][data-row]");
        return await Page.Locator("[data-footer-field=Qty]").EvaluateAsync<double>("e => e.getBoundingClientRect().height");
    }

    [Fact]
    public async Task GS_112_BlankFooter_IsAsTallAsOneWithAValue()
    {
        var withValue = await FooterHeight("");
        var blank = await FooterHeight("?blank=true");

        await Expect(Page.Locator("[data-footer-field=Qty]")).ToHaveTextAsync("");
        Assert.Equal(withValue, blank);
    }

    [Fact]
    public async Task GS_112_FooterWithAValue_HasTheSameHeightAsARowOfText()
    {
        var height = await FooterHeight("");

        Assert.InRange(height, 20, 30);
    }
}
