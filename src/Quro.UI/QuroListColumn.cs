namespace Quro.UI;

/// <summary>Extra column in an open <see cref="QuroComboBox{TItem, TCode}"/> list (Control Spec 9, A-64).</summary>
/// <param name="Field">Field of the code row.</param>
/// <param name="HeaderKey">Resource key of the column header. Null leaves the header empty.</param>
public sealed record QuroListColumn(string Field, string? HeaderKey = null);
