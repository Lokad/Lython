using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal interface IPyStringCoercibleValue
{
    PyString ToPyString();
}

internal static class PyStringOps
{
    public static readonly PyString NoneLiteral = PyString.FromOwnedUtf8([(byte)'N', (byte)'o', (byte)'n', (byte)'e']);
    public static readonly PyString SlashLiteral = PyString.FromOwnedUtf8([(byte)'/']);
    public static readonly PyString DotLiteral = PyString.FromOwnedUtf8([(byte)'.']);
    public static readonly PyString CommaLiteral = PyString.FromOwnedUtf8([(byte)',']);
    private static readonly PyString NewlineLiteral = PyString.FromOwnedUtf8([(byte)'\n']);
    private static readonly PyString CarriageReturnLiteral = PyString.FromOwnedUtf8([(byte)'\r']);
    private static readonly PyString VerticalTabLiteral = PyString.FromOwnedUtf8([(byte)'\v']);
    private static readonly PyString FormFeedLiteral = PyString.FromOwnedUtf8([(byte)'\f']);
    private static readonly PyString LineSeparatorLiteral = PyString.FromString("\u2028");
    private static readonly PyString ParagraphSeparatorLiteral = PyString.FromString("\u2029");

    public static bool TryAsString(object value, out PyString text)
    {
        switch (value)
        {
            case PyString pyString:
                text = pyString;
                return true;
            case string legacy:
                text = PyString.FromString(legacy);
                return true;
            case IPyStringCoercibleValue stringLike:
                text = stringLike.ToPyString();
                return true;
            default:
                text = PyString.Empty;
                return false;
        }
    }

    public static string AsDotNetString(object value)
    {
        return value switch
        {
            PyString text => text.AsString(),
            string legacy => legacy,
            _ => throw new InvalidOperationException("Value is not a Python string.")
        };
    }

    public static PyString DecodeUtf8(ReadOnlyMemory<byte> utf8) => PyString.FromUtf8(utf8);

    public static byte[] EncodeUtf8(PyString value) => value.Utf8Bytes.ToArray();

    public static PyString NormalizeNewlines(PyString value)
    {
        var source = value.Utf8Bytes.Span;
        var builder = CreateBuilder(value, source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == (byte)'\r')
            {
                if (i + 1 < source.Length && source[i + 1] == (byte)'\n')
                {
                    i++;
                }

                builder.Append((byte)'\n');
            }
            else
            {
                builder.Append(source[i]);
            }
        }

        return builder.ToPyString();
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

    public static PyList SplitLines(PyString value, bool keepEnds = false, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList SplitWhitespace(PyString value, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList SplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList Split(PyString value, PyString separator, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList Split(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList RSplitWhitespace(PyString value, int maxSplit, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyList RSplit(PyString value, PyString separator, int maxSplit, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyTuple Partition(PyString value, PyString separator, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyTuple RPartition(PyString value, PyString separator, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
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

    public static PyString Center(PyString value, int width, PyString? fillChar = null)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Center);
    }

    public static PyString LJust(PyString value, int width, PyString? fillChar = null)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Left);
    }

    public static PyString RJust(PyString value, int width, PyString? fillChar = null)
    {
        return Pad(value, width, fillChar ?? PyString.FromString(" "), PadMode.Right);
    }

    public static PyString ZFill(PyString value, int width)
    {
        if (width <= value.Length)
        {
            return value;
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

        return builder.ToPyString();
    }

    public static PyString Format(
        PyString template,
        IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords = null,
        Func<string, object>? resolveCompositeField = null)
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
                    else if (keywords is null || !keywords.TryGetValue(slotText, out selected!))
                    {
                        throw new KeyNotFoundException(slotText);
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

        return builder.ToPyString();
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

        return builder.ToPyString();
    }

    public static bool IsLower(PyString value)
    {
        return CheckCased(value, expectLower: true);
    }

    public static bool IsUpper(PyString value)
    {
        return CheckCased(value, expectLower: false);
    }

    public static PyString Lower(PyString value) => MapCase(value, CaseMapping.Lower);

    public static PyString Upper(PyString value) => MapCase(value, CaseMapping.Upper);

    public static bool IsAlpha(PyString value)
    {
        return CheckAllRunes(value, static rune => Rune.IsLetter(rune));
    }

    public static bool IsDigit(PyString value)
    {
        return CheckAllRunes(value, static rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber;
        });
    }

    public static bool IsAlnum(PyString value)
    {
        return CheckAllRunes(value, static rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return Rune.IsLetter(rune) || category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber;
        });
    }

    public static bool IsSpace(PyString value)
    {
        return CheckAllRunes(value, static rune => Rune.IsWhiteSpace(rune));
    }

    public static PyString Capitalize(PyString value)
    {
        var source = value.Utf8Bytes.Span;
        if (source.Length == 0)
        {
            return PyString.Empty;
        }

        var builder = CreateBuilder(value, source.Length);
        var byteIndex = 0;
        var first = true;
        while (byteIndex < source.Length)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            builder.AppendString(MapCase(rune, first ? CaseMapping.Title : CaseMapping.Lower));
            first = false;
            byteIndex += runeLength;
        }

        return builder.ToPyString();
    }

    public static PyString SwapCase(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            var text = rune.ToString();
            var lower = MapCase(rune, CaseMapping.Lower);
            var upper = MapCase(rune, CaseMapping.Upper);
            builder.AppendString(text == lower && lower != upper ? upper : text == upper && lower != upper ? lower : text);
            byteIndex += runeLength;
        }

        return builder.ToPyString();
    }

    public static PyString Title(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        var previousWasCased = false;
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            var text = rune.ToString();
            var lower = MapCase(rune, CaseMapping.Lower);
            var upper = MapCase(rune, CaseMapping.Upper);
            var isCased = lower != upper;
            if (!isCased)
            {
                builder.AppendString(text);
                previousWasCased = false;
            }
            else
            {
                builder.AppendString(previousWasCased ? lower : MapCase(rune, CaseMapping.Title));
                previousWasCased = true;
            }

            byteIndex += runeLength;
        }

        return builder.ToPyString();
    }

    public static PyString Replace(PyString value, PyString oldValue, PyString newValue, int count)
    {
        if (count == 0)
        {
            return value;
        }

        if (count < 0)
        {
            return value.Replace(oldValue, newValue);
        }

        if (oldValue.Utf8Bytes.Length == 0)
        {
            var builder = CreateBuilder(value, value.Utf8Bytes.Length + ((Math.Min(value.Length + 1, count)) * newValue.Utf8Bytes.Length));
            var replacements = 0;
            if (replacements < count)
            {
                builder.Append(newValue);
                replacements++;
            }

            foreach (var rune in value.EnumerateRunes())
            {
                builder.Append(rune);
                if (replacements < count)
                {
                    builder.Append(newValue);
                    replacements++;
                }
            }

            return builder.ToPyString();
        }

        var result = CreateBuilder(value, value.Utf8Bytes.Length);
        var offset = 0;
        var replacementsCount = 0;
        var bytes = value.Utf8Bytes.Span;
        var needle = oldValue.Utf8Bytes.Span;
        while (offset < bytes.Length && replacementsCount < count)
        {
            var found = PyString.IndexOfBytes(bytes[offset..], needle);
            if (found < 0)
            {
                break;
            }

            result.Append(bytes[offset..(offset + found)]);
            result.Append(newValue);
            offset += found + needle.Length;
            replacementsCount++;
        }

        result.Append(bytes[offset..]);
        return result.ToPyString();
    }

    public static (int Start, int End) NormalizeRange(int length, object? start, object? end)
    {
        var normalizedStart = NormalizeBound(start, length, defaultValue: 0);
        var normalizedEnd = NormalizeBound(end, length, defaultValue: length);
        if (normalizedEnd < normalizedStart)
        {
            normalizedEnd = normalizedStart;
        }

        return (normalizedStart, normalizedEnd);
    }

    private static PyString SliceTrimmed(PyString value, bool trimStart, bool trimEnd, PyString? chars = null)
    {
        HashSet<PyString>? trimChars = chars is null ? null : new HashSet<PyString>(chars.EnumerateRunes());
        var source = value.Utf8Bytes.Span;
        var startByte = 0;
        var endByte = source.Length;

        if (trimStart)
        {
            while (startByte < endByte)
            {
                Rune.DecodeFromUtf8(source[startByte..], out _, out var runeLength);
                var rune = SliceUtf8(value, startByte, startByte + runeLength, value.OwnerMemoryGovernor, value.AllocationSpan);
                if (!ShouldTrim(rune, trimChars))
                {
                    break;
                }

                startByte += runeLength;
            }
        }

        if (trimEnd)
        {
            while (endByte > startByte)
            {
                var runeStart = GetPreviousRuneStart(source, endByte);
                var rune = SliceUtf8(value, runeStart, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
                if (!ShouldTrim(rune, trimChars))
                {
                    break;
                }

                endByte = runeStart;
            }
        }

        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    private static bool CheckCased(PyString value, bool expectLower)
    {
        var sawCasedRune = false;
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            var text = rune.ToString();
            var lower = MapCase(rune, CaseMapping.Lower);
            var upper = MapCase(rune, CaseMapping.Upper);
            if (lower == upper)
            {
                byteIndex += runeLength;
                continue;
            }

            sawCasedRune = true;
            if (expectLower ? text != lower : text != upper)
            {
                return false;
            }

            byteIndex += runeLength;
        }

        return sawCasedRune;
    }

    private static PyString MapCase(PyString value, CaseMapping mapping)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            builder.AppendString(MapCase(rune, mapping));
        }

        return builder.ToPyString();
    }

    private static string MapCase(Rune rune, CaseMapping mapping)
    {
        if (mapping == CaseMapping.Lower && rune.Value == 0x0130)
        {
            return "i\u0307";
        }

        if (mapping is CaseMapping.Upper or CaseMapping.Title)
        {
            var special = (rune.Value, mapping) switch
            {
                (0x00DF, CaseMapping.Upper) => "SS",
                (0x00DF, CaseMapping.Title) => "Ss",
                (0x0149, _) => "\u02BCN",
                (0x01F0, _) => "J\u030C",
                (0x0390, _) => "\u0399\u0308\u0301",
                (0x03B0, _) => "\u03A5\u0308\u0301",
                (0x0587, CaseMapping.Upper) => "\u0535\u0552",
                (0x0587, CaseMapping.Title) => "\u0535\u0582",
                (0xFB00, CaseMapping.Upper) => "FF",
                (0xFB00, CaseMapping.Title) => "Ff",
                (0xFB01, CaseMapping.Upper) => "FI",
                (0xFB01, CaseMapping.Title) => "Fi",
                (0xFB02, CaseMapping.Upper) => "FL",
                (0xFB02, CaseMapping.Title) => "Fl",
                (0xFB03, CaseMapping.Upper) => "FFI",
                (0xFB03, CaseMapping.Title) => "Ffi",
                (0xFB04, CaseMapping.Upper) => "FFL",
                (0xFB04, CaseMapping.Title) => "Ffl",
                (0xFB05 or 0xFB06, CaseMapping.Upper) => "ST",
                (0xFB05 or 0xFB06, CaseMapping.Title) => "St",
                (0xFB13, CaseMapping.Upper) => "\u0544\u0546",
                (0xFB13, CaseMapping.Title) => "\u0544\u0576",
                (0xFB14, CaseMapping.Upper) => "\u0544\u0535",
                (0xFB14, CaseMapping.Title) => "\u0544\u0565",
                (0xFB15, CaseMapping.Upper) => "\u0544\u053B",
                (0xFB15, CaseMapping.Title) => "\u0544\u056B",
                (0xFB16, CaseMapping.Upper) => "\u054E\u0546",
                (0xFB16, CaseMapping.Title) => "\u054E\u0576",
                (0xFB17, CaseMapping.Upper) => "\u0544\u053D",
                (0xFB17, CaseMapping.Title) => "\u0544\u056D",
                _ => null
            };
            if (special is not null)
            {
                return special;
            }
        }

        var text = rune.ToString();
        return mapping switch
        {
            CaseMapping.Lower => text.ToLowerInvariant(),
            CaseMapping.Upper => text.ToUpperInvariant(),
            CaseMapping.Title => text.ToUpperInvariant(),
            _ => text
        };
    }

    private static bool CheckAllRunes(PyString value, Func<Rune, bool> predicate)
    {
        var sawRune = false;
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            sawRune = true;
            if (!predicate(rune))
            {
                return false;
            }

            byteIndex += runeLength;
        }

        return sawRune;
    }

    private static bool ShouldTrim(PyString rune, HashSet<PyString>? trimChars)
    {
        return trimChars is null
            ? Rune.IsWhiteSpace(DecodeSingleRune(rune))
            : trimChars.Contains(rune);
    }

    private static PyString ConcatRunes(IEnumerable<PyString> runes)
    {
        var materialized = runes as PyString[] ?? [.. runes];
        var owner = materialized.Length != 0 ? materialized[0] : PyString.Empty;
        var builder = CreateBuilder(owner);
        foreach (var rune in materialized)
        {
            builder.Append(rune);
        }

        return builder.ToPyString();
    }

    private static bool IsLineBreak(PyString rune)
    {
        return rune.Equals(NewlineLiteral) ||
               rune.Equals(CarriageReturnLiteral) ||
               rune.Equals(VerticalTabLiteral) ||
               rune.Equals(FormFeedLiteral) ||
               rune.Equals(LineSeparatorLiteral) ||
               rune.Equals(ParagraphSeparatorLiteral);
    }

    private static int LastIndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
        {
            return haystack.Length;
        }

        for (var i = haystack.Length - needle.Length; i >= 0; i--)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastFind(PyString value, PyString needle)
    {
        var byteIndex = LastIndexOfBytes(value.Utf8Bytes.Span, needle.Utf8Bytes.Span);
        if (byteIndex < 0)
        {
            return -1;
        }

        if (needle.Length == 0)
        {
            return value.Length;
        }

        return value.ByteIndexToRuneIndex(byteIndex);
    }

    private static PyString SliceRange(PyString value, int start, int end)
    {
        if (start >= end)
        {
            return PyString.Empty;
        }

        var startByte = value.GetByteIndexForRuneBoundary(start);
        var endByte = value.GetByteIndexForRuneBoundary(end);
        return value.SliceByByteRange(startByte, endByte);
    }

    private static int NormalizeBound(object? bound, int length, int defaultValue)
    {
        if (bound is null || ReferenceEquals(bound, PyNone.Instance))
        {
            return defaultValue;
        }

        if (bound is not BigInteger integer)
        {
            throw new InvalidOperationException("slice bounds must be integers or None");
        }

        if (integer < int.MinValue)
        {
            return 0;
        }

        if (integer > int.MaxValue)
        {
            return length;
        }

        var value = (int)integer;
        if (value < 0)
        {
            value += length;
        }

        if (value < 0)
        {
            return 0;
        }

        return value > length ? length : value;
    }

    private static PyString Pad(PyString value, int width, PyString fillChar, PadMode mode)
    {
        if (fillChar.Length != 1)
        {
            throw new InvalidOperationException("The fill character must be exactly one character long");
        }

        var padding = width - value.Length;
        if (padding <= 0)
        {
            return value;
        }

        var left = mode switch
        {
            PadMode.Left => 0,
            PadMode.Right => padding,
            PadMode.Center => padding / 2,
            _ => 0
        };

        var right = padding - left;
        var builder = CreateBuilder(value);
        AppendRepeated(builder, fillChar, left);
        builder.Append(value);
        AppendRepeated(builder, fillChar, right);
        return builder.ToPyString();
    }

    private static void AppendRepeated(Utf8ValueBuilder builder, PyString value, int count)
    {
        for (var i = 0; i < count; i++)
        {
            builder.Append(value);
        }
    }

    private static PyString SliceByByteRange(PyString value, int startByte, int endByte)
    {
        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    private static Rune DecodeSingleRune(PyString rune)
    {
        Rune.DecodeFromUtf8(rune.Utf8Bytes.Span, out var decoded, out _);
        return decoded;
    }

    private static Utf8ValueBuilder CreateBuilder(PyString value, int capacity = 0)
        => value.OwnerMemoryGovernor is null
            ? new Utf8ValueBuilder(capacity)
            : new Utf8ValueBuilder(value.OwnerMemoryGovernor, value.AllocationSpan, capacity);

    private static Utf8ValueBuilder CreateBuilder(MemoryGovernor? governor, LythonSourceSpan? span, int capacity = 0)
        => governor is null ? new Utf8ValueBuilder(capacity) : new Utf8ValueBuilder(governor, span, capacity);

    private static PyString FromString(string text, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? PyString.FromString(text) : PyString.FromString(text, governor, span);

    private static PyString FromUtf8(byte[] utf8, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? PyString.FromUtf8(utf8) : PyString.FromUtf8(utf8, governor, span);

    private static PyString SliceUtf8(PyString value, int startByte, int endByte, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (startByte >= endByte)
        {
            return PyString.Empty;
        }

        return governor is null || ReferenceEquals(governor, value.OwnerMemoryGovernor)
            ? value.SliceByByteRange(startByte, endByte)
            : CopyUtf8Slice(value, startByte, endByte, governor, span);
    }

    private static PyString CopyUtf8Slice(PyString value, int startByte, int endByte, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var length = endByte - startByte;
        governor.EnsureCanReserve(PyString.EstimateApproximateBytes(length), span);
        var copy = value.Utf8Bytes.Span[startByte..endByte].ToArray();
        return FromUtf8(copy, governor, span);
    }

    private static int GetPreviousRuneStart(ReadOnlySpan<byte> source, int endByte)
    {
        var byteIndex = endByte - 1;
        while (byteIndex > 0 && (source[byteIndex] & 0b1100_0000) == 0b1000_0000)
        {
            byteIndex--;
        }

        return byteIndex;
    }

    private static bool TryGetLineBreakByteLength(ReadOnlySpan<byte> source, int byteIndex, out int lineBreakLength)
    {
        var current = source[byteIndex];
        switch (current)
        {
            case (byte)'\n':
            case (byte)'\v':
            case (byte)'\f':
                lineBreakLength = 1;
                return true;
            case (byte)'\r':
                lineBreakLength = byteIndex + 1 < source.Length && source[byteIndex + 1] == (byte)'\n' ? 2 : 1;
                return true;
            case 0xE2 when byteIndex + 2 < source.Length && source[byteIndex + 1] == 0x80 &&
                           (source[byteIndex + 2] == 0xA8 || source[byteIndex + 2] == 0xA9):
                lineBreakLength = 3;
                return true;
            default:
                lineBreakLength = 0;
                return false;
        }
    }

    private enum PadMode
    {
        Left,
        Right,
        Center
    }

    private enum CaseMapping
    {
        Lower,
        Upper,
        Title
    }
}
