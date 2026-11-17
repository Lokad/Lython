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
    public static readonly PyString EllipsisLiteral = PyString.FromString("Ellipsis");
    public static readonly PyString NotImplementedLiteral = PyString.FromString("NotImplemented");
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
        var bytes = value.Utf8Span;
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

    public static PyList SplitLines(PyString value, bool keepEnds, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitLineRanges(value, keepEnds, governor, span, context);

    public static PyList SplitWhitespace(PyString value)
        => SplitWhitespace(value, null, null);

    public static PyList SplitWhitespace(PyString value, MemoryGovernor? governor)
        => SplitWhitespace(value, governor, null);

    public static PyList SplitWhitespace(PyString value, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, -1, false, governor, span, context);

    public static PyList SplitWhitespace(PyString value, int maxSplit)
        => SplitWhitespace(value, maxSplit, null, null);

    public static PyList SplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor)
        => SplitWhitespace(value, maxSplit, governor, null);

    public static PyList SplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, maxSplit, false, governor, span, context);

    public static PyList Split(PyString value, PyString separator)
        => Split(value, separator, null, null);

    public static PyList Split(PyString value, PyString separator, MemoryGovernor? governor)
        => Split(value, separator, governor, null);

    public static PyList Split(PyString value, PyString separator, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, -1, false, governor, span, context, separator: separator);

    public static PyList Split(PyString value, PyString separator, int maxSplit)
        => Split(value, separator, maxSplit, null, null);

    public static PyList Split(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor)
        => Split(value, separator, maxSplit, governor, null);

    public static PyList Split(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, maxSplit, false, governor, span, context, separator: separator);

    public static PyList RSplitWhitespace(PyString value, int maxSplit)
        => RSplitWhitespace(value, maxSplit, null, null);

    public static PyList RSplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor)
        => RSplitWhitespace(value, maxSplit, governor, null);

    public static PyList RSplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, maxSplit, maxSplit >= 0, governor, span, context);

    public static PyList RSplit(PyString value, PyString separator, int maxSplit)
        => RSplit(value, separator, maxSplit, null, null);

    public static PyList RSplit(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor)
        => RSplit(value, separator, maxSplit, governor, null);

    public static PyList RSplit(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null)
        => SplitCore(value, maxSplit, true, governor, span, context, separator: separator);

    public static PyString Strip(PyString value) => SliceTrimmed(value, trimStart: true, trimEnd: true);

    public static PyString LStrip(PyString value) => SliceTrimmed(value, trimStart: true, trimEnd: false);

    public static PyString RStrip(PyString value) => SliceTrimmed(value, trimStart: false, trimEnd: true);

    public static PyString Strip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: true, trimEnd: true, chars);

    public static PyString LStrip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: true, trimEnd: false, chars);

    public static PyString RStrip(PyString value, PyString chars) => SliceTrimmed(value, trimStart: false, trimEnd: true, chars);

    public static BigInteger Find(PyString value, PyString needle) => new(value.Find(needle));

    // Callers prepare one validated UTF-8 window; only index results need
    // scalar conversion. Tuple affixes reuse the same borrowed window.
    public static BigInteger Find(PyString value, PyString needle, int startByte, int endByte)
    {
        var found = PyString.IndexOfBytes(value.Utf8Span[startByte..endByte], needle.Utf8Span);
        return new(found < 0 ? -1 : value.ByteIndexToRuneIndex(startByte + found));
    }

    public static BigInteger RFind(PyString value, PyString needle, int startByte, int endByte)
    {
        var found = LastIndexOfBytes(value.Utf8Span[startByte..endByte], needle.Utf8Span);
        return new(found < 0 ? -1 : value.ByteIndexToRuneIndex(startByte + found));
    }

    public static BigInteger Count(PyString value, PyString needle, int startByte, int endByte)
    {
        if (!needle.IsTruthy())
            return new BigInteger(value.ByteIndexToRuneIndex(endByte) - value.ByteIndexToRuneIndex(startByte)) + BigInteger.One;

        var span = value.Utf8Span[startByte..endByte];
        var needleBytes = needle.Utf8Span;
        var count = 0;
        var offset = 0;
        while (true)
        {
            PyStructuralGuard.NoteWork();
            var found = PyString.IndexOfBytes(span[offset..], needleBytes);
            if (found < 0) return new BigInteger(count);
            count++;
            offset += found + needleBytes.Length;
        }
    }

    public static bool StartsWith(PyString value, PyString prefix, int startByte, int endByte)
        => value.Utf8Span[startByte..endByte].StartsWith(prefix.Utf8Span);

    public static bool EndsWith(PyString value, PyString suffix, int startByte, int endByte)
        => value.Utf8Span[startByte..endByte].EndsWith(suffix.Utf8Span);

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

        var index = PyString.IndexOfBytes(value.Utf8Span, separator.Utf8Span);
        // Slice items through the call governor like split does: the value-owned
        // helper below would leave literal-receiver partitions ungoverned.
        return index < 0
            ? CreateTuple([value, PyString.Empty, PyString.Empty], governor, span)
            : CreateTuple([
                SliceUtf8(value, 0, index, governor, span),
                separator,
                SliceUtf8(value, index + separator.ByteLength, value.ByteLength, governor, span)
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

        var index = LastIndexOfBytes(value.Utf8Span, separator.Utf8Span);
        return index < 0
            ? CreateTuple([PyString.Empty, PyString.Empty, value], governor, span)
            : CreateTuple([
                SliceUtf8(value, 0, index, governor, span),
                separator,
                SliceUtf8(value, index + separator.ByteLength, value.ByteLength, governor, span)
            ], governor, span);
    }

    private static PyTuple CreateTuple(IEnumerable<object> items, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? new PyTuple(items) : new PyTuple(items, governor, span);

    public static PyString Center(PyString value, int width)
        => Center(value, width, null);

    public static PyString Center(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Center, value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan, default);
    }

    internal static PyString Center(PyString value, int width, PyString? fillChar,
        MemoryGovernor governor, LythonSourceSpan span, CancellationToken cancellationToken)
        => Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Center, governor, span, cancellationToken);

    public static PyString LJust(PyString value, int width)
        => LJust(value, width, null);

    public static PyString LJust(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Left, value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan, default);
    }

    internal static PyString LJust(PyString value, int width, PyString? fillChar,
        MemoryGovernor governor, LythonSourceSpan span, CancellationToken cancellationToken)
        => Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Left, governor, span, cancellationToken);

    public static PyString RJust(PyString value, int width)
        => RJust(value, width, null);

    public static PyString RJust(PyString value, int width, PyString? fillChar)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Right, value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan, default);
    }

    internal static PyString RJust(PyString value, int width, PyString? fillChar,
        MemoryGovernor governor, LythonSourceSpan span, CancellationToken cancellationToken)
        => Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Right, governor, span, cancellationToken);

    public static PyString ZFill(PyString value, int width)
        => ZFill(value, width, value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan, default);

    internal static PyString ZFill(PyString value, int width, MemoryGovernor? governor,
        LythonSourceSpan? span, CancellationToken cancellationToken)
    {
        if (width <= value.Length) return value;
        cancellationToken.ThrowIfCancellationRequested();
        var padding = width - value.Length;
        var bytes = checked((long)value.ByteLength + padding);
        if (bytes > int.MaxValue) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        var builder = CreateBuilder(governor, span, (int)bytes);
        try
        {
            var source = value.Utf8Span;
            if (!source.IsEmpty && source[0] is (byte)'+' or (byte)'-')
            {
                builder.Append(source[0]);
                source = source[1..];
            }
            while (padding > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(padding, 4096);
                builder.AppendRepeated((byte)'0', count);
                padding -= count;
            }
            while (!source.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(source.Length, 4096);
                builder.Append(source[..count]);
                source = source[count..];
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public static PyString ExpandTabs(PyString value, int tabSize,
        LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan;
        var source = value.Utf8Span;
        var length = TabExpansionCapacity(source, tabSize, context, span, out var hasTabs);
        if (!hasTabs) return value;
        if (length == 0) return PyString.Empty;
        var governor = context?.MemoryGovernor ?? value.OwnerMemoryGovernor;
        var uncommitted = PyString.EstimateApproximateBytes(length);
        governor?.Reserve(uncommitted, span);
        try
        {
            context?.CheckExecution(span);
            var output = PyString.CreateReservedUtf8(length, governor);
            var written = ExpandTabsUtf8(source, output.UnpublishedUtf8, tabSize, context, span);
            output.CompleteUnpublishedUtf8(written);
            context?.CheckExecution(span);
            governor?.Commit(uncommitted);
            uncommitted = 0;
            return output;
        }
        finally { if (uncommitted != 0) governor?.ReleaseReserved(uncommitted); }
    }

    private static int TabExpansionCapacity(ReadOnlySpan<byte> source, int tabSize,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span, out bool hasTabs)
    {
        var tabs = 0;
        for (var offset = 0; offset < source.Length;)
        {
            context?.CheckExecution(span);
            var count = Math.Min(4096, source.Length - offset);
            tabs += source.Slice(offset, count).Count((byte)'\t');
            offset += count;
        }
        context?.CheckExecution(span);
        hasTabs = tabs != 0;
        var capacity = tabSize <= 0 ? source.Length - tabs
            : checked(source.Length + (long)tabs * (tabSize - 1L));
        // A conservative bound cannot establish an actual representation error.
        // Determine the exact length before admission in that uncommon case;
        // refusal is never retried with a smaller reservation.
        return capacity <= Array.MaxLength ? (int)capacity
            : ExpandTabsUtf8(source, default, tabSize, context, span);
    }

    // Normal construction measures tab bytes only, then processes scalar columns
    // once into fully admitted final capacity. An empty destination is reserved
    // for exact sizing when a capacity bound exceeds the representation limit.
    private static int ExpandTabsUtf8(ReadOnlySpan<byte> source, Span<byte> output, int tabSize,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        long column = 0;
        var written = 0;
        var writing = !output.IsEmpty;
        for (var offset = 0; offset < source.Length;)
        {
            context?.CheckExecution(span);
            var end = offset + Math.Min(4096, source.Length - offset);
            while (end < source.Length && (source[end] & 0xc0) == 0x80) end--;
            while (offset < end)
            {
                var control = source[offset..end].IndexOfAny((byte)'\t', (byte)'\r', (byte)'\n');
                var count = control < 0 ? end - offset : control;
                var literal = source.Slice(offset, count);
                var characters = literal;
                while (!characters.IsEmpty)
                {
                    var ascii = characters.IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
                    if (ascii < 0) { column += characters.Length; break; }
                    column += ascii + 1;
                    var lead = characters[ascii];
                    var width = lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;
                    characters = characters[(ascii + width)..];
                }
                if ((long)written + count > Array.MaxLength)
                    throw RuntimeErrors.Memory("expanded string exceeds supported capacity", span);
                if (writing) literal.CopyTo(output[written..]);
                written += count;
                offset += count;
                if (control < 0) break;
                if (source[offset++] == '\t')
                {
                    var spaces = tabSize <= 0 ? 0 : (int)(tabSize - column % tabSize);
                    column += spaces;
                    if ((long)written + spaces > Array.MaxLength)
                        throw RuntimeErrors.Memory("expanded string exceeds supported capacity", span);
                    var after = written + spaces;
                    while (writing && written < after)
                    {
                        context?.CheckExecution(span);
                        var chunk = Math.Min(after - written, 4096);
                        output.Slice(written, chunk).Fill((byte)' ');
                        written += chunk;
                    }
                    written = after;
                }
                else
                {
                    if (written == Array.MaxLength)
                        throw RuntimeErrors.Memory("expanded string exceeds supported capacity", span);
                    if (writing) output[written] = source[offset - 1];
                    written++;
                    column = 0;
                }
            }
        }
        context?.CheckExecution(span);
        return written;
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
