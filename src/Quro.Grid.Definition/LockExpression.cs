namespace Quro.Grid.Definition;

/// <summary>
/// <c>lockWhen</c> condition (design v0.2 5.6): <c>field in [a,b]</c>, <c>field = a</c>, <c>field != a</c>, joined with
/// <c>and</c> (binds tighter) and <c>or</c>. Values compare as ordinal text. Immutable and thread-safe.
/// </summary>
public sealed class LockExpression
{
    private readonly Func<Func<string, object?>, bool> _eval;

    private LockExpression(Func<Func<string, object?>, bool> eval, IReadOnlySet<string> fields)
    {
        _eval = eval;
        Fields = fields;
    }

    /// <summary>Form field ids the condition reads.</summary>
    internal IReadOnlySet<string> Fields { get; }

    /// <summary>Parses <paramref name="text"/>.</summary>
    /// <exception cref="FormatException">The text is not a lockWhen condition</exception>
    public static LockExpression Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenizer.Read(text, allowBrackets: true);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        var pos = 0;

        Token Next() => tokens[pos++];
        FormatException Fail(Token t, string message) =>
            new($"{message} at position {t.Position + 1}{(t.Kind == TokenKind.End ? " (end)" : $" ('{t.Text}')")}");
        string Value()
        {
            var t = Next();
            return t.Kind is TokenKind.Word or TokenKind.Number ? t.Text : throw Fail(t, "expected a value");
        }

        Func<Func<string, object?>, bool> Condition()
        {
            var field = Next();
            if (field.Kind != TokenKind.Word)
                throw Fail(field, "expected a field id");
            fields.Add(field.Text);
            var op = Next();
            switch (op.Text)
            {
                case "in":
                    if (Next() is var open && open.Text != "[")
                        throw Fail(open, "expected '['");
                    var set = new HashSet<string>(StringComparer.Ordinal) { Value() };
                    while (tokens[pos].Text == ",")
                    {
                        pos++;
                        set.Add(Value());
                    }
                    if (Next() is var close && close.Text != "]")
                        throw Fail(close, "expected ']'");
                    return valueOf => Text(valueOf(field.Text)) is { } v && set.Contains(v);
                case "=":
                    var eq = Value();
                    return valueOf => Text(valueOf(field.Text)) == eq;
                case "!=":
                    var ne = Value();
                    return valueOf => Text(valueOf(field.Text)) != ne;
                default:
                    throw Fail(op, "expected 'in', '=' or '!='");
            }
        }

        Func<Func<string, object?>, bool> And()
        {
            var left = Condition();
            while (tokens[pos].Text == "and")
            {
                pos++;
                var (l, r) = (left, Condition());
                left = valueOf => l(valueOf) && r(valueOf);
            }
            return left;
        }

        var root = And();
        while (tokens[pos].Text == "or")
        {
            pos++;
            var (l, r) = (root, And());
            root = valueOf => l(valueOf) || r(valueOf);
        }
        if (tokens[pos].Kind != TokenKind.End)
            throw Fail(tokens[pos], "unexpected token");
        return new LockExpression(root, fields);
    }

    /// <summary>True when the document is locked.</summary>
    /// <param name="valueOf">Value of a form field (null when empty)</param>
    public bool Evaluate(Func<string, object?> valueOf)
    {
        ArgumentNullException.ThrowIfNull(valueOf);
        return _eval(valueOf);
    }

    private static string? Text(object? value) => value switch
    {
        null => null,
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
