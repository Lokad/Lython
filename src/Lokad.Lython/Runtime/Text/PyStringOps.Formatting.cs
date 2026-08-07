using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static PyString Format(PyString template, IReadOnlyList<object> positional)
        => Format(template, positional, null, null);

    public static PyString Format(PyString template, IReadOnlyList<object> positional, IReadOnlyDictionary<string, object>? keywords)
        => Format(template, positional, keywords, null);

    public static PyString Format(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords,
        Func<string, object>? resolveCompositeField)
    {
        var source = template.Utf8Bytes.Span;
        var builder = CreateBuilder(template, source.Length + positional.Count * 8);
        var autoFieldIndex = 0;
        var sawAutoField = false;
        var sawManualField = false;
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            if (b == (byte)'{')
            {
                if (i + 1 < source.Length && source[i + 1] == (byte)'{')
                {
                    builder.Append((byte)'{');
                    i++;
                    continue;
                }

                var close = source[i..].IndexOf((byte)'}');
                if (close < 0)
                {
                    throw new InvalidOperationException("Expected '}' in format string.");
                }

                close += i;
                var slotBytes = source[(i + 1)..close];
                if (slotBytes.IndexOf((byte)':') >= 0 ||
                    slotBytes.IndexOf((byte)'!') >= 0)
                {
                    throw new InvalidOperationException("Format specifiers are not supported.");
                }

                var slotText = Encoding.ASCII.GetString(slotBytes);
                object selected;
                if (slotText.Length == 0)
                {
                    if (sawManualField)
                    {
                        throw new InvalidOperationException("cannot switch from manual field specification to automatic field numbering");
                    }

                    sawAutoField = true;
                    if (autoFieldIndex >= positional.Count)
                    {
                        throw new IndexOutOfRangeException($"Replacement index {autoFieldIndex} out of range for positional args tuple");
                    }

                    selected = positional[autoFieldIndex++];
                }
                else if (int.TryParse(slotText, out var slot))
                {
                    if (sawAutoField)
                    {
                        throw new InvalidOperationException("cannot switch from automatic field numbering to manual field specification");
                    }

                    sawManualField = true;
                    if (slot < 0 || slot >= positional.Count)
                    {
                        throw new InvalidOperationException("Invalid positional format field.");
                    }

                    selected = positional[slot];
                }
                else
                {
                    if (slotText.IndexOfAny(['.', '[']) >= 0)
                    {
                        if (resolveCompositeField is null)
                        {
                            throw new KeyNotFoundException(slotText);
                        }

                        selected = resolveCompositeField(slotText);
                    }
                    else
                    {
                        if (keywords is null || !keywords.TryGetValue(slotText, out var keywordValue))
                        {
                            throw new KeyNotFoundException(slotText);
                        }

                        selected = keywordValue;
                    }
                }

                var formatted = selected switch
                {
                    PyNone => NoneLiteral,
                    PyString text => text,
                    _ => FromString(selected.ToString() ?? "None", template.OwnerMemoryGovernor, template.AllocationSpan)
                };

                builder.Append(formatted.Utf8Bytes.Span);

                i = close;
                continue;
            }

            if (b == (byte)'}')
            {
                if (i + 1 < source.Length && source[i + 1] == (byte)'}')
                {
                    builder.Append((byte)'}');
                    i++;
                    continue;
                }

                throw new InvalidOperationException("Unexpected '}' in format string.");
            }

            builder.Append(b);
        }

        return builder.ToPyStringAndRelease();
    }

    public static IReadOnlyDictionary<string, object> ExtractStringKeyDictionary(PyDict mapping)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in mapping)
        {
            if (!TryAsString(pair.Key, out var key))
            {
                throw new InvalidOperationException("format mapping keys must be strings.");
            }

            result[key.AsString()] = pair.Value;
        }

        return result;
    }

    public static PyString EscapeRegex(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length * 2);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Utf8Bytes.Length == 1 && ".^$*+?{}[]\\|()&~#- \t\n\r\v\f".Contains((char)rune.Utf8Bytes.Span[0], StringComparison.Ordinal))
            {
                builder.Append((byte)'\\');
            }

            builder.Append(rune);
        }

        return builder.ToPyStringAndRelease();
    }

}
