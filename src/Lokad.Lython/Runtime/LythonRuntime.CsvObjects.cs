using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class CsvReaderObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
    {
        public CsvReaderObject(PyList rows, int lineNum, MemoryGovernor governor, LythonSourceSpan span)
        {
            Rows = rows;
            LineNum = lineNum;
            _governor = governor;
            _span = span;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;

        public PyList Rows { get; }

        public int LineNum { get; }

        public int Count => Rows.Count;

        public int Length => Rows.Length;

        public object this[int index] => Rows[index];

        public object GetItem(int index) => Rows.GetItem(index);

        public object CreateSlice(IEnumerable<object> items) => new PyList(items, _governor, _span);

        public object GetIndex(int index) => Rows.GetIndex(index);

        public object GetSlice(IEnumerable<int> indices) => Rows.GetSlice(indices);

        public bool IsTruthy() => Rows.IsTruthy();

        public IEnumerable<object> Iterate() => Rows;

        public PyString RenderPython(PyRenderingContext context) => Rows.RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => Rows.RenderInterpolated(context);

        public IEnumerator<object> GetEnumerator() => Rows.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CsvDictReaderObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
    {
        public CsvDictReaderObject(PyList rows, PyString[]? fieldNames, int lineNum, MemoryGovernor governor, LythonSourceSpan span)
        {
            Rows = rows;
            FieldNames = fieldNames;
            LineNum = lineNum;
            _governor = governor;
            _span = span;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;

        public PyList Rows { get; }

        public PyString[]? FieldNames { get; }

        public int LineNum { get; }

        public int Count => Rows.Count;

        public int Length => Rows.Length;

        public object this[int index] => Rows[index];

        public object GetItem(int index) => Rows.GetItem(index);

        public object CreateSlice(IEnumerable<object> items) => new PyList(items, _governor, _span);

        public object GetIndex(int index) => Rows.GetIndex(index);

        public object GetSlice(IEnumerable<int> indices) => Rows.GetSlice(indices);

        public bool IsTruthy() => Rows.IsTruthy();

        public IEnumerable<object> Iterate() => Rows;

        public PyString RenderPython(PyRenderingContext context) => Rows.RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => Rows.RenderInterpolated(context);

        public IEnumerator<object> GetEnumerator() => Rows.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CsvWriterObject
    {
        public CsvWriterObject(CsvOptions options, ExecutionContext.TextFileHandle? file)
        {
            Options = options;
            File = file;
        }

        public readonly CsvOptions Options;
        public readonly ExecutionContext.TextFileHandle? File;
        public readonly List<CsvCell[]> Rows = new();
    }

    internal sealed class CsvDictWriterObject
    {
        public CsvDictWriterObject(CsvWriterObject writer, PyString[] fieldNames, object restValue, CsvExtrasAction extrasAction)
        {
            Writer = writer;
            FieldNames = fieldNames;
            RestValue = restValue;
            ExtrasAction = extrasAction;
        }

        public CsvWriterObject Writer { get; }

        public PyString[] FieldNames { get; }

        public object RestValue { get; }

        public CsvExtrasAction ExtrasAction { get; }
    }

    internal readonly record struct CsvCell(PyString Text, CsvCellKind Kind);

    internal static class CsvReaderMembers
    {
        public static bool TryGetMember(CsvReaderObject reader, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "line_num" => new BigInteger(reader.LineNum),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class CsvDictReaderMembers
    {
        public static bool TryGetMember(CsvDictReaderObject reader, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fieldnames" => reader.FieldNames is null ? PyNone.Instance : new PyList(reader.FieldNames),
                "line_num" => new BigInteger(reader.LineNum),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal sealed class DictKeysView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictKeysView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Keys.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictValuesView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictValuesView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictItemsView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictItemsView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var pair in _dict.Items)
            {
                yield return _dict.OwnerMemoryGovernor is null
                    ? PyTuple.FromOwnedArray([pair.Key, pair.Value])
                    : PyTuple.FromOwnedArray([pair.Key, pair.Value], _dict.OwnerMemoryGovernor, _dict.AllocationSpan);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class CsvWriterMembers
    {
        public static bool TryGetMember(CsvWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writerow" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerow(row) expects one argument.", span);
                    }

                    var cells = ToCsvRow(arguments[0], span, context, out var convertedBytes);
                    return WriteRow(writer, cells, span, context, convertedBytes);
                }, "csv.writerow", ["row"]),
                "writerows" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerows(rows) expects one argument.", span);
                    }

                    var written = 0;
                    foreach (var row in ToSequence(arguments[0], span, context))
                    {
                        WriteRow(writer, ToCsvRow(row, span, context, out var convertedBytes), span, context, convertedBytes);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecutionBudget(span);
                        }
                    }

                    return PyNone.Instance;
                }, "csv.writerows", ["rows"]),
                "getvalue" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.getvalue() expects no arguments.", span);
                    }

                    var estimate = 0L;
                    foreach (var retained in writer.Rows)
                    {
                        estimate += EstimateRowBytes(retained);
                    }

                    using var scratch = context.MemoryGovernor.ReserveTemporary(estimate, span);
                    return RenderCsvDocument(writer.Rows, writer.Options, trailingTerminator: false, span);
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static BigInteger WriteRow(CsvWriterObject writer, CsvCell[] row, LythonSourceSpan span, ExecutionContext context, long convertedBytes)
        {
            if (writer.File is null)
            {
                // In-memory writers retain history for getvalue(); charge the list
                // slot, the row array and the converted values the history keeps.
                writer.Rows.Add(row);
                var historyCharge = 64L + (16L * row.Length) + convertedBytes;
                context.MemoryGovernor.Reserve(historyCharge, span);
                context.MemoryGovernor.Commit(historyCharge);
            }
            // else: file-backed writers stream output and retain nothing. Either
            // way the single-row render below is transient: it is covered by a
            // reservation and dropped after the write.
            using var scratch = context.MemoryGovernor.ReserveTemporary(EstimateRowBytes(row), span);
            var rendered = RenderCsvDocument([row], writer.Options, trailingTerminator: true, span);
            if (writer.File is not null)
            {
                return writer.File.Write(rendered);
            }

            return new BigInteger(rendered.Length);
        }

        private static long EstimateRowBytes(CsvCell[] row)
        {
            // Quoting can at most double a field; the terminator adds a line.
            var bytes = 64L;
            foreach (var cell in row)
            {
                bytes += 2L * PyString.EstimateApproximateBytes(cell.Text.Utf8Bytes.Length);
            }

            return bytes;
        }

        public static CsvCell[] ToCsvRow(object row, LythonSourceSpan span, ExecutionContext context, out long convertedBytes)
        {
            convertedBytes = 0;
            var cells = new List<CsvCell>();
            foreach (var cell in ToSequence(row, span, context))
            {
                cells.Add(cell switch
                {
                    PyNone => new CsvCell(PyString.Empty, CsvCellKind.Text),
                    PyString text => new CsvCell(text, CsvCellKind.Text),
                    BigInteger integer => ConvertedCell(PyString.FromString(integer.ToString()), CsvCellKind.Numeric, ref convertedBytes),
                    bool boolean => ConvertedCell(PyString.FromString(boolean ? "True" : "False"), CsvCellKind.Text, ref convertedBytes),
                    double floating => ConvertedCell(PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating)), CsvCellKind.Numeric, ref convertedBytes),
                    _ => throw new LythonRuntimeException("TypeError", "CSV rows must contain scalar values.", span)
                });
            }

            return [.. cells];
        }

        private static CsvCell ConvertedCell(PyString text, CsvCellKind kind, ref long convertedBytes)
        {
            convertedBytes += PyString.EstimateApproximateBytes(text.Utf8Bytes.Length);
            return new CsvCell(text, kind);
        }

        public static PyString RenderCsvDocument(IReadOnlyList<CsvCell[]> rows, CsvOptions options, bool trailingTerminator, LythonSourceSpan span)
        {
            var builder = new GovernedByteBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i != 0)
                {
                    builder.Append(options.LineTerminator);
                }

                builder.Append(RenderCsvRow(rows[i], options, span));
            }

            if (trailingTerminator && rows.Count > 0)
            {
                builder.Append(options.LineTerminator);
            }

            return builder.ToPyStringAndRelease();
        }

        private static PyString RenderCsvRow(CsvCell[] row, CsvOptions options, LythonSourceSpan span)
        {
            var builder = new GovernedByteBuilder();
            var singleEmptyField = row.Length == 1 && row[0].Text.Length == 0;
            for (var i = 0; i < row.Length; i++)
            {
                if (i != 0)
                {
                    builder.Append(options.Delimiter);
                }

                builder.Append(EscapeCsvField(row[i], options, span, forceQuotes: singleEmptyField));
            }

            return builder.ToPyStringAndRelease();
        }

        private static PyString EscapeCsvField(CsvCell cell, CsvOptions options, LythonSourceSpan span)
            => EscapeCsvField(cell, options, span, false);

        private static PyString EscapeCsvField(CsvCell cell, CsvOptions options, LythonSourceSpan span, bool forceQuotes)
        {
            var field = cell.Text;
            var fieldBytes = field.Utf8Bytes.Span;
            var delimiterBytes = options.Delimiter.Utf8Bytes.Span;
            var quoteBytes = options.QuoteChar is null ? ReadOnlySpan<byte>.Empty : options.QuoteChar.Utf8Bytes.Span;
            var needsQuotes =
                forceQuotes ||
                options.Quoting == CsvQuotingMode.All ||
                options.Quoting == CsvQuotingMode.NonNumeric && cell.Kind != CsvCellKind.Numeric ||
                options.Quoting == CsvQuotingMode.Minimal && (
                IndexOfBytes(fieldBytes, delimiterBytes) >= 0 ||
                fieldBytes.IndexOf((byte)'\n') >= 0 ||
                fieldBytes.IndexOf((byte)'\r') >= 0 ||
                (options.QuoteChar is not null && IndexOfBytes(fieldBytes, quoteBytes) >= 0));

            if (options.Quoting == CsvQuotingMode.None)
            {
                if (forceQuotes)
                {
                    throw CsvError("single empty field record must be quoted", span);
                }

                return EscapeUnquotedField(field, options, span);
            }

            if (!needsQuotes || options.QuoteChar is null)
            {
                return field;
            }

            var builder = new GovernedByteBuilder(fieldBytes.Length + 2);
            builder.Append(options.QuoteChar);
            for (var i = 0; i < fieldBytes.Length; i++)
            {
                if (MatchesAt(fieldBytes, i, quoteBytes))
                {
                    if (options.DoubleQuote)
                    {
                        builder.Append(options.QuoteChar);
                    }
                    else if (options.EscapeChar is not null)
                    {
                        builder.Append(options.EscapeChar);
                    }
                    else
                    {
                        throw CsvError("need to escape, but no escapechar set", span);
                    }
                }

                builder.Append(fieldBytes[i]);
            }

            builder.Append(options.QuoteChar);
            return builder.ToPyStringAndRelease();
        }

        private static PyString EscapeUnquotedField(PyString field, CsvOptions options, LythonSourceSpan span)
        {
            var fieldBytes = field.Utf8Bytes.Span;
            var delimiterBytes = options.Delimiter.Utf8Bytes.Span;
            var quoteBytes = options.QuoteChar is null ? ReadOnlySpan<byte>.Empty : options.QuoteChar.Utf8Bytes.Span;
            var builder = new GovernedByteBuilder(fieldBytes.Length);
            for (var i = 0; i < fieldBytes.Length; i++)
            {
                var needsEscape =
                    MatchesAt(fieldBytes, i, delimiterBytes) ||
                    fieldBytes[i] is (byte)'\n' or (byte)'\r' ||
                    (options.QuoteChar is not null && MatchesAt(fieldBytes, i, quoteBytes));
                if (needsEscape)
                {
                    if (options.EscapeChar is null)
                    {
                        throw CsvError("need to escape, but no escapechar set", span);
                    }

                    builder.Append(options.EscapeChar);
                }

                builder.Append(fieldBytes[i]);
            }

            return builder.ToPyStringAndRelease();
        }

        private static bool MatchesAt(ReadOnlySpan<byte> text, int index, ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty || index + value.Length > text.Length)
            {
                return false;
            }

            return text.Slice(index, value.Length).SequenceEqual(value);
        }
    }

    internal static class CsvDictWriterMembers
    {
        public static bool TryGetMember(CsvDictWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writeheader" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writeheader() expects no arguments.", span);
                    }

                    var row = new PyDict();
                    foreach (var fieldName in writer.FieldNames)
                    {
                        row.SetItem(fieldName, fieldName);
                    }

                    return CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, row, span, context, out var convertedBytes), span, context, convertedBytes);
                }),
                "writerow" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerow(rowdict) expects one argument.", span);
                    }

                    return CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, arguments[0], span, context, out var convertedBytes), span, context, convertedBytes);
                }, "csv.DictWriter.writerow", ["rowdict"]),
                "writerows" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerows(rowdicts) expects one argument.", span);
                    }

                    var written = 0;
                    foreach (var row in ToSequence(arguments[0], span, context))
                    {
                        CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, row, span, context, out var convertedBytes), span, context, convertedBytes);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecutionBudget(span);
                        }
                    }

                    return PyNone.Instance;
                }, "csv.DictWriter.writerows", ["rowdicts"]),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static CsvCell[] ToDictCsvRow(CsvDictWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context, out long convertedBytes)
        {
            if (row is not PyDict dict)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter rows must be dictionaries.", span);
            }

            var known = new HashSet<object>(writer.FieldNames, PyValueComparer.Instance);
            foreach (var key in dict.Keys)
            {
                if (!known.Contains(key))
                {
                    if (writer.ExtrasAction == CsvExtrasAction.Ignore)
                    {
                        continue;
                    }

                    throw CsvError("dict contains fields not in fieldnames", span);
                }
            }

            var cells = new List<object>(writer.FieldNames.Length);
            foreach (var fieldName in writer.FieldNames)
            {
                cells.Add(dict.TryGetValue(fieldName, out var value) ? value : writer.RestValue);
            }

            return CsvWriterMembers.ToCsvRow(new PyList(cells), span, context, out convertedBytes);
        }
    }

    private static PyString JoinStrings(PyString separator, IEnumerable<PyString> parts, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var builder = new GovernedByteBuilder(governor, span);
        var first = true;
        foreach (var part in parts)
        {
            if (!first)
            {
                builder.Append(separator);
            }

            builder.Append(part);
            first = false;
        }

        return builder.ToPyStringAndRelease();
    }

    private static PyString SliceByByteCount(PyString text, int start, int length)
        => text.SliceByByteRange(start, start + length);

    private static int IndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool MatchesAt(ReadOnlySpan<byte> haystack, int index, ReadOnlySpan<byte> needle)
        => index + needle.Length <= haystack.Length &&
           haystack.Slice(index, needle.Length).SequenceEqual(needle);
}
