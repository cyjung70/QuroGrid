namespace Quro.Grid.Definition;

/// <summary>Location of a definition in its file (1-based line).</summary>
/// <param name="File">File path as given to the loader</param>
/// <param name="Line">1-based line number</param>
public sealed record SourceInfo(string File, int Line);

/// <summary>A problem found while loading or validating a definition (design v0.2 chapter 8).</summary>
/// <param name="Code">Check id, for example <c>V-01</c></param>
/// <param name="File">File path as given to the loader</param>
/// <param name="Line">1-based line number</param>
/// <param name="Message">English description for logs and CI output</param>
public sealed record DefinitionError(string Code, string File, int Line, string Message)
{
    /// <summary><c>file:line: code message</c>, one line per error (CI annotation format).</summary>
    public override string ToString() => $"{File}:{Line}: {Code} {Message}";
}

/// <summary>Result of loading a definition: the value when there are no errors, otherwise all errors found.</summary>
public sealed class DefinitionResult<T> where T : class
{
    internal DefinitionResult(T? value, IReadOnlyList<DefinitionError> errors)
    {
        Errors = errors;
        Value = errors.Count == 0 ? value : null;
    }

    /// <summary>The loaded definition, or null when <see cref="Errors"/> is not empty.</summary>
    public T? Value { get; }

    /// <summary>Every error found, in file order.</summary>
    public IReadOnlyList<DefinitionError> Errors { get; }
}
