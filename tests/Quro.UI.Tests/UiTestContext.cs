using Bunit;

namespace Quro.UI.Tests;

/// <summary>
/// Base for QuroUI component tests. Controls import quro-ui.js after the first render, so JS calls run in loose mode;
/// the attach call itself is checked in GS-U03.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    public const string ModulePath = "./_content/Quro.UI/quro-ui.js";

    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
