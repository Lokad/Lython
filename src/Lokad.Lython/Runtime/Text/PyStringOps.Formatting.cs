using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static PyString Format(PyString template, IReadOnlyList<object> positional)
        => Format(template, positional, null, null, null);

    public static PyString Format(PyString template, IReadOnlyList<object> positional, IReadOnlyDictionary<string, object>? keywords)
        => Format(template, positional, keywords, null, null);

    public static PyString Format(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords,
        Func<object, string, object>? resolveFieldSuffix,
        Func<object, char?, string?, PyString>? formatValue,
        bool forbidPositionalFields = false)
    {
        var state = new FormatState(positional, keywords, resolveFieldSuffix, formatValue, forbidPositionalFields);
        var source = template.Utf8Bytes.Span;
        var builder = CreateBuilder(template, source.Length + positional.Count * 8);
        AppendFormatTemplate(state, template, source, builder);
        return builder.ToPyStringAndRelease();
    }

    private sealed class FormatState
    {
        public FormatState(
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object>? keywords,
            Func<object, string, object>? resolveFieldSuffix,
            Func<object, char?, string?, PyString>? formatValue,
            bool forbidPositionalFields = false)
        {
            Positional = positional;
            Keywords = keywords;
            ResolveFieldSuffix = resolveFieldSuffix;
            FormatValue = formatValue;
            ForbidPositionalFields = forbidPositionalFields;
        }

        public IReadOnlyList<object> Positional { get; }

        public IReadOnlyDictionary<string, object>? Keywords { get; }

        public Func<object, string, object>? ResolveFieldSuffix { get; }

        public Func<object, char?, string?, PyString>? FormatValue { get; }

        public bool ForbidPositionalFields { get; }

        public int AutoFieldIndex;

        public bool SawAutoField;

        public bool SawManualField;

        public int Depth;
    }

    private static void AppendFormatTemplate(
        FormatState state,
        PyString template,
        ReadOnlySpan<byte> source,
        GovernedByteBuilder builder)
    {
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

                var nesting = 0;
                var close = -1;
                for (var j = i + 1; j < source.Length; j++)
                {
                    if (source[j] == (byte)'{')
                    {
                        nesting++;
                    }
                    else if (source[j] == (byte)'}')
                    {
                        if (nesting == 0)
                        {
                            close = j;
                            break;
                        }

                        nesting--;
                    }
                }

                if (close < 0)
                {
                    throw new InvalidOperationException("Single '{' encountered in format string");
                }

                AppendFormatField(state, template, source[(i + 1)..close], builder);

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

                throw new InvalidOperationException("Single '}' encountered in format string");
            }

            builder.Append(b);
        }
    }

    private static void AppendFormatField(
        FormatState state,
        PyString template,
        ReadOnlySpan<byte> fieldBytes,
        GovernedByteBuilder builder)
    {
        // The field name is ASCII-delimited, but names and keys may carry
        // multi-byte UTF-8, so the field decodes once for splitting while the
        // template itself keeps streaming as bytes.
        var fieldText = Encoding.UTF8.GetString(fieldBytes);
        var end = fieldText.Length;

        var index = 0;
        while (index < end && !IsFormatFieldDelimiter(fieldText[index]))
        {
            if (fieldText[index] == '{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            index++;
        }

        var rootEnd = index;
        while (index < end && (fieldText[index] == '.' || fieldText[index] == '['))
        {
            index = SkipFormatFieldItem(fieldText, index);
        }

        var fieldNameEnd = index;
        var trailing = index < end && fieldText[index] != '!' && fieldText[index] != ':';

        // Eager structural pass past the field-name walk: bracket spans are
        // skipped (a missing ']' fails before argument resolution), a stray
        // '{' fails, the first '!' shape is validated, and the first ':'
        // starts the raw spec.
        char? conversion = null;
        var specStart = -1;
        var sawConversion = false;
        var scan = index;
        while (scan < end)
        {
            var current = fieldText[scan];
            if (current == '.')
            {
                scan = SkipFormatAttribute(fieldText, scan);
                continue;
            }

            if (current == '[')
            {
                scan = SkipFormatBracket(fieldText, scan);
                continue;
            }

            if (current == '{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            if (current == '!' && !sawConversion)
            {
                sawConversion = true;
                if (scan + 1 >= end)
                {
                    throw new InvalidOperationException("unmatched '{' in format spec");
                }

                conversion = fieldText[scan + 1];
                if (scan + 2 < end && fieldText[scan + 2] != ':' && fieldText[scan + 2] != '}')
                {
                    throw new InvalidOperationException("expected ':' after conversion specifier");
                }

                scan += 2;
                continue;
            }

            if (current == ':')
            {
                specStart = scan + 1;
                break;
            }

            scan++;
        }

        var root = fieldText[..rootEnd];
        object selected;
        if (root.Length == 0)
        {
            if (state.ForbidPositionalFields)
            {
                throw new InvalidOperationException("Format string contains positional fields");
            }

            if (state.SawManualField)
            {
                throw new InvalidOperationException("cannot switch from manual field specification to automatic field numbering");
            }

            state.SawAutoField = true;
            if (state.AutoFieldIndex >= state.Positional.Count)
            {
                throw new IndexOutOfRangeException($"Replacement index {state.AutoFieldIndex} out of range for positional args tuple");
            }

            selected = state.Positional[state.AutoFieldIndex++];
        }
        else if (IsAsciiDigits(root))
        {
            long slot = 0;
            foreach (var digit in root)
            {
                var value = digit - '0';
                if (slot > (long.MaxValue - value) / 10)
                {
                    throw new InvalidOperationException("Too many decimal digits in format string");
                }

                slot = (slot * 10) + value;
            }

            if (state.ForbidPositionalFields)
            {
                throw new InvalidOperationException("Format string contains positional fields");
            }

            if (state.SawAutoField)
            {
                throw new InvalidOperationException("cannot switch from automatic field numbering to manual field specification");
            }

            state.SawManualField = true;
            if (slot >= state.Positional.Count)
            {
                throw new IndexOutOfRangeException($"Replacement index {slot} out of range for positional args tuple");
            }

            selected = state.Positional[(int)slot];
        }
        else
        {
            if (state.Keywords is null || !state.Keywords.TryGetValue(root, out var keywordValue))
            {
                throw new KeyNotFoundException(root);
            }

            selected = keywordValue;
        }

        // The suffix carries one trailing character past the field-name walk
        // when the walk stopped on neither '!' nor ':' nor the end, so the
        // resolver reports the '.'-or-'[' failure after item lookups run.
        var suffixEnd = trailing ? fieldNameEnd + 1 : fieldNameEnd;
        var suffix = fieldText[rootEnd..suffixEnd];
        if (suffix.Length != 0)
        {
            if (state.ResolveFieldSuffix is null)
            {
                throw new KeyNotFoundException(fieldText[..fieldNameEnd]);
            }

            selected = state.ResolveFieldSuffix(selected, suffix);
        }

        if (conversion is not null && conversion is not ('s' or 'r' or 'a'))
        {
            throw new InvalidOperationException($"Unknown conversion specifier {conversion}");
        }

        string? spec = null;
        if (specStart >= 0 && specStart < end)
        {
            var rawSpec = fieldText[specStart..];
            if (rawSpec.Contains('{') || rawSpec.Contains('}'))
            {
                if (state.Depth >= 1)
                {
                    throw new InvalidOperationException("Max string recursion exceeded");
                }

                state.Depth++;
                var nested = CreateBuilder(template, rawSpec.Length);
                AppendFormatTemplate(state, template, Encoding.UTF8.GetBytes(rawSpec), nested);
                state.Depth--;
                spec = nested.ToPyStringAndRelease().AsString();
            }
            else
            {
                spec = rawSpec;
            }
        }

        if (conversion is null && spec is null && state.FormatValue is null)
        {
            var formatted = selected switch
            {
                PyNone => NoneLiteral,
                PyString text => text,
                _ => FromString(selected.ToString() ?? "None", template.OwnerMemoryGovernor, template.AllocationSpan)
            };

            builder.Append(formatted.Utf8Bytes.Span);
            return;
        }

        if (state.FormatValue is null)
        {
            throw new InvalidOperationException("Format specifiers are not supported.");
        }

        builder.Append(state.FormatValue(selected, conversion, spec).Utf8Bytes.Span);
    }

    private static bool IsFormatFieldDelimiter(char value)
        => value is '.' or '[' or '!' or ':';

    private static int SkipFormatFieldItem(string fieldText, int index)
        => fieldText[index] == '.'
            ? SkipFormatAttribute(fieldText, index)
            : SkipFormatBracket(fieldText, index);

    private static int SkipFormatAttribute(string fieldText, int index)
    {
        var end = fieldText.Length;
        var scan = index + 1;
        while (scan < end && !IsFormatFieldDelimiter(fieldText[scan]))
        {
            if (fieldText[scan] == '{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            scan++;
        }

        return scan;
    }

    private static int SkipFormatBracket(string fieldText, int index)
    {
        var end = fieldText.Length;
        var scan = index + 1;
        while (scan < end && fieldText[scan] != ']')
        {
            scan++;
        }

        if (scan >= end)
        {
            throw new InvalidOperationException("expected '}' before end of string");
        }

        return scan + 1;
    }

    private static bool IsAsciiDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var digit in text)
        {
            if (digit < '0' || digit > '9')
            {
                return false;
            }
        }

        return true;
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
