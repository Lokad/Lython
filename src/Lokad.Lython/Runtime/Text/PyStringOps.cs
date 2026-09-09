using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal interface IPyStringCoercibleValue
{
    PyString ToPyString();
}

internal static partial class PyStringOps
{
    public static readonly PyString NoneLiteral = PyString.FromOwnedUtf8([(byte)'N', (byte)'o', (byte)'n', (byte)'e']);
    public static readonly PyString SlashLiteral = PyString.FromOwnedUtf8([(byte)'/']);
    public static readonly PyString DotLiteral = PyString.FromOwnedUtf8([(byte)'.']);
    public static readonly PyString CommaLiteral = PyString.FromOwnedUtf8([(byte)',']);

    public static bool TryAsString(object value, out PyString text)
    {
        switch (value)
        {
            case PyString pyString:
                text = pyString;
                return true;
            case IPyStringCoercibleValue stringLike:
                text = stringLike.ToPyString();
                return true;
            default:
                text = PyString.Empty;
                return false;
        }
    }

    public static PyString TrimTrailingNewline(PyString value)
    {
        var bytes = value.Utf8Bytes.Span;
        if (bytes.Length == 0)
        {
            return value;
        }

        if (bytes[^1] == (byte)'\n')
        {
            var end = bytes.Length - 1;
            if (end > 0 && bytes[end - 1] == (byte)'\r')
            {
                end--;
            }

            return value.SliceByByteRange(0, end);
        }

        if (bytes[^1] == (byte)'\r')
        {
            return value.SliceByByteRange(0, bytes.Length - 1);
        }

        return value;
    }

    public static PyList SplitLines(PyString value)
        => SplitLines(value, false, null, null);

    public static PyList SplitLines(PyString value, bool keepEnds)
        => SplitLines(value, keepEnds, null, null);

    public static PyList SplitLines(PyString value, bool keepEnds, MemoryGovernor? governor)
        => SplitLines(value, keepEnds, governor, null);

    public static PyList SplitLines(PyString value, bool keepEnds, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        var lines = governor is null ? new PyList() : new PyList([], governor, span);
        var source = value.Utf8Bytes.Span;
        var startByte = 0;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            if (!TryGetLineBreakByteLength(source, byteIndex, out var lineBreakLength))
            {
                Rune.DecodeFromUtf8(source[byteIndex..], out _, out var runeLength);
                byteIndex += runeLength;
                continue;
            }

            var endByte = keepEnds ? byteIndex + lineBreakLength : byteIndex;
            lines.Add(SliceUtf8(value, startByte, endByte, governor, span));
            byteIndex += lineBreakLength;
            startByte = byteIndex;
        }

        if (startByte < source.Length)
        {
            lines.Add(SliceUtf8(value, startByte, source.Length, governor, span));
        }

        return lines;
    }

    public static PyList SplitWhitespace(PyString value)
        => SplitWhitespace(value, null, null);

    public static PyList SplitWhitespace(PyString value, MemoryGovernor? governor)
        => SplitWhitespace(value, governor, null);

    public static PyList SplitWhitespace(PyString value, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        var parts = governor is null ? new PyList() : new PyList([], governor, span);
        var source = value.Utf8Bytes.Span;
        var partStart = -1;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            if (Rune.IsWhiteSpace(rune))
            {
                if (partStart >= 0)
                {
                    parts.Add(SliceUtf8(value, partStart, byteIndex, governor, span));
                    partStart = -1;
                }
            }
            else
            {
                partStart = partStart >= 0 ? partStart : byteIndex;
            }

            byteIndex += runeLength;
        }

        if (partStart >= 0)
        {
            parts.Add(SliceUtf8(value, partStart, source.Length, governor, span));
        }

        return parts;
    }

    public static PyList SplitWhitespace(PyString value, int maxSplit)
        => SplitWhitespace(value, maxSplit, null, null);

    public static PyList SplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor)
        => SplitWhitespace(value, maxSplit, governor, null);

    public static PyList SplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        if (maxSplit < 0)
        {
            return SplitWhitespace(value, governor, span);
        }

        var parts = governor is null ? new PyList() : new PyList([], governor, span);
        var source = value.Utf8Bytes.Span;
        var startByte = 0;
        while (startByte < source.Length)
        {
            Rune.DecodeFromUtf8(source[startByte..], out var leadingRune, out var leadingRuneLength);
            if (!Rune.IsWhiteSpace(leadingRune))
            {
                break;
            }

            startByte += leadingRuneLength;
        }

        if (startByte >= source.Length)
        {
            return parts;
        }

        if (maxSplit == 0)
        {
            parts.Add(SliceUtf8(value, startByte, source.Length, governor, span));
            return parts;
        }

        var splits = 0;
        var byteIndex = startByte;
        while (byteIndex < source.Length)
        {
            var partStart = byteIndex;
            while (byteIndex < source.Length)
            {
                Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
                if (Rune.IsWhiteSpace(rune))
                {
                    break;
                }

                byteIndex += runeLength;
            }

            if (splits == maxSplit)
            {
                parts.Add(SliceUtf8(value, partStart, source.Length, governor, span));
                return parts;
            }

            parts.Add(SliceUtf8(value, partStart, byteIndex, governor, span));
            splits++;

            while (byteIndex < source.Length)
            {
                Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
                if (!Rune.IsWhiteSpace(rune))
                {
                    break;
                }

                byteIndex += runeLength;
            }
        }

        return parts;
    }

    public static PyList Split(PyString value, PyString separator)
        => Split(value, separator, null, null);

    public static PyList Split(PyString value, PyString separator, MemoryGovernor? governor)
        => Split(value, separator, governor, null);

    public static PyList Split(PyString value, PyString separator, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        var parts = governor is null ? new PyList() : new PyList([], governor, span);
        var source = value.Utf8Bytes.Span;
        var needle = separator.Utf8Bytes.Span;
        var offset = 0;
        while (true)
        {
            var found = PyString.IndexOfBytes(source[offset..], needle);
            if (found < 0)
            {
                parts.Add(SliceUtf8(value, offset, source.Length, governor, span));
                return parts;
            }

            parts.Add(SliceUtf8(value, offset, offset + found, governor, span));
            offset += found + needle.Length;
        }
    }

    public static PyList Split(PyString value, PyString separator, int maxSplit)
        => Split(value, separator, maxSplit, null, null);

    public static PyList Split(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor)
        => Split(value, separator, maxSplit, governor, null);

    public static PyList Split(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        if (separator.Length == 0)
        {
            throw new InvalidOperationException("empty separator");
        }

        if (maxSplit < 0)
        {
            return Split(value, separator, governor, span);
        }

        var parts = governor is null ? new PyList() : new PyList([], governor, span);
        var source = value.Utf8Bytes.Span;
        var needle = separator.Utf8Bytes.Span;
        var offset = 0;
        var splits = 0;
        while (splits < maxSplit)
        {
            var found = PyString.IndexOfBytes(source[offset..], needle);
            if (found < 0)
            {
                break;
            }

            parts.Add(SliceUtf8(value, offset, offset + found, governor, span));
            offset += found + needle.Length;
            splits++;
        }

        parts.Add(SliceUtf8(value, offset, source.Length, governor, span));
        return parts;
    }

    public static PyList RSplitWhitespace(PyString value, int maxSplit)
        => RSplitWhitespace(value, maxSplit, null, null);

    public static PyList RSplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor)
        => RSplitWhitespace(value, maxSplit, governor, null);

    public static PyList RSplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        if (maxSplit < 0)
        {
            return SplitWhitespace(value, governor, span);
        }

        var source = value.Utf8Bytes.Span;
        var endByte = source.Length;
        while (endByte > 0)
        {
            var runeStart = GetPreviousRuneStart(source, endByte);
            Rune.DecodeFromUtf8(source[runeStart..endByte], out var rune, out _);
            if (!Rune.IsWhiteSpace(rune))
            {
                break;
            }

            endByte = runeStart;
        }

        var parts = new List<PyString>();
        if (endByte == 0)
        {
            return governor is null ? new PyList() : new PyList([], governor, span);
        }

        if (maxSplit == 0)
        {
            var only = SliceUtf8(value, 0, endByte, governor, span);
            return governor is null ? new PyList([only]) : new PyList([only], governor, span);
        }

        var splits = 0;
        while (endByte > 0)
        {
            var partEnd = endByte;
            var partStart = endByte;
            while (partStart > 0)
            {
                var runeStart = GetPreviousRuneStart(source, partStart);
                Rune.DecodeFromUtf8(source[runeStart..partStart], out var rune, out _);
                if (Rune.IsWhiteSpace(rune))
                {
                    break;
                }

                partStart = runeStart;
            }

            if (splits == maxSplit)
            {
                parts.Add(SliceUtf8(value, 0, partEnd, governor, span));
                break;
            }

            parts.Add(SliceUtf8(value, partStart, partEnd, governor, span));
            splits++;

            endByte = partStart;
            while (endByte > 0)
            {
                var runeStart = GetPreviousRuneStart(source, endByte);
                Rune.DecodeFromUtf8(source[runeStart..endByte], out var rune, out _);
                if (!Rune.IsWhiteSpace(rune))
                {
                    break;
                }

                endByte = runeStart;
            }

            if (endByte == 0)
            {
                break;
            }
        }

        parts.Reverse();
        var items = new object[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            items[i] = parts[i];
        }

        return governor is null ? new PyList(items) : new PyList(items, governor, span);
    }

    public static PyList RSplit(PyString value, PyString separator, int maxSplit)
        => RSplit(value, separator, maxSplit, null, null);

    public static PyList RSplit(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor)
        => RSplit(value, separator, maxSplit, governor, null);

    public static PyList RSplit(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        if (separator.Length == 0)
        {
            throw new InvalidOperationException("empty separator");
        }

        if (maxSplit < 0)
        {
            return Split(value, separator, governor, span);
        }

        var source = value.Utf8Bytes.Span;
        var needle = separator.Utf8Bytes.Span;
        var parts = new List<PyString>();
        var end = source.Length;
        var splits = 0;
        while (splits < maxSplit)
        {
            var found = LastIndexOfBytes(source[..end], needle);
            if (found < 0)
            {
                break;
            }

            parts.Add(SliceUtf8(value, found + needle.Length, end, governor, span));
            end = found;
            splits++;
        }

        parts.Add(SliceUtf8(value, 0, end, governor, span));
        parts.Reverse();
        var items = new object[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            items[i] = parts[i];
        }

        return governor is null ? new PyList(items) : new PyList(items, governor, span);
    }

    public static PyString Strip(PyString value) => SliceTrimmed(value, trimStart: true, trimEnd: true);

    public static PyString LStrip(PyString value) => SliceTrimmed(value, trimStart: true, trimEnd: false);

    public static PyString RStrip(PyString value) => SliceTrimmed(value, trimStart: false, trimEnd: true);

    public static PyString Strip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: true, trimEnd: true, chars);

    public static PyString LStrip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: true, trimEnd: false, chars);

    public static PyString RStrip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: false, trimEnd: true, chars);

    public static BigInteger Find(PyString value, PyString needle) => new(value.Find(needle));

    public static BigInteger Find(PyString value, PyString needle, int start, int end)
    {
        var slice = SliceRange(value, start, end);
        var found = slice.Find(needle);
        return new(found < 0 ? -1 : start + found);
    }

    public static BigInteger RFind(PyString value, PyString needle, int start, int end)
    {
        var slice = SliceRange(value, start, end);
        var found = LastFind(slice, needle);
        return new(found < 0 ? -1 : start + found);
    }

    public static BigInteger Count(PyString value, PyString needle, int start, int end)
    {
        var slice = SliceRange(value, start, end);
        return new BigInteger(slice.Count(needle));
    }

    public static bool StartsWith(PyString value, PyString prefix, int start, int end)
    {
        return SliceRange(value, start, end).StartsWith(prefix);
    }

    public static bool EndsWith(PyString value, PyString suffix, int start, int end)
    {
        return SliceRange(value, start, end).EndsWith(suffix);
    }

    public static PyTuple Partition(PyString value, PyString separator)
        => Partition(value, separator, null, null);

    public static PyTuple Partition(PyString value, PyString separator, MemoryGovernor? governor)
        => Partition(value, separator, governor, null);

    public static PyTuple Partition(PyString value, PyString separator, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (separator.Length == 0)
        {
            throw new InvalidOperationException("empty separator");
        }

        var index = PyString.IndexOfBytes(value.Utf8Bytes.Span, separator.Utf8Bytes.Span);
        return index < 0
            ? CreateTuple([value, PyString.Empty, PyString.Empty], governor, span)
            : CreateTuple([
                SliceByByteRange(value, 0, index),
                separator,
                SliceByByteRange(value, index + separator.Utf8Bytes.Length, value.Utf8Bytes.Length)
            ], governor, span);
    }

    public static PyTuple RPartition(PyString value, PyString separator)
        => RPartition(value, separator, null, null);

    public static PyTuple RPartition(PyString value, PyString separator, MemoryGovernor? governor)
        => RPartition(value, separator, governor, null);

    public static PyTuple RPartition(PyString value, PyString separator, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (separator.Length == 0)
        {
            throw new InvalidOperationException("empty separator");
        }

        var index = LastIndexOfBytes(value.Utf8Bytes.Span, separator.Utf8Bytes.Span);
        return index < 0
            ? CreateTuple([PyString.Empty, PyString.Empty, value], governor, span)
            : CreateTuple([
                SliceByByteRange(value, 0, index),
                separator,
                SliceByByteRange(value, index + separator.Utf8Bytes.Length, value.Utf8Bytes.Length)
            ], governor, span);
    }

    private static PyTuple CreateTuple(IEnumerable<object> items, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? new PyTuple(items) : new PyTuple(items, governor, span);

    public static PyString Center(PyString value, int width)
        => Center(value, width, null);

    public static PyString Center(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Center);
    }

    public static PyString LJust(PyString value, int width)
        => LJust(value, width, null);

    public static PyString LJust(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Left);
    }

    public static PyString RJust(PyString value, int width)
        => RJust(value, width, null);

    public static PyString RJust(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Right);
    }

    public static PyString ZFill(PyString value, int width)
    {
        if (width <= value.Length)
        {
            return value;
        }

        // Bound the expansion before the padding buffer is built: the result
        // is exactly the source bytes plus one ASCII zero per missing char, so a
        // hostile width fails here instead of materializing multi-megabyte CLR
        // strings (or worse) ahead of the governed result charge.
        var governor = value.OwnerMemoryGovernor;
        if (governor is not null)
        {
            governor.EnsureCanReserve(32L + value.Utf8Bytes.Length + ((long)width - value.Length), value.AllocationSpan);
        }

        var text = value.AsString();
        var prefix = text.Length > 0 && (text[0] == '+' || text[0] == '-') ? text[..1] : string.Empty;
        var rest = prefix.Length == 0 ? text : text[1..];
        return FromString(prefix + new string('0', width - value.Length) + rest, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    public static PyString ExpandTabs(PyString value, int tabSize)
    {
        if (tabSize < 0)
        {
            tabSize = 0;
        }

        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        var source = value.Utf8Bytes.Span;
        var column = 0;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            if (rune.Value == '\t')
            {
                var spaces = tabSize == 0 ? 0 : tabSize - (column % tabSize);
                for (var i = 0; i < spaces; i++)
                {
                    builder.Append((byte)' ');
                }

                column += spaces;
            }
            else
            {
                builder.Append(source.Slice(byteIndex, runeLength));
                if (rune.Value is '\n' or '\r')
                {
                    column = 0;
                }
                else
                {
                    column++;
                }
            }

            byteIndex += runeLength;
        }

        return builder.ToPyStringAndRelease();
    }

}
