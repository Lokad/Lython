using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Readers are Python iterators: every access drives the same record source,
    // and a caught pull error does not permanently fault the reader object.
    internal sealed class CsvReaderObject : IPyTruthyValue, IPyAsyncIteratorValue, IPyRenderableValue, IEnumerable<object>
    {
        public CsvReaderObject(CsvRecordSource records, MemoryGovernor governor, LythonSourceSpan span)
        {
            Records = records;
            _governor = governor;
            _span = span;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;

        public CsvRecordSource Records { get; }

        public int LineNum => Records.PhysicalLineCount;

        // Readers stream single-pass: materialize with list(reader) for
        // indexing, slicing or a length. Truth testing and rendering never
        // pull input.
        public bool IsTruthy() => true;

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (Records.TryMoveNext(out var row))
            {
                value = row;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            var row = await Records.TryMoveNextAsync().ConfigureAwait(false);
            return row is null ? PyIterationResult.End : PyIterationResult.Yield(row);
        }

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<csv.reader object>");

        public PyString RenderInterpolated(PyRenderingContext context) => PyString.FromString("<csv.reader object>");

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CsvDictReaderObject : IPyTruthyValue, IPyAsyncIteratorValue, IPyRenderableValue, IEnumerable<object>
    {
        public CsvDictReaderObject(
            CsvRecordSource rowLists,
            PyString[]? fieldNames,
            object restKey,
            object restValue,
            MemoryGovernor governor,
            LythonSourceSpan span)
        {
            Rows = rowLists;
            FieldNames = fieldNames;
            _restKey = restKey;
            _restValue = restValue;
            _governor = governor;
            _span = span;
            LineNum = rowLists.PhysicalLineCount;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;
        private readonly object _restKey;
        private readonly object _restValue;

        public CsvRecordSource Rows { get; }

        public PyString[]? FieldNames { get; }

        public int LineNum { get; private set; }

        // Readers stream single-pass: materialize with list(reader) for
        // indexing, slicing or a length. Truth testing and rendering never
        // pull input.
        public bool IsTruthy() => true;

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (LineNum == 0) LineNum = Rows.PhysicalLineCount;
            if (Rows.TryMoveNext(out var row))
            {
                LineNum = Rows.PhysicalLineCount;
                while (row.Count == 0)
                {
                    if (!Rows.TryMoveNext(out row))
                    {
                        value = PyNone.Instance;
                        return false;
                    }
                }
                value = ConvertRow(row);
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            if (LineNum == 0) LineNum = Rows.PhysicalLineCount;
            var row = await Rows.TryMoveNextAsync().ConfigureAwait(false);
            if (row is null) return PyIterationResult.End;
            LineNum = Rows.PhysicalLineCount;
            while (row.Count == 0)
            {
                row = await Rows.TryMoveNextAsync().ConfigureAwait(false);
                if (row is null) return PyIterationResult.End;
            }
            return PyIterationResult.Yield(ConvertRow(row));
        }

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<csv.DictReader object>");

        public PyString RenderInterpolated(PyRenderingContext context) => PyString.FromString("<csv.DictReader object>");

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public PyList BuildFieldNamesList()
        {
            LineNum = Rows.PhysicalLineCount;
            return new(FieldNames ?? [], _governor, _span);
        }

        private PyDict ConvertRow(PyList row)
        {
            // Publish acquisition before mapping: source errors retain the
            // previous count, while dictionary conversion errors expose this row.
            LineNum = Rows.PhysicalLineCount;
            if (FieldNames is null)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictReader has no field names.", _span);
            }

            var dict = CreateDictReaderRow(row,FieldNames,_restKey,_restValue,_governor,_span);
            return dict;
        }

        private static PyDict CreateDictReaderRow(PyList row,PyString[] fieldNames,object restKey,object restValue,MemoryGovernor governor,LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            var count = Math.Min(row.Count, fieldNames.Length);
            for (var i = 0; i < count; i++)
            {
                dict.SetItem(fieldNames[i], row[i]);
            }

            for (var i = count; i < fieldNames.Length; i++)
            {
                dict.SetItem(fieldNames[i], restValue);
            }

            if (row.Count > fieldNames.Length)
            {
                var extras = new PyList(row.Count - fieldNames.Length, governor, span);
                for (var i = fieldNames.Length; i < row.Count; i++)
                {
                    extras.Add(row[i]);
                }

                dict.SetItem(LythonRuntime.ValidateDictionaryKey(restKey, span), extras);
            }

            return dict;
        }
    }

    internal sealed class CsvWriterObject
    {
        public CsvWriterObject(CsvOptions options, ICallable? writeMethod, MemoryGovernor governor, LythonSourceSpan span)
        {
            // Own the shell beside the governed row history.
            governor.Reserve(128L, span);
            governor.Commit(128L);
            MemoryGovernor = governor;
            Options = options;
            WriteMethod = writeMethod;
        }

        public readonly CsvOptions Options;
        public readonly ICallable? WriteMethod;
        public readonly List<PyString> Rows = new();
        internal MemoryGovernor MemoryGovernor { get; }

        internal void AppendHistory(PyString row, LythonSourceSpan span)
        {
            if (Rows.Count == Rows.Capacity)
            {
                var previous = Rows.Capacity;
                var capacity = previous == 0 ? 4 : checked(previous * 2);
                using var replacement = MemoryGovernor.ReserveTemporary(checked(8L * capacity), span);
                Rows.Capacity = capacity;
                replacement.Commit(8L * capacity);
                MemoryGovernor.Release(8L * previous);
            }
            Rows.Add(row);
        }
    }

    internal sealed class CsvDictWriterObject
    {
        public CsvDictWriterObject(CsvWriterObject writer, PyString[] fieldNames, object restValue, CsvExtrasAction extrasAction, LythonSourceSpan? span = null)
        {
            writer.MemoryGovernor.Reserve(64L, span);
            writer.MemoryGovernor.Commit(64L);
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


    internal static class CsvReaderMembers
    {
        public static bool TryGetMember(CsvReaderObject reader, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "line_num" => new BigInteger(reader.LineNum),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance) || IteratorMembers.TryGetMember(reader, name, out value);
        }
    }

    internal static class CsvDictReaderMembers
    {
        public static bool TryGetMember(CsvDictReaderObject reader, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fieldnames" => reader.FieldNames is null ? PyNone.Instance : reader.BuildFieldNamesList(),
                "line_num" => new BigInteger(reader.LineNum),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance) || IteratorMembers.TryGetMember(reader, name, out value);
        }
    }

    internal sealed class DictKeysView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictKeysView(PyDict dict)
        {
            _dict = dict;
        }

        internal PyDict Source => _dict;

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

        internal PyDict Source => _dict;

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

        internal PyDict Source => _dict;

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator()
            => EnumerateItems(_dict.GetEnumerator()).GetEnumerator();

        private IEnumerable<object> EnumerateItems(IEnumerator<KeyValuePair<object, object>> entries)
        {
            using (entries)
            {
                while (entries.MoveNext())
                {
                    var pair = entries.Current;
                    yield return _dict.OwnerMemoryGovernor is null
                        ? PyTuple.FromOwnedArray([pair.Key, pair.Value])
                        : PyTuple.FromOwnedArray([pair.Key, pair.Value], _dict.OwnerMemoryGovernor, _dict.AllocationSpan);
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class CsvDictWriterMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature CsvDictWriterowSignature = LythonCallableSignature.Create("csv.DictWriter.writerow", ["rowdict"]);
        private static readonly LythonCallableSignature CsvDictWriterowsSignature = LythonCallableSignature.Create("csv.DictWriter.writerows", ["rowdicts"]);
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

                    var row = BuildHeaderRow(writer);

                    return CsvWriterMembers.WriteRowFromValue(writer.Writer, ToDictCsvRow(writer, BuildKnownFields(writer), row, span, context), span, context);
                }, async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writeheader() expects no arguments.", span);
                    }

                    var row = BuildHeaderRow(writer);
                    var cells = ToDictCsvRow(writer, BuildKnownFields(writer), row, span, context);
                    return await CsvWriterMembers.WriteRowFromValueAsync(writer.Writer, cells, span, context).ConfigureAwait(false);
                }),
                "writerow" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerow(rowdict) expects one argument.", span);
                    }

                    return CsvWriterMembers.WriteRowFromValue(writer.Writer, ToDictCsvRow(writer, BuildKnownFields(writer), arguments[0], span, context), span, context);
                }, CsvDictWriterowSignature, async (arguments, span, context) =>
                {
                    var cells = ToDictCsvRow(writer, BuildKnownFields(writer), arguments[0], span, context);
                    return await CsvWriterMembers.WriteRowFromValueAsync(writer.Writer, cells, span, context).ConfigureAwait(false);
                }),
                "writerows" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerows(rowdicts) expects one argument.", span);
                    }

                    var written = 0;
                    var known = BuildKnownFields(writer);
                    foreach (var row in ToSequence(arguments[0], span, context))
                    {
                        CsvWriterMembers.WriteRowFromValue(writer.Writer, ToDictCsvRow(writer, known, row, span, context), span, context);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecution(span);
                        }
                    }

                    return PyNone.Instance;
                }, CsvDictWriterowsSignature, async (arguments, span, context) =>
                {
                    var written = 0;
                    var known = BuildKnownFields(writer);
                    await foreach (var row in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
                    {
                        var cells = ToDictCsvRow(writer, known, row, span, context);
                        await CsvWriterMembers.WriteRowFromValueAsync(writer.Writer, cells, span, context).ConfigureAwait(false);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecution(span);
                        }
                    }

                    return PyNone.Instance;
                }),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static HashSet<object> BuildKnownFields(CsvDictWriterObject writer)
            => new(writer.FieldNames, PyValueComparer.Instance);

        private static PyDict BuildHeaderRow(CsvDictWriterObject writer)
        {
            var row = new PyDict();
            foreach (var fieldName in writer.FieldNames)
            {
                row.SetItem(fieldName, fieldName);
            }

            return row;
        }

        private static IEnumerable<object> ToDictCsvRow(CsvDictWriterObject writer, HashSet<object> known,
            object row, LythonSourceSpan span, ExecutionContext context)
        {
            if (row is not PyDict dict) throw RuntimeErrors.Type("csv.DictWriter rows must be dictionaries.", span);
            foreach (var key in dict.Keys)
                if (!known.Contains(key) && writer.ExtrasAction != CsvExtrasAction.Ignore)
                    throw CsvError("dict contains fields not in fieldnames", span);
            foreach (var name in writer.FieldNames)
            {
                context.CheckExecution(span);
                yield return dict.TryGetValue(name, out var value) ? value : writer.RestValue;
            }
        }
    }

    private static PyString JoinStrings(PyString separator, IEnumerable<PyString> parts, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var builder = new GovernedByteBuilder(governor, span);
        try
        {
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

            return builder.ToPyString();
        }
        finally
        {
            builder.Release();
        }
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
