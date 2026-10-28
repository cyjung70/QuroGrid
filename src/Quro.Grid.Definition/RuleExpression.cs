using System.Globalization;

namespace Quro.Grid.Definition;

/// <summary>
/// Whitelisted arithmetic for rule <c>expr</c> (design v0.2 5.7): numbers, column ids, <c>+ - * /</c>, parentheses and
/// <c>sum min max round abs coalesce</c>. No strings, comparisons, member access or other functions, so an expression
/// cannot run arbitrary code (H7). Parse once and share: instances are immutable and thread-safe.
/// </summary>
public sealed class RuleExpression
{
    private static readonly HashSet<string> Functions = ["sum", "min", "max", "round", "abs", "coalesce"];

    private readonly Node _root;

    private RuleExpression(Node root, IReadOnlySet<string> identifiers)
    {
        _root = root;
        Identifiers = identifiers;
    }

    /// <summary>Column ids the expression reads.</summary>
    internal IReadOnlySet<string> Identifiers { get; }

    /// <summary>Parses <paramref name="expr"/>.</summary>
    /// <exception cref="FormatException">The text is not a whitelisted expression</exception>
    public static RuleExpression Parse(string expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var parser = new Parser(Tokenizer.Read(expr, allowBrackets: false));
        var root = parser.Expr();
        parser.End();
        return new RuleExpression(root, parser.Ids);
    }

    /// <summary>
    /// Evaluates in <see cref="decimal"/>. Empty values count as 0 (R11). Returns null only when dividing by zero, which
    /// the caller shows as a warning; nothing throws.
    /// </summary>
    /// <param name="valueOf">Value of a column in the current row</param>
    /// <param name="columnValues">All values of a column, for <c>sum(column)</c>; required only when the expression uses sum</param>
    public decimal? Evaluate(Func<string, decimal?> valueOf, Func<string, IEnumerable<decimal?>>? columnValues = null)
    {
        ArgumentNullException.ThrowIfNull(valueOf);
        return _root.Eval(new Context(valueOf, columnValues));
    }

    private sealed record Context(Func<string, decimal?> ValueOf, Func<string, IEnumerable<decimal?>>? ColumnValues);

    // Eval returns null only for division by zero; the null then propagates to the result.
    private abstract record Node
    {
        public abstract decimal? Eval(Context ctx);
    }

    private sealed record Number(decimal Value) : Node
    {
        public override decimal? Eval(Context ctx) => Value;
    }

    private sealed record Id(string Name) : Node
    {
        public override decimal? Eval(Context ctx) => ctx.ValueOf(Name) ?? 0m;
    }

    private sealed record Negate(Node Operand) : Node
    {
        public override decimal? Eval(Context ctx) => -Operand.Eval(ctx);
    }

    private sealed record Binary(char Op, Node Left, Node Right) : Node
    {
        public override decimal? Eval(Context ctx)
        {
            if (Left.Eval(ctx) is not { } l || Right.Eval(ctx) is not { } r)
                return null;
            return Op switch
            {
                '+' => l + r,
                '-' => l - r,
                '*' => l * r,
                _ => r == 0 ? null : l / r,
            };
        }
    }

    private sealed record Call(string Name, IReadOnlyList<Node> Args) : Node
    {
        public override decimal? Eval(Context ctx)
        {
            if (Name == "sum")
            {
                var column = ((Id)Args[0]).Name;
                var values = ctx.ColumnValues?.Invoke(column)
                    ?? throw new InvalidOperationException($"sum({column}) needs columnValues");
                return values.Sum(v => v ?? 0m);
            }
            if (Name == "coalesce")
            {
                // First argument with a value; a column counts only when it is not empty.
                foreach (var arg in Args)
                    if ((arg is Id id ? ctx.ValueOf(id.Name) : arg.Eval(ctx)) is { } v)
                        return v;
                return 0m;
            }

            var values2 = new List<decimal>(Args.Count);
            foreach (var arg in Args)
            {
                if (arg.Eval(ctx) is not { } v)
                    return null;
                values2.Add(v);
            }
            return Name switch
            {
                "min" => values2.Min(),
                "max" => values2.Max(),
                "abs" => Math.Abs(values2[0]),
                // Half away from zero, the usual business rounding. Saved values are rounded by the server (T-13).
                _ => Math.Round(values2[0], values2.Count > 1 ? (int)values2[1] : 0, MidpointRounding.AwayFromZero),
            };
        }
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _pos;

        public HashSet<string> Ids { get; } = new(StringComparer.Ordinal);

        private Token Peek => tokens[_pos];

        private Token Next() => tokens[_pos++];

        private static FormatException Fail(Token t, string message) =>
            new($"{message} at position {t.Position + 1}");

        private void Expect(string text)
        {
            if (Peek.Text != text)
                throw Fail(Peek, $"expected '{text}'");
            _pos++;
        }

        public void End()
        {
            if (Peek.Kind != TokenKind.End)
                throw Fail(Peek, $"unexpected '{Peek.Text}'");
        }

        public Node Expr()
        {
            var node = Term();
            while (Peek.Text is "+" or "-")
                node = new Binary(Next().Text[0], node, Term());
            return node;
        }

        private Node Term()
        {
            var node = Factor();
            while (Peek.Text is "*" or "/")
                node = new Binary(Next().Text[0], node, Factor());
            return node;
        }

        private Node Factor()
        {
            var t = Next();
            switch (t.Kind)
            {
                case TokenKind.Number:
                    return new Number(decimal.Parse(t.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));
                case TokenKind.Word when Peek.Text == "(":
                    return Function(t);
                case TokenKind.Word:
                    Ids.Add(t.Text);
                    return new Id(t.Text);
                case TokenKind.Symbol when t.Text == "-":
                    return new Negate(Factor());
                case TokenKind.Symbol when t.Text == "(":
                    var inner = Expr();
                    Expect(")");
                    return inner;
                default:
                    throw Fail(t, t.Kind == TokenKind.End ? "unexpected end" : $"unexpected '{t.Text}'");
            }
        }

        private Node Function(Token name)
        {
            if (!Functions.Contains(name.Text))
                throw Fail(name, $"function '{name.Text}' is not allowed (allowed: {string.Join(", ", Functions)})");
            Expect("(");
            var args = new List<Node> { Expr() };
            while (Peek.Text == ",")
            {
                _pos++;
                args.Add(Expr());
            }
            Expect(")");

            var (min, max) = name.Text switch
            {
                "sum" or "abs" => (1, 1),
                "round" => (1, 2),
                _ => (1, int.MaxValue),
            };
            if (args.Count < min || args.Count > max)
                throw Fail(name, $"{name.Text} takes {(min == max ? min.ToString(CultureInfo.InvariantCulture) : $"{min} to {max}")} argument(s)");
            if (name.Text == "sum" && args[0] is not Id)
                throw Fail(name, "sum takes a column id");
            if (name.Text == "round" && args.Count == 2 && args[1] is not Number { Value: >= 0 and <= 28 })
                throw Fail(name, "round digits must be a number from 0 to 28");
            return new Call(name.Text, args);
        }
    }
}

internal enum TokenKind { Number, Word, Symbol, End }

internal readonly record struct Token(TokenKind Kind, string Text, int Position);

/// <summary>Shared tokenizer for rule and lock expressions. Unknown characters are a <see cref="FormatException"/>.</summary>
internal static class Tokenizer
{
    public static List<Token> Read(string text, bool allowBrackets)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var start = i;
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            if (char.IsAsciiDigit(c))
            {
                while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.'))
                    i++;
                var number = text[start..i];
                if (number.Count(ch => ch == '.') > 1 || number.EndsWith('.'))
                    throw new FormatException($"bad number '{number}' at position {start + 1}");
                tokens.Add(new Token(TokenKind.Number, number, start));
            }
            else if (char.IsAsciiLetter(c))
            {
                while (i < text.Length && char.IsAsciiLetterOrDigit(text[i]))
                    i++;
                tokens.Add(new Token(TokenKind.Word, text[start..i], start));
            }
            else if (c is '+' or '-' or '*' or '/' or '(' or ')' or ',' || (allowBrackets && c is '[' or ']' or '='))
            {
                tokens.Add(new Token(TokenKind.Symbol, c.ToString(), start));
                i++;
            }
            else if (allowBrackets && c == '!' && i + 1 < text.Length && text[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.Symbol, "!=", start));
                i += 2;
            }
            else
            {
                throw new FormatException($"unexpected '{c}' at position {start + 1}");
            }
        }
        if (tokens.Count == 0)
            throw new FormatException("empty expression");
        tokens.Add(new Token(TokenKind.End, "", text.Length));
        return tokens;
    }
}
