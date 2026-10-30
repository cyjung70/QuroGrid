using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Quro.UI;

/// <summary>Multi-language label (Control Spec 11, Addendum A-66).</summary>
public partial class QuroLabel
{
    /// <summary>Resource key of the label text.</summary>
    [Parameter, EditorRequired] public string TextKey { get; set; } = "";

    /// <summary>Resolves <see cref="TextKey"/>. Without it, or without a translation, the key itself is shown.</summary>
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    private string Text => Localizer?[TextKey].Value ?? TextKey;
}
