namespace Quro.Grid.Definition;

/// <summary>
/// A code-help definition from a <c>{id}.lookup.toon</c> file (Addendum A-48, lookup design v0.3 chapter 4): the popup
/// title and columns, the queries and their arguments, and which column is the code. Immutable. Data is queried by the
/// app; QuroGrid only reads and checks the definition.
/// </summary>
/// <param name="Id">Lookup id (camelCase), the same as the file name</param>
/// <param name="Title">Resource key of the popup title</param>
/// <param name="Version">Version of this definition (1 or more)</param>
/// <param name="Api">Opaque path the app queries; starts with <c>/</c></param>
/// <param name="Key">Result column whose value goes into the cell</param>
/// <param name="Text">Result column shown as the name next to the code, or null</param>
/// <param name="MinChars">Typed characters needed before the automatic search on Tab (A-05); 0 searches empty input too</param>
/// <param name="Perm">Permission id needed to use the lookup, or null</param>
/// <param name="Params">Query arguments</param>
/// <param name="Queries">Named queries; the first one is used when a screen column has no lookup rule</param>
/// <param name="Columns">Popup columns, which are also the result fields</param>
/// <param name="Source">Where the <c>lookup</c> section is</param>
public sealed record LookupDef(string Id, string Title, int Version, string Api, string Key, string? Text, int MinChars,
    string? Perm, IReadOnlyList<LookupParamDef> Params, IReadOnlyList<LookupQueryDef> Queries,
    IReadOnlyList<LookupColumnDef> Columns, SourceInfo Source)
{
    /// <summary>Line of each <c>lookup</c> key, for errors about a key.</summary>
    internal IReadOnlyDictionary<string, int> Lines { get; init; } = new Dictionary<string, int>();

    internal int LineOf(string key) => Lines.TryGetValue(key, out var line) ? line : Source.Line;
}

/// <summary>A query argument.</summary>
/// <param name="Id">Argument id</param>
/// <param name="Type">Value type</param>
/// <param name="Req">The query needs a value</param>
/// <param name="Source">Where the row is</param>
public sealed record LookupParamDef(string Id, LookupParamType Type, bool Req, SourceInfo Source);

/// <summary>A named query, called from screen rules as <c>{lookupId}.{query}(arguments)</c>.</summary>
/// <param name="Id">Query id</param>
/// <param name="Params">Argument ids in call order</param>
/// <param name="Source">Where the row is</param>
public sealed record LookupQueryDef(string Id, IReadOnlyList<string> Params, SourceInfo Source);

/// <summary>A popup column and result field. Screen lookup rules fill grid columns with the same id.</summary>
/// <param name="Id">Result field id</param>
/// <param name="Label">Resource key of the header</param>
/// <param name="Type">Text, Number, Money, Date or Checkbox</param>
/// <param name="Width">Width in pixels (more than 0)</param>
/// <param name="Fmt">Display format, or null</param>
/// <param name="Source">Where the row is</param>
public sealed record LookupColumnDef(string Id, string Label, ColumnType Type, int Width, string? Fmt, SourceInfo Source);

/// <summary>Type of a query argument.</summary>
public enum LookupParamType
{
    /// <summary>Text</summary>
    Text,
    /// <summary>Number</summary>
    Number,
    /// <summary>Date</summary>
    Date,
}
