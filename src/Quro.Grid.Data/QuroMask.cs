using System.Text;

namespace Quro.Grid.Data;

/// <summary>
/// Input mask (Control Spec 11, Addendum A-69): <c>0</c> digit, <c>A</c> letter of any language, <c>*</c> letter or
/// digit, <c>\</c> makes the next character fixed, every other character is fixed. The column stores only the typed
/// characters; the fixed characters are added for display. Shared by the grid Mask cell and QuroMaskBox (A-56);
/// quro-grid.js and quro-ui.js apply the same rules while typing.
/// </summary>
public sealed class QuroMask
{
    private enum Kind { Digit, Letter, LetterOrDigit, Fixed }

    private readonly record struct Slot(Kind Kind, char Fixed = default);

    private readonly Slot[] _slots;

    private QuroMask(Slot[] slots) => _slots = slots;

    /// <summary>Number of characters of a fully typed value with its fixed characters.</summary>
    public int Length => _slots.Length;

    /// <summary>Reads a mask. Throws <see cref="ArgumentException"/> when it has no input position.</summary>
    public static QuroMask Parse(string? mask, string? paramName = null)
    {
        var slots = new List<Slot>();
        var text = mask ?? "";
        for (var i = 0; i < text.Length; i++)
        {
            slots.Add(text[i] switch
            {
                '\\' when i + 1 < text.Length => new Slot(Kind.Fixed, text[++i]),
                '0' => new Slot(Kind.Digit),
                'A' => new Slot(Kind.Letter),
                '*' => new Slot(Kind.LetterOrDigit),
                var c => new Slot(Kind.Fixed, c),
            });
        }
        if (!slots.Any(s => s.Kind != Kind.Fixed))
            throw new ArgumentException($"마스크 '{text}'에 입력 자리(0·A·*)가 없습니다.", paramName);
        return new QuroMask([.. slots]);
    }

    /// <summary>
    /// Typed text (with or without the fixed characters) → the typed characters. False when a character doesn't fit
    /// its position, or the text is shorter or longer than the mask.
    /// </summary>
    public bool TryRead(string text, out string value)
    {
        var typed = new StringBuilder();
        var i = 0;
        foreach (var c in text)
        {
            while (i < _slots.Length && _slots[i].Kind == Kind.Fixed && _slots[i].Fixed != c)
                i++;   // fixed character left out
            if (i == _slots.Length)
            {
                value = "";
                return false;
            }
            if (_slots[i].Kind != Kind.Fixed)
            {
                if (!Fits(_slots[i].Kind, c))
                {
                    value = "";
                    return false;
                }
                typed.Append(c);
            }
            i++;
        }
        value = typed.ToString();
        return typed.Length == _slots.Count(s => s.Kind != Kind.Fixed);
    }

    /// <summary>Stored typed characters → display text. A value that doesn't fit the mask is returned as it is.</summary>
    public string Format(string value)
    {
        var text = new StringBuilder();
        var next = 0;
        foreach (var slot in _slots)
        {
            if (next == value.Length)
                break;
            if (slot.Kind == Kind.Fixed)
            {
                text.Append(slot.Fixed);
                continue;
            }
            if (!Fits(slot.Kind, value[next]))
                return value;
            text.Append(value[next++]);
        }
        return next == value.Length ? text.ToString() : value;
    }

    private static bool Fits(Kind kind, char c) => kind switch
    {
        Kind.Digit => char.IsAsciiDigit(c),
        Kind.Letter => char.IsLetter(c),
        _ => char.IsAsciiDigit(c) || char.IsLetter(c),
    };
}
