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

    internal sealed class CsvWriterObject : IPyOwnershipSnapshot
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
        public readonly List<CsvCell[]> Rows = new();
        internal MemoryGovernor MemoryGovernor { get; }
        internal long OwnedBytes { get; private set; } = 128L;

        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
            => OwnershipSnapshot.Owned(MemoryGovernor, OwnedBytes, out chargeBytes);

        internal void AppendHistory(CsvCell[] row, long charge, LythonSourceSpan span)
        {
            MemoryGovernor.Reserve(charge, span);
            try { Rows.Add(row); }
            catch { MemoryGovernor.ReleaseReserved(charge); throw; }
            MemoryGovernor.Commit(charge);
            OwnedBytes += charge;
        }
    }

    internal sealed class CsvDictWriterObject : IPyOwnershipSnapshot
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

        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
            => OwnershipSnapshot.Owned(Writer.MemoryGovernor, 64L, out chargeBytes);
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

    internal sealed class DictKeysView : IReadOnlyCollection<object>, IPyOwnershipSnapshot
    {
        private readonly PyDict _dict;

        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
        {
            chargeBytes = OwnershipSnapshot.ViewShellBytes;
            return true;
        }

        public DictKeysView(PyDict dict)
        {
            _dict = dict;
        }

        internal PyDict Source => _dict;

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Keys.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictValuesView : IReadOnlyCollection<object>, IPyOwnershipSnapshot
    {
        private readonly PyDict _dict;

        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
        {
            chargeBytes = OwnershipSnapshot.ViewShellBytes;
            return true;
        }

        public DictValuesView(PyDict dict)
        {
            _dict = dict;
        }

        internal PyDict Source => _dict;

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictItemsView : IReadOnlyCollection<object>, IPyOwnershipSnapshot
    {
        private readonly PyDict _dict;

        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
        {
            chargeBytes = OwnershipSnapshot.ViewShellBytes;
            return true;
        }

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

    internal static class CsvWriterMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature CsvWriterowSignature = LythonCallableSignature.Create("csv.writerow", ["row"]);
        private static readonly LythonCallableSignature CsvWriterowsSignature = LythonCallableSignature.Create("csv.writerows", ["rows"]);
        public static bool TryGetMember(CsvWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writerow" => BoundCallable.Create(
                    (arguments, span, context) => WriteRowFromValue(writer, arguments[0], span, context),
                    CsvWriterowSignature,
                    (arguments, span, context) => WriteRowFromValueAsync(writer, arguments[0], span, context)),
                "writerows" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerows(rows) expects one argument.", span);
                    }

                    var written = 0;
                    foreach (var row in ToSequence(arguments[0], span, context))
                    {
                        WriteRowFromValue(writer, row, span, context);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecution(span);
                        }
                    }

                    return PyNone.Instance;
                }, CsvWriterowsSignature, async (arguments, span, context) =>
                {
                    var written = 0;
                    await foreach (var row in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
                    {
                        var (cells, convertedBytes) = await ToCsvRowAsync(row, span, context).ConfigureAwait(false);
                        await WriteRowAsync(writer, cells, span, context, convertedBytes).ConfigureAwait(false);
                        if ((++written & 63) == 0)
                        {
                            context.CheckExecution(span);
                        }
                    }

                    return PyNone.Instance;
                }),
                "getvalue" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.getvalue() expects no arguments.", span);
                    }

                    // The rendered document escapes as a retained value, so it
                    // commits durable ownership through a governed builder:
                    // repeated retained results accumulate their charges, and
                    // releasing the builder capacity never uncharges them.
                    var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
                    RenderCsvDocumentInto(builder, writer.Rows, writer.Options, trailingTerminator: false, span);
                    return builder.ToPyStringAndRelease();
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Shared by ordinary operand calls and the general binder. The fixed
        // signature validates arity before the bound route reaches this body.
        public static object WriteRowFromValue(CsvWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context)
            => WriteRow(writer, ToCsvRow(row, span, context, out var convertedBytes), span, context, convertedBytes);

        public static async ValueTask<object> WriteRowFromValueAsync(CsvWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context)
        {
            var (cells, convertedBytes) = await ToCsvRowAsync(row, span, context).ConfigureAwait(false);
            return await WriteRowAsync(writer, cells, span, context, convertedBytes).ConfigureAwait(false);
        }

        public static object WriteRow(CsvWriterObject writer, CsvCell[] row, LythonSourceSpan span, ExecutionContext context, long convertedBytes)
        {
            var rendered = PrepareRow(writer, row, span, context, convertedBytes);
            if (writer.WriteMethod is null)
            {
                return new BigInteger(rendered.Length);
            }

            var result = InvokeCallableTarget(writer.WriteMethod, span, span, context,
                () => [CallArgumentValue.Positional(rendered)]);
            return result;
        }

        public static async ValueTask<object> WriteRowAsync(CsvWriterObject writer, CsvCell[] row, LythonSourceSpan span, ExecutionContext context, long convertedBytes)
        {
            var rendered = PrepareRow(writer, row, span, context, convertedBytes);
            if (writer.WriteMethod is null)
            {
                return new BigInteger(rendered.Length);
            }

            var result = await InvokeCallableTargetAsync(writer.WriteMethod, span, span, context,
                () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional(rendered)])).ConfigureAwait(false);
            return result;
        }

        private static PyString PrepareRow(CsvWriterObject writer, CsvCell[] row, LythonSourceSpan span, ExecutionContext context, long convertedBytes)
        {
            if (writer.WriteMethod is null)
            {
                // In-memory writers retain history for getvalue(); charge the list
                // slot, the row array and the converted values the history keeps.
                // The history list doubles its backing array as it grows, so the
                // replacement array is transiently live beside the old one: hold
                // that overlap first, then reserve the row charge, so a failed
                // reservation retains nothing uncharged and leaks no reserve.
                using var growth = writer.MemoryGovernor.ReserveTemporary(PredictHistoryGrowthBytes(writer.Rows), span);
                var historyCharge = 64L + (16L * row.Length) + convertedBytes;
                writer.AppendHistory(row, historyCharge, span);
            }
            // Intermediate field/row renders remain transient. The final string
            // can escape through a guest callback, so commit and track its owner.
            using var scratch = context.MemoryGovernor.ReserveTemporary(EstimateRowBytes(row), span);
            if (writer.WriteMethod is null)
            {
                return RenderCsvDocument([row], writer.Options, trailingTerminator: true, span);
            }

            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                RenderCsvDocumentInto(builder, [row], writer.Options, trailingTerminator: true, span);
                var rendered = builder.ToPyStringAndRelease();
                return rendered;
            }
            finally
            {
                builder.Release();
            }
        }

        private static long PredictHistoryGrowthBytes(List<CsvCell[]> rows)
        {
            if (rows.Count != rows.Capacity)
            {
                return 0;
            }

            // Mirror List<T> doubling (0 -> 4, then x2) with long arithmetic;
            // only the reference slots need cover since the cells themselves
            // ride the per-row history charge.
            var newCapacity = rows.Capacity == 0 ? 4L : Math.Min((long)rows.Capacity * 2L, int.MaxValue);
            return 8L * newCapacity;
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
            // Converted cells materialize CLR strings before the governed copy,
            // so hold a predicted bound transiently: an oversized value trips
            // the budget instead of growing uncharged. Retention stays covered
            // by the convertedBytes history charge on the in-memory path.
            using var conversion = context.MemoryGovernor.ReserveTemporary(0, span);
            foreach (var cell in ToSequence(row, span, context))
            {
                AddCell(cells, cell, conversion, span, context, ref convertedBytes);
            }

            conversion.Grow(16L * cells.Count, span);
            return [.. cells];
        }

        private static async ValueTask<(CsvCell[] Cells, long ConvertedBytes)> ToCsvRowAsync(object row, LythonSourceSpan span, ExecutionContext context)
        {
            // Both single-row and bulk callers share native conversion here,
            // without adding an awaited frame to the bulk publication loop.
            if (row is PyList or PyTuple)
            {
                var nativeCells = ToCsvRow(row, span, context, out var nativeBytes);
                return (nativeCells, nativeBytes);
            }

            var convertedBytes = 0L;
            var cells = new List<CsvCell>();
            using var conversion = context.MemoryGovernor.ReserveTemporary(0, span);
            await foreach (var cell in ToSequenceAsync(row, span, context).ConfigureAwait(false))
            {
                AddCell(cells, cell, conversion, span, context, ref convertedBytes);
            }

            conversion.Grow(16L * cells.Count, span);
            return ([.. cells], convertedBytes);
        }

        private static void AddCell(List<CsvCell> cells, object value, MemoryGovernor.TemporaryMemoryReservation scratch,
            LythonSourceSpan span, ExecutionContext context, ref long convertedBytes)
        {
            if (cells.Count == cells.Capacity)
            {
                // Fund the new backing array before it overlaps the old array.
                var capacity = cells.Capacity == 0 ? 4L : 2L * cells.Capacity;
                scratch.Grow(16L * capacity, span);
            }

            cells.Add(ConvertCsvCell(value, scratch, span, ref convertedBytes));
            context.CheckExecution(span);
        }

        internal static CsvCell ConvertCsvCell(object cell, MemoryGovernor.TemporaryMemoryReservation conversion, LythonSourceSpan span, ref long convertedBytes)
            => cell switch
            {
                PyNone => new CsvCell(PyString.Empty, CsvCellKind.Text),
                PyString text => new CsvCell(text, CsvCellKind.Text),
                BigInteger integer => ConvertedCell(FormatIntegerCell(integer, conversion, span), CsvCellKind.Numeric, ref convertedBytes),
                bool boolean => ConvertedCell(FormatFixedCell(boolean ? "True" : "False", conversion, span), CsvCellKind.Text, ref convertedBytes),
                double floating => ConvertedCell(FormatFixedCell(Numbers.PyNumberOps.RenderFloat(floating), conversion, span), CsvCellKind.Numeric, ref convertedBytes),
                _ => throw new LythonRuntimeException("TypeError", "CSV rows must contain scalar values.", span)
            };

        private static PyString FormatIntegerCell(BigInteger integer, MemoryGovernor.TemporaryMemoryReservation scratch, LythonSourceSpan span)
        {
            // Decimal digits stay below bytes x log10(256); bound with long
            // arithmetic before formatting so huge magnitudes trip first.
            scratch.Grow(128L + ((long)integer.GetByteCount() * 241L / 100L) + 2L, span);
            return PyString.FromString(integer.ToString());
        }

        private static PyString FormatFixedCell(string text, MemoryGovernor.TemporaryMemoryReservation scratch, LythonSourceSpan span)
        {
            // Bools and doubles render to a few dozen characters at most; hold
            // a fixed bound ahead of the CLR string.
            scratch.Grow(128L + 32L, span);
            return PyString.FromString(text);
        }

        private static CsvCell ConvertedCell(PyString text, CsvCellKind kind, ref long convertedBytes)
        {
            convertedBytes += PyString.EstimateApproximateBytes(text.Utf8Bytes.Length);
            return new CsvCell(text, kind);
        }

        public static PyString RenderCsvDocument(IReadOnlyList<CsvCell[]> rows, CsvOptions options, bool trailingTerminator, LythonSourceSpan span)
        {
            var builder = new GovernedByteBuilder();
            RenderCsvDocumentInto(builder, rows, options, trailingTerminator, span);
            return builder.ToPyStringAndRelease();
        }

        // Builder-taking core so escaping renders (getvalue) can commit durable
        // ownership through a governed builder while transient single-row
        // renders keep the ungoverned path above.
        public static void RenderCsvDocumentInto(GovernedByteBuilder builder, IReadOnlyList<CsvCell[]> rows, CsvOptions options, bool trailingTerminator, LythonSourceSpan span)
        {
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

                    return CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, BuildKnownFields(writer), row, span, context, out var convertedBytes), span, context, convertedBytes);
                }, async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writeheader() expects no arguments.", span);
                    }

                    var row = BuildHeaderRow(writer);
                    var cells = ToDictCsvRow(writer, BuildKnownFields(writer), row, span, context, out var convertedBytes);
                    return await CsvWriterMembers.WriteRowAsync(writer.Writer, cells, span, context, convertedBytes).ConfigureAwait(false);
                }),
                "writerow" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerow(rowdict) expects one argument.", span);
                    }

                    return CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, BuildKnownFields(writer), arguments[0], span, context, out var convertedBytes), span, context, convertedBytes);
                }, CsvDictWriterowSignature, async (arguments, span, context) =>
                {
                    var cells = ToDictCsvRow(writer, BuildKnownFields(writer), arguments[0], span, context, out var convertedBytes);
                    return await CsvWriterMembers.WriteRowAsync(writer.Writer, cells, span, context, convertedBytes).ConfigureAwait(false);
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
                        CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, known, row, span, context, out var convertedBytes), span, context, convertedBytes);
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
                        var cells = ToDictCsvRow(writer, known, row, span, context, out var convertedBytes);
                        await CsvWriterMembers.WriteRowAsync(writer.Writer, cells, span, context, convertedBytes).ConfigureAwait(false);
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

        private static CsvCell[] ToDictCsvRow(CsvDictWriterObject writer, HashSet<object> known, object row, LythonSourceSpan span, ExecutionContext context, out long convertedBytes)
        {
            if (row is not PyDict dict)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter rows must be dictionaries.", span);
            }

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

            // The field count is fixed, so fill an exact array instead of
            // draining through a second list; converted cells ride the same
            // pre-format reservation as plain rows.
            convertedBytes = 0;
            using var conversion = context.MemoryGovernor.ReserveTemporary(16L * writer.FieldNames.Length, span);
            var array = new CsvCell[writer.FieldNames.Length];
            for (var i = 0; i < writer.FieldNames.Length; i++)
            {
                array[i] = CsvWriterMembers.ConvertCsvCell(
                    dict.TryGetValue(writer.FieldNames[i], out var value) ? value : writer.RestValue,
                    conversion,
                    span,
                    ref convertedBytes);
            }

            return array;
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
