namespace Quro.Grid.Definition;

// Immutable screen definition model (design v0.2 chapters 4, 5 and 9). Ids and keys are opaque to QuroGrid;
// the consumer resolves resource keys, data sources, handlers and permissions.

/// <summary>One <c>*.screen.toon</c> file.</summary>
/// <param name="Id">Screen id, <c>{module}.{doc}.{screen}</c></param>
/// <param name="Module">Module code</param>
/// <param name="Title">Resource key of the title</param>
/// <param name="Version">Version of this screen definition</param>
/// <param name="Api">API path the consumer saves to and queries</param>
/// <param name="Key">Form field id of the document key</param>
/// <param name="Layout">Screen layout</param>
/// <param name="Perm">Read and write permission ids</param>
/// <param name="LockWhen">Condition that locks the screen, or null</param>
/// <param name="Toolbar">Toolbar commands in display order</param>
/// <param name="Form">Form section, or null for a grid-only screen</param>
/// <param name="Grids">Grids in display order</param>
/// <param name="Source">Where the <c>screen</c> section starts</param>
public sealed record ScreenDef(string Id, string Module, string Title, int Version, string Api, string Key,
    ScreenLayout Layout, PermDef Perm, string? LockWhen, IReadOnlyList<ToolbarCommand> Toolbar,
    FormDef? Form, IReadOnlyList<GridDef> Grids, SourceInfo Source)
{
    /// <summary>Lines of <c>screen</c> and top-level entries (<c>title</c>, <c>perm.write</c>, <c>toolbar</c> ...) for error reports.</summary>
    internal IReadOnlyDictionary<string, int> Lines { get; init; } = new Dictionary<string, int>();

    /// <summary>Line of a <c>screen</c> or top-level entry, or the line of the <c>screen</c> section.</summary>
    internal int LineOf(string key) => Lines.TryGetValue(key, out var line) ? line : Source.Line;
}

/// <summary>Permission ids of a screen.</summary>
/// <param name="Read">Permission to open the screen</param>
/// <param name="Write">Permission to save</param>
public sealed record PermDef(string Read, string Write);

/// <summary>Form section.</summary>
/// <param name="Cols">Number of layout columns (1-4)</param>
/// <param name="Fields">Fields in display order</param>
/// <param name="ReadOnlyFor">Fields made read-only for a permission</param>
public sealed record FormDef(int Cols, IReadOnlyList<FieldDef> Fields, IReadOnlyList<ReadOnlyRule> ReadOnlyFor);

/// <summary>One form field.</summary>
/// <param name="Id">Field id (camelCase)</param>
/// <param name="Label">Resource key, or null for hidden fields</param>
/// <param name="Type">Input type</param>
/// <param name="Mode">Edit mode, resolved per form state by <c>FieldModeResolver</c></param>
/// <param name="Max">Maximum text length, or null</param>
/// <param name="Case">Case conversion on input</param>
/// <param name="Default">Default value: a literal or an <c>@</c> token resolved by the consumer</param>
/// <param name="DataSource">Data source reference (<c>code:</c> or <c>lookup:</c>), or null</param>
/// <param name="Span">Layout columns the field spans (0 for hidden fields)</param>
/// <param name="On">Change handler id, or null</param>
/// <param name="Source">Where the field row is</param>
public sealed record FieldDef(string Id, string? Label, FieldType Type, FieldMode Mode, int? Max, CaseRule Case,
    string? Default, string? DataSource, int Span, string? On, SourceInfo Source);

/// <summary>Fields that become read-only for users with a permission.</summary>
/// <param name="Perm">Permission id</param>
/// <param name="Fields">Field ids</param>
/// <param name="Source">Where the row is defined</param>
public sealed record ReadOnlyRule(string Perm, IReadOnlyList<string> Fields, SourceInfo Source);

/// <summary>One grid.</summary>
/// <param name="Id">Grid id</param>
/// <param name="Bind">Collection name in the consumer's save DTO</param>
/// <param name="RowKey">Business row key field (may be a hidden field without a column)</param>
/// <param name="InitRows">Empty rows added when the screen opens</param>
/// <param name="Clipboard">Whether copy and paste are allowed</param>
/// <param name="PageSize">0 for memory mode, otherwise the server paging size</param>
/// <param name="Frozen">Columns frozen on the left</param>
/// <param name="DeletedRows">How rows marked for deletion are shown</param>
/// <param name="Columns">Columns in display order</param>
/// <param name="Rules">Rules (calc, sum, lookup, set)</param>
/// <param name="Validate">Validation rules</param>
/// <param name="Source">Where the grid item starts</param>
public sealed record GridDef(string Id, string Bind, string RowKey, int InitRows, bool Clipboard, int PageSize,
    int Frozen, DeletedRowsMode DeletedRows, IReadOnlyList<ColumnDef> Columns, IReadOnlyList<RuleDef> Rules,
    IReadOnlyList<ValidateDef> Validate, SourceInfo Source);

/// <summary>One grid column. Optional table fields have their documented defaults when the header omits them.</summary>
/// <param name="Id">Column id, equal to the data set field name</param>
/// <param name="Label">Resource key of the header</param>
/// <param name="Type">Cell type</param>
/// <param name="Width">Width in px</param>
/// <param name="Edit">When the cells can be edited</param>
/// <param name="Req">Required</param>
/// <param name="Fmt">.NET display format, or null</param>
/// <param name="Lookup">Lookup id for code help, or null</param>
/// <param name="Sum">Shown in the footer and subtotals</param>
/// <param name="Hidden">Kept in the data set but not shown</param>
/// <param name="Source">Where the column row is</param>
public sealed record ColumnDef(string Id, string Label, ColumnType Type, int Width, ColumnEdit Edit, bool Req,
    string? Fmt, string? Lookup, bool Sum, bool Hidden, SourceInfo Source)
{
    /// <summary>Text alignment, or null for the type default.</summary>
    public ColumnAlign? Align { get; init; }

    /// <summary>Smallest allowed number (inclusive), or null.</summary>
    public decimal? Min { get; init; }

    /// <summary>Largest allowed number (inclusive), or null.</summary>
    public decimal? Max { get; init; }

    /// <summary>Code list reference (<c>code:</c>) for select columns, or null.</summary>
    public string? DataSource { get; init; }

    /// <summary>Row field that holds the currency code for money columns, or null.</summary>
    public string? Currency { get; init; }

    /// <summary>Header filter allowed.</summary>
    public bool Filter { get; init; } = true;

    /// <summary>Sorting allowed.</summary>
    public bool Sort { get; init; } = true;

    /// <summary>Grouping allowed.</summary>
    public bool Group { get; init; }
}

/// <summary>A grid rule.</summary>
/// <param name="On">Trigger: <c>{columnId}.change</c> or <c>{gridId}.change</c></param>
/// <param name="Action">What the rule does</param>
/// <param name="Targets">Target column or form field ids</param>
/// <param name="Expr">Expression (design 5.7) or lookup call</param>
/// <param name="Source">Where the rule row is</param>
public sealed record RuleDef(string On, RuleAction Action, IReadOnlyList<string> Targets, string Expr, SourceInfo Source);

/// <summary>A grid validation rule.</summary>
/// <param name="Target">Column id</param>
/// <param name="Rule">Rule text (design 5.8), for example <c>gt 0</c></param>
/// <param name="Msg">Resource key of the message</param>
/// <param name="Source">Where the validation row is</param>
public sealed record ValidateDef(string Target, string Rule, string Msg, SourceInfo Source);

/// <summary>Screen layout.</summary>
public enum ScreenLayout
{
    /// <summary>Form above grids.</summary>
    FormGrid,
    /// <summary>Form only.</summary>
    Form,
    /// <summary>Grids only.</summary>
    Grid,
}

/// <summary>Toolbar command (design 4.2).</summary>
public enum ToolbarCommand
{
    /// <summary>Query.</summary>
    Query,
    /// <summary>New document.</summary>
    New,
    /// <summary>Save.</summary>
    Save,
    /// <summary>Delete document.</summary>
    Delete,
    /// <summary>Add a grid row.</summary>
    AddRow,
    /// <summary>Delete grid rows.</summary>
    DeleteRow,
    /// <summary>Revert a grid row.</summary>
    UndoRow,
    /// <summary>Upload CSV rows.</summary>
    UploadCsv,
    /// <summary>Export to xlsx.</summary>
    ExportXlsx,
    /// <summary>Export to CSV.</summary>
    ExportCsv,
    /// <summary>Print.</summary>
    Print,
}

/// <summary>Form field input type (design 5.1).</summary>
public enum FieldType
{
    /// <summary>Single-line text.</summary>
    Text,
    /// <summary>Multi-line text.</summary>
    Textarea,
    /// <summary>Password.</summary>
    Password,
    /// <summary>Drop-down list.</summary>
    Select,
    /// <summary>Radio buttons.</summary>
    Radio,
    /// <summary>Check box.</summary>
    Checkbox,
    /// <summary>Date.</summary>
    Date,
    /// <summary>Number.</summary>
    Number,
    /// <summary>Amount with 2 decimals.</summary>
    Money,
    /// <summary>Amount with 4 decimals.</summary>
    Money4,
    /// <summary>Not shown.</summary>
    Hidden,
}

/// <summary>Field edit mode (design 5.2).</summary>
public enum FieldMode
{
    /// <summary>Optional input.</summary>
    Opt,
    /// <summary>Required input.</summary>
    Req,
    /// <summary>Document key entered by the user: editable and required only for a new document.</summary>
    Key,
    /// <summary>Editable only for a new document.</summary>
    NewOnly,
    /// <summary>Read-only.</summary>
    Ro,
    /// <summary>Assigned by the system (for example document numbering).</summary>
    Auto,
    /// <summary>Not shown.</summary>
    Hidden,
}

/// <summary>Case conversion on input.</summary>
public enum CaseRule
{
    /// <summary>No conversion.</summary>
    None,
    /// <summary>Upper case.</summary>
    Upper,
    /// <summary>Lower case.</summary>
    Lower,
}

/// <summary>Grid column type (design 5.3).</summary>
public enum ColumnType
{
    /// <summary>Text (a popup cell when the column has a lookup).</summary>
    Text,
    /// <summary>Number.</summary>
    Number,
    /// <summary>Amount (currency cell when the column names a currency field).</summary>
    Money,
    /// <summary>Date.</summary>
    Date,
    /// <summary>Code list.</summary>
    Select,
    /// <summary>Check box.</summary>
    Checkbox,
    /// <summary>Button.</summary>
    Button,
}

/// <summary>When a column can be edited (TOON <c>edit</c>: <c>false</c>, <c>true</c>, <c>new</c>).</summary>
public enum ColumnEdit
{
    /// <summary><c>false</c>: never.</summary>
    ReadOnly,
    /// <summary><c>true</c>: always.</summary>
    Editable,
    /// <summary><c>new</c>: only in added rows.</summary>
    NewRowOnly,
}

/// <summary>Column text alignment.</summary>
public enum ColumnAlign
{
    /// <summary>Left.</summary>
    Left,
    /// <summary>Center.</summary>
    Center,
    /// <summary>Right.</summary>
    Right,
}

/// <summary>Grid rule action (design 4.4).</summary>
public enum RuleAction
{
    /// <summary>Set a column of the same row from an expression.</summary>
    Calc,
    /// <summary>Set a form field from an aggregate over the grid.</summary>
    Sum,
    /// <summary>The consumer looks up values and fills the target columns.</summary>
    Lookup,
    /// <summary>Set the target from an expression.</summary>
    Set,
}

/// <summary>How rows marked for deletion are shown (O-02).</summary>
public enum DeletedRowsMode
{
    /// <summary>Hidden.</summary>
    Hide,
    /// <summary>Shown with a strikethrough.</summary>
    Strike,
}
