namespace Quro.Grid.Definition;

/// <summary>
/// What the consumer has registered, for <see cref="ScreenDefValidator"/> (design v0.2 chapter 8). QuroGrid does not read
/// resource files, code tables or APIs itself; the consumer builds this from its own assets.
/// </summary>
public interface IQuroDefinitionCatalog
{
    /// <summary>Resource keys used for labels, titles and messages</summary>
    IReadOnlySet<string> ResourceKeys { get; }

    /// <summary>Common code majors referenced as <c>code:{major}</c></summary>
    IReadOnlySet<string> CodeMajors { get; }

    /// <summary>Lookup ids referenced as <c>lookup:{id}</c>, column <c>lookup</c> and lookup rules</summary>
    IReadOnlySet<string> LookupIds { get; }

    /// <summary>Handler ids referenced by form field <c>on</c></summary>
    IReadOnlySet<string> Handlers { get; }

    /// <summary>Permission ids referenced by <c>perm</c> and <c>readOnlyFor</c></summary>
    IReadOnlySet<string> Permissions { get; }
}
