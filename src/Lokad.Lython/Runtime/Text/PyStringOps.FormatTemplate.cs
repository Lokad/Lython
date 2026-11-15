using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // A bound method keeps this immutable layout in its execution owner, never
    // on the shared literal. It contains no guest values, callbacks or tokens.
    internal sealed record FormatTemplate(FormatPart[] Parts, MemoryGovernor? Owner);

    internal readonly record struct FormatPart(ReadOnlyMemory<byte> Literal, FormatField? Field = null, string? Error = null);

    internal sealed record FormatField(string Root, string Suffix, string Name, Rune? Conversion,
        string? Spec, FormatTemplate? NestedSpec, bool RecursiveSpec,
        LythonRuntime.InterpolatedFormatSpecifier? PreparedSpec);

    internal static FormatTemplate PrepareFormatTemplate(PyString template, MemoryGovernor governor,
        LythonSourceSpan? span, CancellationToken token)
    {
        var bytes = EstimateFormatTemplateBytes(template, token);
        governor.Reserve(bytes, span);
        try
        {
            var result = ParseFormatTemplate(template.Utf8Bytes, 0, governor, span, token);
            governor.Commit(bytes);
            return result;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static long EstimateFormatTemplateBytes(PyString template, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long braces = 0;
        var source = template.Utf8Span;
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested();
            if (source[i] is (byte)'{' or (byte)'}') braces++;
        }
        // Two supported levels, list growth/replacement overlap, part/field
        // records, UTF16 names/specs and the shared native spec descriptors.
        return checked(512L + 8L * source.Length + 384L * braces);
    }

    private static FormatTemplate ParseFormatTemplate(ReadOnlyMemory<byte> source, int depth,
        MemoryGovernor? owner, LythonSourceSpan? span, CancellationToken token)
    {
        var parts = new List<FormatPart>();
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
                return new(parts.ToArray(), owner);
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
                return new(parts.ToArray(), owner);
            }
            try { parts.Add(new(default, ParseFormatField(source[(i + 1)..close], depth, owner, span, token))); }
            // Store grammar failures at their original execution position.
            // Earlier fields still perform their lookups, hooks and side effects.
            catch (InvalidOperationException error)
            {
                parts.Add(new(default, Error: error.Message));
                return new(parts.ToArray(), owner);
            }
            i = close;
            literal = i + 1;
        }
        if (literal < source.Length) parts.Add(new(source[literal..]));
        return new(parts.ToArray(), owner);
    }
    private static FormatField ParseFormatField(ReadOnlyMemory<byte> fieldBytes, int depth,
        MemoryGovernor? owner, LythonSourceSpan? span, CancellationToken token)
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
        while (index < end && (fieldBytes.Span[index] == (byte)'.' || fieldBytes.Span[index] == (byte)'['))
        {
            index = fieldBytes.Span[index] == (byte)'.'
                ? SkipFormatAttribute(fieldBytes.Span, index, token)
                : SkipFormatBracket(fieldBytes.Span, index, token);
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

        var root = Encoding.UTF8.GetString(fieldBytes.Span[..rootEnd]);
        var suffixEnd = fieldNameEnd;
        if (trailing)
        {
            Rune.DecodeFromUtf8(fieldBytes.Span[fieldNameEnd..], out _, out var width);
            suffixEnd += width;
        }
        var suffix = Encoding.UTF8.GetString(fieldBytes.Span[rootEnd..suffixEnd]);
        string? spec = null;
        FormatTemplate? nested = null;
        var recursive = false;
        if (specStart >= 0 && specStart < end)
        {
            var rawSpec = fieldBytes[specStart..];
            if (rawSpec.Span.Contains((byte)'{') || rawSpec.Span.Contains((byte)'}'))
            {
                recursive = depth >= 1;
                if (!recursive) nested = ParseFormatTemplate(rawSpec, depth + 1, owner, span, token);
            }
            else spec = Encoding.UTF8.GetString(rawSpec.Span);
        }
        var prepared = LythonRuntime.PrepareConstantFormatSpecifier(spec, span ?? new(0, 0, 0, 0), token);
        return new(root, suffix, Encoding.UTF8.GetString(fieldBytes.Span[..fieldNameEnd]),
            conversion, spec, nested, recursive, prepared);
    }


}
