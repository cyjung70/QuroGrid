namespace Quro.UI;

/// <summary>Characters a text box accepts (Control Spec 8). Filtered in the browser while typing (R2).</summary>
public enum QuroCharType
{
    /// <summary>Any character.</summary>
    Any,

    /// <summary>Digits 0-9 only.</summary>
    Digits,

    /// <summary>Letters of any language only.</summary>
    Alpha,

    /// <summary>Letters of any language and digits 0-9.</summary>
    AlphaNumeric,
}
