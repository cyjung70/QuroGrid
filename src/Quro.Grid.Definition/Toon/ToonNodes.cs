namespace Quro.Grid.Definition.Toon;

// Parsed TOON tree (design v0.2 chapter 3). Every node keeps its 1-based line for error reporting.

internal abstract record ToonNode(int Line);

/// <summary>Scalar: string, bool, decimal or null. <see cref="Quoted"/> tells "true" (string) from true (bool).</summary>
internal sealed record ToonScalar(object? Value, bool Quoted, int Line) : ToonNode(Line);

/// <summary>Object entries in file order.</summary>
internal sealed record ToonObject(IReadOnlyList<ToonEntry> Entries, int Line) : ToonNode(Line);

internal sealed record ToonEntry(string Key, ToonNode Value, int Line);

/// <summary>Primitive array (<c>name[N]: a,b</c>) or list array (<c>name[N]:</c> with <c>- </c> items).</summary>
internal sealed record ToonArray(IReadOnlyList<ToonNode> Items, int Line) : ToonNode(Line);

/// <summary>Tabular array (<c>name[N]{f1,f2}:</c>). Rows with a wrong value count are reported and left out.</summary>
internal sealed record ToonTable(IReadOnlyList<string> Fields, IReadOnlyList<ToonRow> Rows, int Line) : ToonNode(Line);

internal sealed record ToonRow(IReadOnlyList<ToonScalar> Cells, int Line);
