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
        public CsvReaderObject(CsvRecordSource records) => Records = records;

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

    internal sealed class CsvDictReaderObject : IPyTruthyValue, IPyAsyncIteratorValue, IPyRenderableValue,
        IPyMutableDynamicAttributes, IPyAsyncDynamicAttributes, IEnumerable<object>
    {
        private object _fieldNames;
        private object _restKey;
        private object _restValue;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        public CsvRecordSource Rows { get; }
        private readonly CsvReaderObject _reader;
        public object LineNum { get; private set; } = BigInteger.Zero;

        public CsvDictReaderObject(CsvRecordSource rows, object fieldNames, object restKey, object restValue,
            ExecutionContext context, LythonSourceSpan span)
        {
            Rows = rows; _fieldNames = fieldNames; _restKey = restKey; _restValue = restValue;
            _context = context; _span = span; _reader = new CsvReaderObject(rows);
        }
        public bool IsTruthy() => true;
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fieldnames" => GetFieldNamesAsync(false).GetAwaiter().GetResult(),
                "restkey" => _restKey, "restval" => _restValue, "line_num" => LineNum, "reader" => _reader,
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance) || IteratorMembers.TryGetMember(this, name, out value);
        }
        public async ValueTask<(bool Found, object Value)> TryGetMemberAsync(string name, ExecutionContext context, LythonSourceSpan span)
            => name == "fieldnames" ? (true, await GetFieldNamesAsync(true).ConfigureAwait(false))
                : TryGetMember(name, out var value) ? (true, value) : (false, PyNone.Instance);
        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "fieldnames": _fieldNames = value; return true;
                case "restkey": _restKey = value; return true;
                case "restval": _restValue = value; return true;
                case "line_num": LineNum = value; return true;
                default: return false;
            }
        }
        private async ValueTask<object> GetFieldNamesAsync(bool asynchronous)
        {
            if (_fieldNames is PyNone)
            {
                var row = asynchronous ? await Rows.TryMoveNextAsync().ConfigureAwait(false) : Rows.TryMoveNext(out var next) ? next : null;
                if (row is not null) _fieldNames = row;
            }
            LineNum = new BigInteger(Rows.PhysicalLineCount);
            return _fieldNames;
        }
        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = NextAsync(false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }
        public ValueTask<PyIterationResult> TryMoveNextAsync() => NextAsync(true);
        private async ValueTask<PyIterationResult> NextAsync(bool asynchronous)
        {
            var zero = asynchronous ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Equal, LineNum, BigInteger.Zero, _context, _span).ConfigureAwait(false)
                : EvaluateBinaryOperator(BinaryOperatorSyntax.Equal, LineNum, BigInteger.Zero, _context, _span);
            if (asynchronous ? await IsTruthyAsync(zero, _context, _span).ConfigureAwait(false) : LythonRuntime.IsTruthy(zero, _context, _span))
                _ = await GetFieldNamesAsync(asynchronous).ConfigureAwait(false);
            var row = asynchronous ? await Rows.TryMoveNextAsync().ConfigureAwait(false) : Rows.TryMoveNext(out var next) ? next : null;
            if (row is null) return PyIterationResult.End;
            LineNum = new BigInteger(Rows.PhysicalLineCount);
            while (row.Count == 0)
            {
                row = asynchronous ? await Rows.TryMoveNextAsync().ConfigureAwait(false) : Rows.TryMoveNext(out next) ? next : null;
                if (row is null) return PyIterationResult.End;
            }
            var fields = await GetFieldNamesAsync(asynchronous).ConfigureAwait(false);
            var result = new PyDict(_context.MemoryGovernor, _span);
            await using (var keys = asynchronous ? await PyIteration.Cursor.CreateAsync(fields, _span, _context).ConfigureAwait(false)
                : PyIteration.Cursor.Create(fields, _span, _context))
            {
                for (var i = 0; ; i++)
                {
                    var key = asynchronous ? await keys.TryMoveNextAsync().ConfigureAwait(false)
                        : keys.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;
                    if (!key.HasValue || i >= row.Count) break;
                    Store(key.Value, row[i]);
                }
            }
            fields = await GetFieldNamesAsync(asynchronous).ConfigureAwait(false);
            var length = (BigInteger)(asynchronous ? await LenAsync([fields], _span, _context).ConfigureAwait(false) : Len([fields], _span, _context));
            if (length < row.Count) Store(_restKey, ReadSliceValue(row, length, null, null, _span, _context));
            else if (length > row.Count)
            {
                var tail = asynchronous ? await ReadSliceValueAsync(fields, new BigInteger(row.Count), null, null, _span, _context).ConfigureAwait(false)
                    : ReadSliceValue(fields, new BigInteger(row.Count), null, null, _span, _context);
                if (asynchronous)
                    await foreach (var key in ToSequenceAsync(tail, _span, _context).ConfigureAwait(false)) Store(key, _restValue);
                else foreach (var key in ToSequence(tail, _span, _context)) Store(key, _restValue);
            }
            return PyIterationResult.Yield(result);
            void Store(object key, object value)
            {
                using var ambient = PyStructuralGuard.PushAmbient(_context, _span);
                result.SetItem(ValidateDictionaryKey(key, _span), value);
            }
        }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<csv.DictReader object>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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

    internal sealed class CsvDictWriterObject : IPyMutableDynamicAttributes
    {
        public CsvDictWriterObject(CsvWriterObject writer, object fieldNames, object restValue, object extrasAction, LythonSourceSpan? span = null)
        {
            writer.MemoryGovernor.Reserve(64L, span); writer.MemoryGovernor.Commit(64L);
            Writer = writer; FieldNames = fieldNames; RestValue = restValue; ExtrasAction = extrasAction;
        }
        public CsvWriterObject Writer { get; }
        public object FieldNames { get; private set; }
        public object RestValue { get; private set; }
        public object ExtrasAction { get; private set; }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fieldnames" => FieldNames, "restval" => RestValue, "extrasaction" => ExtrasAction, "writer" => Writer,
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance) || CsvDictWriterMembers.TryGetMember(this, name, out value);
        }
        public bool TrySetMember(string name, object value)
        {
            switch(name)
            {
                case "fieldnames": FieldNames = value; return true;
                case "restval": RestValue = value; return true;
                case "extrasaction": ExtrasAction = value; return true;
                default: return false;
            }
        }
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

}
