using System.Text;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // A bound method keeps this immutable layout in its execution owner, never
    // on the shared literal. It retains no invocation values, callbacks or tokens.
    internal sealed record FormatTemplate(FormatPart[] Parts, MemoryGovernor? Owner, bool IsPositionalText);

    internal readonly record struct FormatPart(ReadOnlyMemory<byte> Literal, FormatField? Field = null, string? Error = null);

    internal enum FormatAccessKind { Member, StringIndex, IntegerIndex, Error }

    internal readonly record struct FormatAccess(FormatAccessKind Kind, object? Value, long Index = 0);

    internal sealed record FormatField(long Position, string? Keyword, FormatAccess[] Accesses, Rune? Conversion,
        PyString? Spec, FormatTemplate? NestedSpec, bool RecursiveSpec,
        LythonRuntime.InterpolatedFormatSpecifier? PreparedSpec);

    internal static FormatTemplate PrepareFormatTemplate(PyString template, MemoryGovernor governor,
        LythonSourceSpan? span, CancellationToken token)
    {
        var bytes = EstimateFormatTemplateBytes(template, token);
        governor.Reserve(bytes, span);
        try
        {
            var nextPosition = 0;
            var result = ParseFormatTemplate(template.Utf8Bytes, 0, governor, span, token, ref nextPosition);
            governor.Commit(bytes);
            return result;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static long EstimateFormatTemplateBytes(PyString template, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long braces = 0;
        long accessMarkers = 0;
        var nesting = 0;
        var source = template.Utf8Span;
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested();
            switch (source[i])
            {
                case (byte)'{': braces++; nesting++; break;
                case (byte)'}': braces++; if (nesting > 0) nesting--; break;
                case (byte)'.' or (byte)'[' when nesting > 0: accessMarkers++; break;
            }
        }
        // Two supported levels, list growth/replacement overlap, part/field
        // records, UTF-16 names and UTF-8 specs and native spec descriptors. Potential
        // access markers cover the access-array overlap and a private UTF-8 key.
        return checked(512L + 8L * source.Length + 384L * braces + 224L * accessMarkers);
    }

    private static FormatTemplate ParseFormatTemplate(ReadOnlyMemory<byte> source, int depth,
        MemoryGovernor? owner, LythonSourceSpan? span, CancellationToken token, ref int nextPosition)
    {
        var parts = new List<FormatPart>();
        var isPositionalText = true;
        var literal = 0;
        var i = 0;
        for (; i < source.Length; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested();
            var b = source.Span[i];
            if (b is not ((byte)'{' or (byte)'}')) continue;
            if (i != literal) parts.Add(new(source[literal..i]));
            if (i + 1 < source.Length && source.Span[i + 1] == b)
            {
                parts.Add(new(source.Slice(i, 1)));
                i++;
                literal = i + 1;
                continue;
            }
            if (b == (byte)'}')
            {
                parts.Add(new(default, Error: "Single '}' encountered in format string"));
                return new(parts.ToArray(), owner, false);
            }
            var nesting = 0;
            var close = -1;
            for (var j = i + 1; j < source.Length; j++)
            {
                if ((j & 2047) == 0) token.ThrowIfCancellationRequested();
                if (source.Span[j] == (byte)'{') nesting++;
                else if (source.Span[j] == (byte)'}')
                {
                    if (nesting == 0) { close = j; break; }
                    nesting--;
                }
            }
            if (close < 0)
            {
                parts.Add(new(default, Error: "Single '{' encountered in format string"));
                return new(parts.ToArray(), owner, false);
            }
            try
            {
                var field = ParseFormatField(source[(i + 1)..close], depth, owner, span, token, ref nextPosition);
                parts.Add(new(default, field));
                isPositionalText = isPositionalText && field is
                    { Keyword: null, Accesses.Length: 0, Conversion: null, NestedSpec: null, RecursiveSpec: false }
                    && field.Spec is null;
            }
            // Store grammar failures at their original execution position.
            // Earlier fields still perform their lookups, hooks and side effects.
            catch (InvalidOperationException error)
            {
                parts.Add(new(default, Error: error.Message));
                return new(parts.ToArray(), owner, false);
            }
            i = close;
            literal = i + 1;
        }
        if (literal < source.Length) parts.Add(new(source[literal..]));
        return new(parts.ToArray(), owner, isPositionalText);
    }
    private static FormatField ParseFormatField(ReadOnlyMemory<byte> fieldBytes, int depth,
        MemoryGovernor? owner, LythonSourceSpan? span, CancellationToken token, ref int nextPosition)
    {
        var end = fieldBytes.Length;

        var index = 0;
        while (index < end && !IsFormatFieldDelimiter(fieldBytes.Span[index]))
        {
            if ((index & 2047) == 0) token.ThrowIfCancellationRequested();
            if (fieldBytes.Span[index] == (byte)'{')
            {
                throw new InvalidOperationException("unexpected '{' in field name");
            }

            index++;
        }

        var rootEnd = index;
        List<FormatAccess>? accesses = null;
        var badAccess = false;
        while (index < end && (fieldBytes.Span[index] == (byte)'.' || fieldBytes.Span[index] == (byte)'['))
        {
            var start = index + 1;
            var member = fieldBytes.Span[index] == (byte)'.';
            index = member
                ? SkipFormatAttribute(fieldBytes.Span, index, token)
                : SkipFormatBracket(fieldBytes.Span, index, token);
            // Keep checking the enclosing grammar after an error that belongs
            // to a particular access. The error itself is raised after lookup.
            if (badAccess) continue;
            var access = PrepareFormatAccess(fieldBytes.Span[start..(member ? index : index - 1)], member, span, token);
            (accesses ??= []).Add(access);
            badAccess = access.Kind == FormatAccessKind.Error;
        }

        if (!badAccess && index < end && fieldBytes.Span[index] is not ((byte)'!' or (byte)':'))
            (accesses ??= []).Add(new(FormatAccessKind.Error, "Only '.' or '[' may follow ']' in format field specifier"));

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
            if ((scan & 2047) == 0) token.ThrowIfCancellationRequested();
            var current = fieldBytes.Span[scan];
            if (current == (byte)'.')
            {
                scan = SkipFormatAttribute(fieldBytes.Span, scan, token);
                continue;
            }

            if (current == (byte)'[')
            {
                scan = SkipFormatBracket(fieldBytes.Span, scan, token);
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

        var position = -1L;
        string? keyword = null;
        // Only successful fields reach later selections. Sharing numbering
        // across nested templates preserves the order in which fields use it.
        if (rootEnd == 0)
        {
            if (nextPosition < 0)
                throw new InvalidOperationException("cannot switch from manual field specification to automatic field numbering");
            position = nextPosition++;
        }
        else
        {
            var root = fieldBytes.Span[..rootEnd];
            if (TryParseFormatIndex(root, out var explicitPosition, token))
            {
                if (nextPosition > 0)
                    throw new InvalidOperationException("cannot switch from automatic field numbering to manual field specification");
                nextPosition = -1;
                position = explicitPosition;
            }
            else keyword = Encoding.UTF8.GetString(root);
        }

        PyString? spec = null;
        FormatTemplate? nested = null;
        var recursive = false;
        if (specStart >= 0 && specStart < end)
        {
            var rawSpec = fieldBytes[specStart..];
            if (rawSpec.Span.Contains((byte)'{') || rawSpec.Span.Contains((byte)'}'))
            {
                recursive = depth >= 1;
                if (!recursive) nested = ParseFormatTemplate(rawSpec, depth + 1, owner, span, token, ref nextPosition);
            }
            else spec = PyString.CopyFromUtf8(rawSpec.Span, governor: null, span);
        }
        var prepared = LythonRuntime.PrepareConstantFormatSpecifier(spec is null ? default : spec.Utf8Span, span ?? new(0, 0, 0, 0), token);
        return new(position, keyword, accesses?.ToArray() ?? [], conversion, spec, nested, recursive, prepared);
    }

    private static FormatAccess PrepareFormatAccess(ReadOnlySpan<byte> bytes, bool member,
        LythonSourceSpan? span, CancellationToken token)
    {
        if (bytes.IsEmpty) return new(FormatAccessKind.Error, "Empty attribute in format string");
        if (member) return new(FormatAccessKind.Member, Encoding.UTF8.GetString(bytes));
        try
        {
            if (TryParseFormatIndex(bytes, out var index, token))
                return new(FormatAccessKind.IntegerIndex, null, index);
            var key = PyString.CopyFromUtf8(bytes, null, span);
            _ = key.GetHashCode(token, span);
            return new(FormatAccessKind.StringIndex, key);
        }
        catch (InvalidOperationException error) { return new(FormatAccessKind.Error, error.Message); }
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

    private static bool TryParseFormatIndex(ReadOnlySpan<byte> text, out long value, CancellationToken cancellationToken)
    {
        value = 0;
        if (text.IsEmpty) return false;
        var scalar = 0;
        for (var offset = 0; offset < text.Length;)
        {
            if ((scalar++ & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
            Rune.DecodeFromUtf8(text[offset..], out var digit, out var width);
            offset += width;
            if (Rune.GetUnicodeCategory(digit) != UnicodeCategory.DecimalDigitNumber) return false;
            // CPython checks overflow while walking, even before a later nondigit.
            var number = (int)Rune.GetNumericValue(digit);
            if (value > (long.MaxValue - number) / 10)
                throw new InvalidOperationException("Too many decimal digits in format string");
            value = value * 10 + number;
        }
        return true;
    }

}
