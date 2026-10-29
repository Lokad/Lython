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
        => FormatCoreAsync(template, positional, keywords, null,
            resolveFieldSuffix is null ? null : (value, suffix) => new(resolveFieldSuffix(value, suffix)),
            formatValue is null ? null : (value, conversion, spec) => new(formatValue(value, conversion, spec)),
            forbidPositionalFields, template.OwnerMemoryGovernor, template.AllocationSpan, default).GetAwaiter().GetResult();

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
            await AppendFormatTemplateAsync(state, template, template.Utf8Bytes, builder).ConfigureAwait(false);
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
        PyString template,
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

                await AppendFormatFieldAsync(state, template, source[(i + 1)..close], builder).ConfigureAwait(false);

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
        PyString template,
        ReadOnlyMemory<byte> fieldBytes,
        GovernedByteBuilder builder)
    {
        // The field name is ASCII-delimited, but names and keys may carry
        // multi-byte UTF-8, so the field decodes once for splitting while the
        // template itself keeps streaming as bytes.
        state.CancellationToken.ThrowIfCancellationRequested();
        var fieldText = Encoding.UTF8.GetString(fieldBytes.Span);
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

            selected = await state.ResolveFieldSuffix(selected, suffix).ConfigureAwait(false);
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
                using var encodingStorage = state.Governor?.ReserveTemporary(checked(32L + 3L * rawSpec.Length), state.Span);
                var nested = CreateBuilder(state.Governor, state.Span, rawSpec.Length);
                try
                {
                    await AppendFormatTemplateAsync(state, template, Encoding.UTF8.GetBytes(rawSpec), nested).ConfigureAwait(false);
                    spec = nested.ToPyString().AsString();
                }
                finally { nested.Release(); state.Depth--; }
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
                _ => FromString(selected.ToString() ?? "None", state.Governor, state.Span)
            };

            builder.Append(formatted.Utf8Bytes.Span);
            return;
        }

        if (state.FormatValue is null)
        {
            throw new InvalidOperationException("Format specifiers are not supported.");
        }

        var result = await state.FormatValue(selected, conversion, spec).ConfigureAwait(false);
        state.CancellationToken.ThrowIfCancellationRequested();
        builder.Append(result);
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
