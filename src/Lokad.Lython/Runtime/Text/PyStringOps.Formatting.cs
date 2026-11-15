using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static async ValueTask<PyString> FormatAsync(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords,
        Func<object, string, ValueTask<object>>? resolveFieldSuffix,
        Func<GovernedByteBuilder, object, char?, string?, LythonRuntime.InterpolatedFormatSpecifier?, ValueTask>? appendValue,
        MemoryGovernor? governor,
        LythonSourceSpan? span,
        bool forbidPositionalFields = false,
        CancellationToken cancellationToken = default,
        Func<string, ValueTask<object>>? resolveKeyword = null,
        FormatTemplate? prepared = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var layoutStorage = prepared is null ? governor?.ReserveTemporary(EstimateFormatTemplateBytes(template, cancellationToken), span) : null;
        prepared ??= ParseFormatTemplate(template.Utf8Bytes, 0, null, span, cancellationToken);
        var state = new FormatState(positional, keywords, resolveKeyword, resolveFieldSuffix, appendValue,
            forbidPositionalFields, governor, span, cancellationToken, template.ByteLength);
        var builder = CreateBuilder(governor, span, checked(template.ByteLength + positional.Count * 8));
        try
        {
            await AppendFormatTemplateAsync(state, prepared, builder).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return builder.ToPyString();
        }
        finally
        {
            try { builder.Release(); }
            finally { state.Dispose(); }
        }
    }

    private sealed class FormatState : IDisposable
    {
        public FormatState(
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object>? keywords,
            Func<string, ValueTask<object>>? resolveKeyword,
            Func<object, string, ValueTask<object>>? resolveFieldSuffix,
            Func<GovernedByteBuilder, object, char?, string?, LythonRuntime.InterpolatedFormatSpecifier?, ValueTask>? appendValue,
            bool forbidPositionalFields,
            MemoryGovernor? governor,
            LythonSourceSpan? span,
            CancellationToken cancellationToken,
            int templateBytes)
        {
            Positional = positional;
            Keywords = keywords;
            ResolveKeyword = resolveKeyword;
            ResolveFieldSuffix = resolveFieldSuffix;
            AppendValue = appendValue;
            ForbidPositionalFields = forbidPositionalFields;
            Governor = governor;
            Span = span;
            CancellationToken = cancellationToken;
            // Expanded nested specs are private to this operation. Layout
            // metadata is admitted separately and may survive on a bound method.
            _metadataBaseBytes = checked(512L + 8L * templateBytes);
        }

        public IReadOnlyList<object> Positional { get; }

        public IReadOnlyDictionary<string, object>? Keywords { get; }

        public Func<string, ValueTask<object>>? ResolveKeyword { get; }

        public Func<object, string, ValueTask<object>>? ResolveFieldSuffix { get; }

        public Func<GovernedByteBuilder, object, char?, string?, LythonRuntime.InterpolatedFormatSpecifier?, ValueTask>? AppendValue { get; }

        private readonly long _metadataBaseBytes;
        private MemoryGovernor.TemporaryMemoryReservation? _metadata;
        private long _metadataBytes;

        public void AdmitMetadata(long additionalBytes = 0)
        {
            if (Governor is null) return;
            var target = checked(_metadataBaseBytes + additionalBytes);
            if (_metadata is null)
            {
                _metadata = Governor.ReserveTemporary(target, Span);
                _metadataBytes = target;
            }
            else if (target > _metadataBytes)
            {
                _metadata.Grow(target - _metadataBytes, Span);
                _metadataBytes = target;
            }
        }

        public void Dispose() => _metadata?.Dispose();

        public MemoryGovernor? Governor { get; }

        public LythonSourceSpan? Span { get; }

        public CancellationToken CancellationToken { get; }

        public bool ForbidPositionalFields { get; }

        public int AutoFieldIndex;

        public bool SawAutoField;

        public bool SawManualField;

    }

    private static async ValueTask AppendFormatTemplateAsync(
        FormatState state, FormatTemplate template, GovernedByteBuilder builder)
    {
        foreach (var part in template.Parts)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            if (part.Error is { } error) throw new InvalidOperationException(error);
            if (part.Field is { } field)
                await AppendFormatFieldAsync(state, field, builder).ConfigureAwait(false);
            else builder.Append(part.Literal.Span);
        }
    }

    private static async ValueTask AppendFormatFieldAsync(
        FormatState state, FormatField field, GovernedByteBuilder builder)
    {
        state.CancellationToken.ThrowIfCancellationRequested();
        var root = field.Root;
        var conversion = field.Conversion;
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

        var suffix = field.Suffix;
        if (suffix.Length != 0)
        {
            if (state.ResolveFieldSuffix is null)
            {
                throw new KeyNotFoundException(field.Name);
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

        var spec = field.Spec;
        if (field.RecursiveSpec) throw new InvalidOperationException("Max string recursion exceeded");
        if (field.NestedSpec is { } nestedTemplate)
        {
            var nested = CreateBuilder(state.Governor, state.Span);
            try
            {
                await AppendFormatTemplateAsync(state, nestedTemplate, nested).ConfigureAwait(false);
                state.AdmitMetadata(checked(32L + 2L * nested.Length));
                spec = Encoding.UTF8.GetString(nested.WrittenSpan);
            }
            finally { nested.Release(); }
        }

        if (conversion is null && spec is null && state.AppendValue is null)
        {
            var formatted = selected switch
            {
                PyNone => NoneLiteral,
                PyString text => text,
                _ => FromString(selected.ToString() ?? "None", state.Governor, state.Span)
            };

            builder.Append(formatted.Utf8Span);
            return;
        }

        if (state.AppendValue is null)
        {
            throw new InvalidOperationException("Format specifiers are not supported.");
        }

        await state.AppendValue(builder, selected, conversion is { } rune ? (char)rune.Value : null, spec, field.PreparedSpec).ConfigureAwait(false);
        state.CancellationToken.ThrowIfCancellationRequested();
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
        var builder = CreateBuilder(value, value.ByteLength * 2);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.ByteLength == 1 && ".^$*+?{}[]\\|()&~#- \t\n\r\v\f".Contains((char)rune.Utf8Span[0], StringComparison.Ordinal))
            {
                builder.Append((byte)'\\');
            }

            builder.Append(rune);
        }

        return builder.ToPyStringAndRelease();
    }

}
