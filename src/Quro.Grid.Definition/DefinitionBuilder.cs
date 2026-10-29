using System.Globalization;
using Quro.Grid.Definition.Toon;

namespace Quro.Grid.Definition;

/// <summary>
/// Maps TOON nodes to values for the definition builders (screens and lookups). Unknown or missing keys and fields are
/// V-02, values of the wrong kind V-03.
/// </summary>
internal abstract class DefinitionBuilder(string file, List<DefinitionError> errors)
{
    protected void Error(string code, int line, string message) => errors.Add(new DefinitionError(code, file, line, message));

    protected SourceInfo At(int line) => new(file, line);

    // ── object entries ──

    protected Dictionary<string, ToonEntry> Entries(ToonObject obj, string section, string[] required, string[] optional)
    {
        var map = new Dictionary<string, ToonEntry>();
        foreach (var e in obj.Entries)
        {
            if (!required.Contains(e.Key) && !optional.Contains(e.Key))
                Error("V-02", e.Line, $"unknown key '{e.Key}' in {section}");
            else if (!map.TryAdd(e.Key, e))
                Error("V-02", e.Line, $"duplicate key '{e.Key}' in {section}");
        }
        foreach (var key in required.Where(k => !map.ContainsKey(k)))
            Error("V-02", obj.Line, $"missing key '{key}' in {section}");
        return map;
    }

    protected ToonObject? Obj(ToonEntry entry)
    {
        if (entry.Value is ToonObject o)
            return o;
        Error("V-02", entry.Line, $"'{entry.Key}' must be a nested object");
        return null;
    }

    protected string? Text(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsText(Scalar(e)) : null;

    protected int? Int(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsInt(Scalar(e), key) : null;

    protected bool? Bool(Dictionary<string, ToonEntry> map, string key) =>
        map.TryGetValue(key, out var e) ? AsBool(Scalar(e), key) : null;

    protected T? Enum<T>(Dictionary<string, ToonEntry> map, string key, Dictionary<string, T> values) where T : struct =>
        map.TryGetValue(key, out var e) ? EnumOf(Scalar(e), key, values) : null;

    protected ToonScalar? Scalar(ToonEntry entry)
    {
        if (entry.Value is ToonScalar s)
            return s;
        Error("V-02", entry.Line, $"'{entry.Key}' must be a single value");
        return null;
    }

    // ── tables ──

    protected sealed record Row(Dictionary<string, ToonScalar> Cells, int Line);

    protected List<Row>? Table(ToonEntry entry, string[] required, string[] optional)
    {
        if (entry.Value is not ToonTable table)
        {
            Error("V-02", entry.Line, $"'{entry.Key}' must be a table: {entry.Key}[N]{{{string.Join(",", required)},...}}:");
            return null;
        }
        foreach (var f in table.Fields.Where(f => !required.Contains(f) && !optional.Contains(f)))
            Error("V-02", table.Line, $"unknown field '{f}' in {entry.Key}");
        foreach (var f in required.Where(f => !table.Fields.Contains(f)))
            Error("V-02", table.Line, $"missing field '{f}' in {entry.Key}");
        if (table.Fields.Distinct().Count() != table.Fields.Count)
            Error("V-02", table.Line, $"duplicate field in {entry.Key} header");

        return table.Rows
            .Select(r => new Row(table.Fields.Zip(r.Cells).DistinctBy(p => p.First).ToDictionary(p => p.First, p => p.Second), r.Line))
            .ToList();
    }

    protected string? Cell(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsText(s) : null;

    protected int? CellInt(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsInt(s, field) : null;

    protected bool? CellBool(Row row, string field) => row.Cells.TryGetValue(field, out var s) ? AsBool(s, field) : null;

    protected decimal? CellDecimal(Row row, string field)
    {
        if (!row.Cells.TryGetValue(field, out var s) || IsEmpty(s))
            return null;
        if (s is { Value: decimal d, Quoted: false })
            return d;
        Error("V-03", s.Line, $"'{field}' must be a number");
        return null;
    }

    protected T? CellEnum<T>(Row row, string field, Dictionary<string, T> values) where T : struct =>
        row.Cells.TryGetValue(field, out var s) ? EnumOf(s, field, values) : null;

    protected ColumnEdit CellEdit(Row row)
    {
        if (!row.Cells.TryGetValue("edit", out var s) || IsEmpty(s))
            return ColumnEdit.Editable;
        return s switch
        {
            { Value: true, Quoted: false } => ColumnEdit.Editable,
            { Value: false, Quoted: false } => ColumnEdit.ReadOnly,
            { Value: "new" } => ColumnEdit.NewRowOnly,
            _ => Invalid(s, "edit", "true, false, new", ColumnEdit.Editable),
        };
    }

    // ── scalar conversion ──

    protected static bool IsEmpty(ToonScalar? s) => s is null || s.Value is null || s.Value is "";

    protected static string? AsText(ToonScalar? s) => IsEmpty(s)
        ? null
        : s!.Value switch
        {
            string str => str,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => null,
        };

    protected int? AsInt(ToonScalar? s, string key)
    {
        if (IsEmpty(s))
            return null;
        if (s is { Value: decimal d, Quoted: false } && d == decimal.Truncate(d) && d is >= int.MinValue and <= int.MaxValue)
            return (int)d;
        Error("V-03", s!.Line, $"'{key}' must be a whole number");
        return null;
    }

    protected bool? AsBool(ToonScalar? s, string key)
    {
        if (IsEmpty(s))
            return null;
        if (s is { Value: bool b, Quoted: false })
            return b;
        Error("V-03", s!.Line, $"'{key}' must be true or false");
        return null;
    }

    protected T? EnumOf<T>(ToonScalar? s, string key, Dictionary<string, T> values) where T : struct
    {
        if (IsEmpty(s))
            return null;
        if (s!.Value is string str && values.TryGetValue(str, out var v))
            return v;
        return Invalid<T?>(s, key, string.Join(", ", values.Keys), null);
    }

    protected T Invalid<T>(ToonScalar s, string key, string allowed, T fallback)
    {
        Error("V-03", s.Line, $"'{key}' value '{AsText(s)}' is not one of: {allowed}");
        return fallback;
    }

    protected static List<string> Ids(string? list) =>
        string.IsNullOrEmpty(list) ? [] : [.. list.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
}
