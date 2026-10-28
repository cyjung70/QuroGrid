namespace Quro.Grid.Definition;

/// <summary>State of the document a field belongs to (design v0.2 5.2). For grid columns, an added row is <see cref="New"/>.</summary>
public enum FormState
{
    /// <summary>New document, not saved yet</summary>
    New,

    /// <summary>Saved document loaded for editing</summary>
    Loaded,

    /// <summary>Document locked by <c>lockWhen</c>; everything is read-only</summary>
    Locked,
}

/// <summary>How a field is shown in a given state.</summary>
/// <param name="Visible">Shown on screen</param>
/// <param name="Editable">User can change the value</param>
/// <param name="Required">A value must be entered before saving</param>
public readonly record struct FieldState(bool Visible, bool Editable, bool Required);

/// <summary>Resolves a field <see cref="FieldMode"/> to what the user sees (design v0.2 5.2). Pure function shared by forms and grids.</summary>
public static class FieldModeResolver
{
    private static readonly FieldState Hidden = new(false, false, false);
    private static readonly FieldState Read = new(true, false, false);
    private static readonly FieldState Edit = new(true, true, false);
    private static readonly FieldState EditRequired = new(true, true, true);

    /// <summary>Resolves <paramref name="mode"/> for <paramref name="state"/>.</summary>
    /// <param name="mode">Field mode from the definition</param>
    /// <param name="state">Document state (for grid columns: row added = New, otherwise Loaded)</param>
    /// <param name="permReadOnly">The user has a <c>readOnlyFor</c> permission for this field, or no write permission</param>
    public static FieldState Resolve(FieldMode mode, FormState state, bool permReadOnly)
    {
        if (mode == FieldMode.Hidden)
            return Hidden;
        if (permReadOnly || state == FormState.Locked)
            return Read;
        return (mode, state) switch
        {
            (FieldMode.Opt, _) => Edit,
            (FieldMode.Req, _) => EditRequired,
            (FieldMode.Key, FormState.New) => EditRequired,
            (FieldMode.NewOnly, FormState.New) => Edit,
            _ => Read,
        };
    }

    /// <summary>Column <c>edit</c>/<c>req</c> as a field mode (design 5.2): new → newOnly, false → ro, true → req or opt.</summary>
    internal static FieldMode ModeOf(ColumnEdit edit, bool req) => edit switch
    {
        ColumnEdit.NewRowOnly => FieldMode.NewOnly,
        ColumnEdit.ReadOnly => FieldMode.Ro,
        _ => req ? FieldMode.Req : FieldMode.Opt,
    };
}
