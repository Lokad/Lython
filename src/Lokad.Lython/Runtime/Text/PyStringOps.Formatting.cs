using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static PyString Format(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords,
        Func<object, string, object>? resolveFieldSuffix,
        Func<object, char?, string?, PyString>? formatValue,
        MemoryGovernor? governor,
        LythonSourceSpan? span,
        bool forbidPositionalFields = false,
        CancellationToken cancellationToken = default)
        => FormatCoreAsync(template, positional, keywords, null,
            resolveFieldSuffix is null ? null : (value, suffix) => new(resolveFieldSuffix(value, suffix)),
            formatValue is null ? null : (value, conversion, spec) => new(formatValue(value, conversion, spec)),
            forbidPositionalFields, governor, span, cancellationToken).GetAwaiter().GetResult();

    public static ValueTask<PyString> FormatMapAsync(
        PyString template,
        Func<string, ValueTask<object>> resolveKeyword,
        Func<object, string, ValueTask<object>> resolveFieldSuffix,
        Func<object, char?, string?, ValueTask<PyString>> formatValue,
        MemoryGovernor governor,
        LythonSourceSpan span,
        CancellationToken cancellationToken)
        => FormatCoreAsync(template, Array.Empty<object>(), null, resolveKeyword, resolveFieldSuffix,
            formatValue, true, governor, span, cancellationToken);

    private static async ValueTask<PyString> FormatCoreAsync(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords,
        Func<string, ValueTask<object>>? resolveKeyword,
        Func<object, string, ValueTask<object>>? resolveFieldSuffix,
        Func<object, char?, string?, ValueTask<PyString>>? formatValue,
        bool forbidPositionalFields,
        MemoryGovernor? governor,
        LythonSourceSpan? span,
        CancellationToken cancellationToken)
    {
        var state = new FormatState(positional, keywords, resolveKeyword, resolveFieldSuffix, formatValue,
            forbidPositionalFields, governor, span, cancellationToken);
        var builder = CreateBuilder(governor, span, checked(template.Utf8Bytes.Length + positional.Count * 8));
        try
        {
            await AppendFormatTemplateAsync(state, template.Utf8Bytes, builder).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    private sealed class FormatState
    {
        public FormatState(
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object>? keywords,
            Func<string, ValueTask<object>>? resolveKeyword,
            Func<object, string, ValueTask<object>>? resolveFieldSuffix,
            Func<object, char?, string?, ValueTask<PyString>>? formatValue,
            bool forbidPositionalFields,
            MemoryGovernor? governor,
            LythonSourceSpan? span,
            CancellationToken cancellationToken)
        {
            Positional = positional;
            Keywords = keywords;
            ResolveKeyword = resolveKeyword;
            ResolveFieldSuffix = resolveFieldSuffix;
            FormatValue = formatValue;
            ForbidPositionalFields = forbidPositionalFields;
            Governor = governor;
            Span = span;
            CancellationToken = cancellationToken;
        }

        public IReadOnlyList<object> Positional { get; }

        public IReadOnlyDictionary<string, object>? Keywords { get; }

        public Func<string, ValueTask<object>>? ResolveKeyword { get; }

        public Func<object, string, ValueTask<object>>? ResolveFieldSuffix { get; }

        public Func<object, char?, string?, ValueTask<PyString>>? FormatValue { get; }

        public MemoryGovernor? Governor { get; }

        public LythonSourceSpan? Span { get; }

        public CancellationToken CancellationToken { get; }

        public bool ForbidPositionalFields { get; }

        public int AutoFieldIndex;

        public bool SawAutoField;

        public bool SawManualField;

        public int Depth;
    }

    private static async ValueTask AppendFormatTemplateAsync(
        FormatState state,
        ReadOnlyMemory<byte> source,
        GovernedByteBuilder builder)
    {
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 2047) == 0) state.CancellationToken.ThrowIfCancellationRequested();
            var b = source.Span[i];
            if (b == (byte)'{')
            {
                if (i + 1 < source.Length && source.Span[i + 1] == (byte)'{')
                {
                    builder.Append((byte)'{');
                    i++;
                    continue;
                }

                var nesting = 0;
                var close = -1;
                for (var j = i + 1; j < source.Length; j++)
                {
                    if ((j & 2047) == 0) state.CancellationToken.ThrowIfCancellationRequested();
                    if (source.Span[j] == (byte)'{')
                    {
                        nesting++;
                    }
                    else if (source.Span[j] == (byte)'}')
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

                await AppendFormatFieldAsync(state, source[(i + 1)..close], builder).ConfigureAwait(false);

                i = close;
                continue;
            }

            if (b == (byte)'}')
            {
                if (i + 1 < source.Length && source.Span[i + 1] == (byte)'}')
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

    private static async ValueTask AppendFormatFieldAsync(
        FormatState state,
        ReadOnlyMemory<byte> fieldBytes,
        GovernedByteBuilder builder)
    {
        // Delimiters are ASCII. Keep structure/specs as borrowed UTF-8; only
        // names, suffixes and the final callback spec need CLR metadata strings.
        state.CancellationToken.ThrowIfCancellationRequested();
        var end = fieldBytes.Length;

        var index = 0;
        while (index < end && !IsFormatFieldDelimiter(fieldBytes.Span[index]))
        {
            if ((index & 2047) == 0) state.CancellationToken.ThrowIfCancellationRequested();
            if (fieldBytes.Span[index] == (byte)'{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            index++;
        }

        var rootEnd = index;
        while (index < end && (fieldBytes.Span[index] == (byte)'.' || fieldBytes.Span[index] == (byte)'['))
        {
            index = fieldBytes.Span[index] == (byte)'.'
                ? SkipFormatAttribute(fieldBytes.Span, index, state.CancellationToken)
                : SkipFormatBracket(fieldBytes.Span, index, state.CancellationToken);
        }

        var fieldNameEnd = index;
        var trailing = index < end && fieldBytes.Span[index] != (byte)'!' && fieldBytes.Span[index] != (byte)':';

        // Eager structural pass past the field-name walk: bracket spans are
        // skipped (a missing ']' fails before argument resolution), a stray
        // '{' fails, the first '!' shape is validated, and the first ':'
        // starts the raw spec.
        Rune? conversion = null;
        var specStart = -1;
        var sawConversion = false;
        var scan = index;
        while (scan < end)
        {
            if ((scan & 2047) == 0) state.CancellationToken.ThrowIfCancellationRequested();
            var current = fieldBytes.Span[scan];
            if (current == (byte)'.')
            {
                scan = SkipFormatAttribute(fieldBytes.Span, scan, state.CancellationToken);
                continue;
            }

            if (current == (byte)'[')
            {
                scan = SkipFormatBracket(fieldBytes.Span, scan, state.CancellationToken);
                continue;
            }

            if (current == (byte)'{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            if (current == (byte)'!' && !sawConversion)
            {
                sawConversion = true;
                if (scan + 1 >= end)
                {
                    throw new InvalidOperationException("unmatched '{' in format spec");
                }

                Rune.DecodeFromUtf8(fieldBytes.Span[(scan + 1)..], out var convertedRune, out var conversionBytes);
                conversion = convertedRune.Value == 0 ? null : convertedRune;
                var conversionEnd = scan + 1 + conversionBytes;
                if (conversionEnd < end && fieldBytes.Span[conversionEnd] != (byte)':' && fieldBytes.Span[conversionEnd] != (byte)'}')
                {
                    throw new InvalidOperationException("expected ':' after conversion specifier");
                }

                scan = conversionEnd;
                continue;
            }

            if (current == (byte)':')
            {
                specStart = scan + 1;
                break;
            }

            scan++;
        }

        // Metadata remains private until callback completion. Include room for
        // resolver error strings; expanded specs grow this same temporary lease.
        using var metadata = fieldBytes.IsEmpty ? null : state.Governor?.ReserveTemporary(
            checked(128L + 4L * fieldBytes.Length), state.Span);
        var root = Encoding.UTF8.GetString(fieldBytes.Span[..rootEnd]);
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
        else if (TryParseFormatIndex(root, out var slot, state.CancellationToken))
        {
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
            if (state.ResolveKeyword is not null)
            {
                selected = await state.ResolveKeyword(root).ConfigureAwait(false);
            }
            else if (state.Keywords is not null && state.Keywords.TryGetValue(root, out var keywordValue)) selected = keywordValue;
            else throw new KeyNotFoundException(root);
        }
        state.CancellationToken.ThrowIfCancellationRequested();

        // The suffix carries one trailing character past the field-name walk
        // when the walk stopped on neither '!' nor ':' nor the end, so the
        // resolver reports the '.'-or-'[' failure after item lookups run.
        var suffixEnd = fieldNameEnd;
        if (trailing)
        {
            Rune.DecodeFromUtf8(fieldBytes.Span[fieldNameEnd..], out _, out var width);
            suffixEnd += width;
        }
        var suffix = Encoding.UTF8.GetString(fieldBytes.Span[rootEnd..suffixEnd]);
        if (suffix.Length != 0)
        {
            if (state.ResolveFieldSuffix is null)
            {
                throw new KeyNotFoundException(Encoding.UTF8.GetString(fieldBytes.Span[..fieldNameEnd]));
            }

            selected = await state.ResolveFieldSuffix(selected, suffix).ConfigureAwait(false);
        }
        state.CancellationToken.ThrowIfCancellationRequested();

        if (conversion is { Value: not ('s' or 'r' or 'a') })
        {
            var scalar = conversion.Value.Value;
            var display = scalar is > 32 and < 127 ? conversion.ToString()
                : "\\x" + scalar.ToString("x", CultureInfo.InvariantCulture);
            throw new InvalidOperationException($"Unknown conversion specifier {display}");
        }

        string? spec = null;
        if (specStart >= 0 && specStart < end)
        {
            var rawSpec = fieldBytes[specStart..];
            if (rawSpec.Span.Contains((byte)'{') || rawSpec.Span.Contains((byte)'}'))
            {
                if (state.Depth >= 1)
                {
                    throw new InvalidOperationException("Max string recursion exceeded");
                }

                var nested = CreateBuilder(state.Governor, state.Span, rawSpec.Length);
                state.Depth++;
                try
                {
                    await AppendFormatTemplateAsync(state, rawSpec, nested).ConfigureAwait(false);
                    metadata?.Grow(checked(32L + 2L * nested.Length), state.Span);
                    spec = Encoding.UTF8.GetString(nested.WrittenSpan);
                }
                finally { nested.Release(); state.Depth--; }
            }
            else
            {
                spec = Encoding.UTF8.GetString(rawSpec.Span);
            }
        }

        if (conversion is null && spec is null && state.FormatValue is null)
        {
            var formatted = selected switch
            {
                PyNone => NoneLiteral,
                PyString text => text,
                _ => FromString(selected.ToString() ?? "None", state.Governor, state.Span)
            };

            builder.Append(formatted.Utf8Bytes.Span);
            return;
        }

        if (state.FormatValue is null)
        {
            throw new InvalidOperationException("Format specifiers are not supported.");
        }

        var result = await state.FormatValue(selected, conversion is { } rune ? (char)rune.Value : null, spec).ConfigureAwait(false);
        state.CancellationToken.ThrowIfCancellationRequested();
        builder.Append(result);
    }

    private static bool IsFormatFieldDelimiter(byte value)
        => value is (byte)'.' or (byte)'[' or (byte)'!' or (byte)':';

    private static int SkipFormatAttribute(ReadOnlySpan<byte> field, int index, CancellationToken token)
    {
        var scan = index + 1;
        while (scan < field.Length && !IsFormatFieldDelimiter(field[scan]))
        {
            if ((scan & 2047) == 0) token.ThrowIfCancellationRequested();
            if (field[scan] == (byte)'{')
                throw new InvalidOperationException("unexpected '{' in field name");
            scan++;
        }
        return scan;
    }

    private static int SkipFormatBracket(ReadOnlySpan<byte> field, int index, CancellationToken token)
    {
        var scan = index + 1;
        while (scan < field.Length && field[scan] != (byte)']')
        {
            if ((scan & 2047) == 0) token.ThrowIfCancellationRequested();
            scan++;
        }
        if (scan >= field.Length)
            throw new InvalidOperationException("expected '}' before end of string");
        return scan + 1;
    }

    internal static bool TryParseFormatIndex(string text, out long value, CancellationToken cancellationToken = default)
    {
        value = 0;
        if (text.Length == 0) return false;
        var offset = 0;
        foreach (var digit in text.EnumerateRunes())
        {
            if ((offset++ & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (Rune.GetUnicodeCategory(digit) != UnicodeCategory.DecimalDigitNumber) return false;
            // CPython checks overflow while walking, even before a later nondigit.
            var number = (int)Rune.GetNumericValue(digit);
            if (value > (long.MaxValue - number) / 10)
                throw new InvalidOperationException("Too many decimal digits in format string");
            value = value * 10 + number;
        }
        return true;
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
