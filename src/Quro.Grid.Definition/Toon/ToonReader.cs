using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Quro.Grid.Definition.Toon;

/// <summary>
/// Strict reader for the TOON subset used by screen definitions (design v0.2 chapter 3, T-06): two-space indentation,
/// comma delimiter, comments only on their own line. Syntax problems are V-01 errors; reading continues so that
/// every problem in a file is reported at once.
/// </summary>
internal sealed partial class ToonReader
{
    private const string Code = "V-01";
    private readonly string _file;
    private readonly List<DefinitionError> _errors;
    private readonly List<SourceLine> _lines = [];
    private int _pos;

    private ToonReader(string file, List<DefinitionError> errors)
    {
        _file = file;
        _errors = errors;
    }

    private sealed record SourceLine(int Indent, string Text, int Number);

    /// <summary>Reads <paramref name="text"/> into an object tree. Errors are added to <paramref name="errors"/>.</summary>
    public static ToonObject Read(string file, string text, List<DefinitionError> errors)
    {
        var reader = new ToonReader(file, errors);
        reader.Split(text);
        return reader.ReadObject(0, 1);
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z][A-Za-z0-9]*)(?:\[(?<n>[^\]]*)\])?(?:\{(?<f>[^}]*)\})?:(?:[ ](?<rest>.*))?$")]
    private static partial Regex EntryPattern();

    [GeneratedRegex(@"^-?\d+(?:\.\d+)?$")]
    private static partial Regex NumberPattern();

    private void Error(int line, string message) => _errors.Add(new DefinitionError(Code, _file, line, message));

    private void Split(string text)
    {
        var raw = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < raw.Length; i++)
        {
            var line = raw[i].TrimEnd();
            var body = line.TrimStart(' ');
            if (body.Length == 0 || body.StartsWith('#'))
                continue;
            if (body.StartsWith('\t'))
            {
                Error(i + 1, "tab in indentation; use two spaces");
                continue;
            }
            var indent = line.Length - body.Length;
            if (indent % 2 != 0)
            {
                Error(i + 1, $"indentation of {indent} spaces is not a multiple of two");
                continue;
            }
            _lines.Add(new SourceLine(indent, body, i + 1));
        }
    }

    private SourceLine? Current => _pos < _lines.Count ? _lines[_pos] : null;

    /// <summary>Reads entries at exactly <paramref name="indent"/> until a shallower line.</summary>
    private ToonObject ReadObject(int indent, int line)
    {
        var entries = new List<ToonEntry>();
        while (Current is { } cur && cur.Indent >= indent)
        {
            if (cur.Indent > indent)
            {
                Error(cur.Number, "unexpected indentation");
                _pos++;
                continue;
            }
            if (ReadEntry(indent) is { } entry)
                entries.Add(entry);
        }
        return new ToonObject(entries, line);
    }

    private ToonEntry? ReadEntry(int indent)
    {
        var cur = _lines[_pos++];
        var m = EntryPattern().Match(cur.Text);
        if (!m.Success)
        {
            Error(cur.Number, "expected 'key: value', 'key:', 'key[N]: ...' or 'key[N]{fields}:'");
            SkipDeeper(indent);
            return null;
        }

        var key = m.Groups["key"].Value;
        var rest = m.Groups["rest"].Success ? m.Groups["rest"].Value : "";

        if (!m.Groups["n"].Success)
        {
            if (m.Groups["f"].Success)
            {
                Error(cur.Number, $"'{key}' has a field header without a length; write {key}[N]{{...}}:");
                SkipDeeper(indent);
                return null;
            }
            if (rest.Length > 0)
                return new ToonEntry(key, ParseScalar(rest, cur.Number), cur.Number);
            return new ToonEntry(key, ReadObject(indent + 2, cur.Number), cur.Number);
        }

        if (!int.TryParse(m.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            Error(cur.Number, $"'{key}[{m.Groups["n"].Value}]': the length must be a number");
            SkipDeeper(indent);
            return null;
        }

        if (m.Groups["f"].Success)
        {
            if (rest.Length > 0)
                Error(cur.Number, $"'{key}': table rows go on the following lines");
            return new ToonEntry(key, ReadTable(key, count, m.Groups["f"].Value, indent, cur.Number), cur.Number);
        }

        if (rest.Length > 0)
        {
            var items = SplitCells(rest, cur.Number);
            if (items.Count != count)
                Error(cur.Number, $"{key}[{count}] has {items.Count} items");
            return new ToonEntry(key, new ToonArray(items, cur.Number), cur.Number);
        }

        return new ToonEntry(key, ReadList(key, count, indent, cur.Number), cur.Number);
    }

    private ToonTable ReadTable(string key, int count, string header, int indent, int line)
    {
        var fields = header.Split(',').Select(f => f.Trim()).ToList();
        var rows = new List<ToonRow>();
        var found = 0;
        while (Current is { } cur && cur.Indent > indent)
        {
            _pos++;
            if (cur.Indent != indent + 2)
            {
                Error(cur.Number, $"'{key}' rows must be indented two spaces more than the header");
                continue;
            }
            found++;
            var cells = SplitCells(cur.Text, cur.Number);
            if (cells.Count != fields.Count)
            {
                Error(cur.Number, $"{key} row has {cells.Count} values, header has {fields.Count}");
                continue;
            }
            rows.Add(new ToonRow(cells, cur.Number));
        }
        if (found != count)
            Error(line, $"{key}[{count}] declares {count} rows, found {found}");
        return new ToonTable(fields, rows, line);
    }

    private ToonArray ReadList(string key, int count, int indent, int line)
    {
        var items = new List<ToonNode>();
        while (Current is { } cur && cur.Indent > indent)
        {
            if (cur.Indent != indent + 2 || !cur.Text.StartsWith("- ", StringComparison.Ordinal))
            {
                Error(cur.Number, $"'{key}' items must start with '- ' two spaces deeper than '{key}'");
                _pos++;
                SkipDeeper(cur.Indent);
                continue;
            }
            // "- k: v" is the first entry of an object whose other entries sit two spaces further in.
            _lines[_pos] = cur with { Indent = indent + 4, Text = cur.Text[2..] };
            items.Add(ReadObject(indent + 4, cur.Number));
        }
        if (items.Count != count)
            Error(line, $"{key}[{count}] declares {count} items, found {items.Count}");
        return new ToonArray(items, line);
    }

    private void SkipDeeper(int indent)
    {
        while (Current is { } cur && cur.Indent > indent)
            _pos++;
    }

    private ToonScalar ParseScalar(string text, int line)
    {
        if (text.StartsWith('"'))
        {
            var cells = SplitCells(text, line);
            if (cells.Count != 1)
                Error(line, "unexpected text after the quoted value");
            return cells[0];
        }
        return Unquoted(text, line);
    }

    /// <summary>Splits a comma-delimited row. Quoted cells keep commas, colons and '#'; escapes are \" and \\.</summary>
    private List<ToonScalar> SplitCells(string text, int line)
    {
        var cells = new List<ToonScalar>();
        var i = 0;
        while (true)
        {
            if (i < text.Length && text[i] == '"')
            {
                var sb = new StringBuilder();
                i++;
                var closed = false;
                while (i < text.Length)
                {
                    var c = text[i++];
                    if (c == '\\' && i < text.Length && text[i] is '"' or '\\')
                        sb.Append(text[i++]);
                    else if (c == '"')
                    {
                        closed = true;
                        break;
                    }
                    else
                        sb.Append(c);
                }
                if (!closed)
                    Error(line, "unterminated quoted value");
                cells.Add(new ToonScalar(sb.ToString(), true, line));
                if (i < text.Length && text[i] != ',')
                {
                    Error(line, "expected ',' after a quoted value");
                    i = text.IndexOf(',', i) is var next and >= 0 ? next : text.Length;
                }
            }
            else
            {
                var end = text.IndexOf(',', i);
                if (end < 0)
                    end = text.Length;
                cells.Add(Unquoted(text[i..end], line));
                i = end;
            }

            if (i >= text.Length)
                return cells;
            i++; // the comma
        }
    }

    private static ToonScalar Unquoted(string text, int line)
    {
        var s = text.Trim();
        object? value = s switch
        {
            "" or "null" => null,
            "true" => true,
            "false" => false,
            _ when NumberPattern().IsMatch(s) => decimal.Parse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
            _ => s,
        };
        return new ToonScalar(value, false, line);
    }
}
