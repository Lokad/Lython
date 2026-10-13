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
    internal enum CsvQuotingMode
    {
        Minimal = 0,
        All = 1,
        NonNumeric = 2,
        None = 3,
    }

    internal enum CsvExtrasAction
    {
        Raise,
        Ignore,
    }

    internal enum CsvCellKind
    {
        Text,
        Numeric,
    }

    private sealed class CsvModule : PyModule
    {
        public static readonly CsvModule Instance = new();

        private CsvModule() : base("csv")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "reader" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvReader, Reader),
                "writer" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvWriter, Writer, WriterAsync),
                "DictReader" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictReader, DictReader, DictReaderAsync),
                "DictWriter" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictWriter, DictWriter, DictWriterAsync),
                "Error" => new ExceptionTypeValue(ModuleException("csv", "Error")),
                "QUOTE_MINIMAL" => new BigInteger((int)CsvQuotingMode.Minimal),
                "QUOTE_ALL" => new BigInteger((int)CsvQuotingMode.All),
                "QUOTE_NONNUMERIC" => new BigInteger((int)CsvQuotingMode.NonNumeric),
                "QUOTE_NONE" => new BigInteger((int)CsvQuotingMode.None),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Reader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            var records = new CsvRecordSource(arguments[0], options, span, context);
            return new CsvReaderObject(records, context.MemoryGovernor, span);
        }

        private object DictReader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictReader(f[, fieldnames][, restkey][, restval][, ...]) expects at least one argument.", span);
            }

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            var records = new CsvRecordSource(arguments[0], options, span, context);
            PyString[]? fieldNames;
            if (arguments.Length > 1 && arguments[1] is not PyNone)
            {
                fieldNames = ToCsvFieldNames(arguments[1], "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", span, context);
                // Validate (and surface malformed input) exactly like the
                // historical eager first pull, then replay the record so
                // iteration still yields every row.
                if (records.TryMoveNext(out var first))
                {
                    records.PushBack(first);
                }
            }
            else
            {
                var header = records.PullHeader();
                fieldNames = header is null
                    ? null
                    : ToFieldNameList(header, span, context);
            }

            // Rows parse on demand and dictionaries convert on demand, so early
            // termination never pays for unconsumed rows or dictionaries.
            return new CsvDictReaderObject(
                records,
                fieldNames,
                RestKey(arguments, 2),
                RestValue(arguments, 3),
                context.MemoryGovernor,
                span);
        }

        // Asynchronous twin: header inference and first-row validation pull
        // through the async host boundary; everything else stays shared.
        // Plain reader construction pulls nothing, so it needs no twin.
        private async ValueTask<object> DictReaderAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictReader(f[, fieldnames][, restkey][, restval][, ...]) expects at least one argument.", span);
            }

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            var records = new CsvRecordSource(arguments[0], options, span, context);
            PyString[]? fieldNames;
            if (arguments.Length > 1 && arguments[1] is not PyNone)
            {
                fieldNames = ToCsvFieldNames(arguments[1], "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", span, context);
                // Validate (and surface malformed input) exactly like the
                // historical eager first pull, then replay the record so
                // iteration still yields every row.
                var first = await records.TryMoveNextAsync().ConfigureAwait(false);
                if (first is not null)
                {
                    records.PushBack(first);
                }
            }
            else
            {
                var header = await records.PullHeaderAsync().ConfigureAwait(false);
                fieldNames = header is null
                    ? null
                    : ToFieldNameList(header, span, context);
            }

            // Rows parse on demand and dictionaries convert on demand, so early
            // termination never pays for unconsumed rows or dictionaries.
            return new CsvDictReaderObject(
                records,
                fieldNames,
                RestKey(arguments, 2),
                RestValue(arguments, 3),
                context.MemoryGovernor,
                span);
        }

        private object Writer(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);

            var write = arguments.Length == 0 || arguments[0] is PyNone
                ? null : ResolveWrite(arguments[0], span, context);
            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            return CreateWriter(options, write, span, context);
        }

        private async ValueTask<object> WriterAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var write = arguments.Length == 0 || arguments[0] is PyNone
                ? null : await ResolveWriteAsync(arguments[0], span, context).ConfigureAwait(false);
            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            return CreateWriter(options, write, span, context);
        }

        private static CsvWriterObject CreateWriter(CsvOptions options, ICallable? write, LythonSourceSpan span, ExecutionContext context)
        {
            var writer = new CsvWriterObject(options, write, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(writer, writer.OwnedBytes, span);
            return writer;
        }

        private object DictWriter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length < 2)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(fileobj, fieldnames, ...) expects a text writer and field names.", span);
            }

            var fieldNames = ToCsvFieldNames(arguments[1], "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", span, context);
            var extrasAction = GetExtrasAction(arguments, 3, span);
            return CreateDictWriter(arguments, ResolveWrite(arguments[0], span, context), fieldNames, extrasAction, span, context);
        }

        private async ValueTask<object> DictWriterAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length < 2)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(fileobj, fieldnames, ...) expects a text writer and field names.", span);
            }

            var fieldNames = await ToCsvFieldNamesAsync(arguments[1], "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", span, context).ConfigureAwait(false);
            var extrasAction = GetExtrasAction(arguments, 3, span);
            var write = await ResolveWriteAsync(arguments[0], span, context).ConfigureAwait(false);
            return CreateDictWriter(arguments, write, fieldNames, extrasAction, span, context);
        }

        private static object CreateDictWriter(object[] arguments, ICallable write, PyString[] fieldNames,
            CsvExtrasAction extrasAction, LythonSourceSpan span, ExecutionContext context)
        {
            var restVal = arguments.Length > 2 && arguments[2] is not PyNone ? arguments[2] : PyString.Empty;
            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            var writer = CreateWriter(options, write, span, context);
            var result = new CsvDictWriterObject(writer, fieldNames, restVal, extrasAction, span);
            context.State.CallTemporaries.TrackFreshMutable(result, 64L, span);
            return result;
        }

        // CPython captures write once at construction. Ordinary attribute lookup
        // can execute a descriptor; callability itself is a type-slot check.
        private static ICallable ResolveWrite(object stream, LythonSourceSpan span, ExecutionContext context)
        {
            try
            {
                return RequireWriteMethod(PyTextStream.ResolveMember(stream, "write", span, context), span);
            }
            catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("AttributeError"), error.Identity))
            {
                throw MissingWriteMethod(span);
            }
        }

        private static async ValueTask<ICallable> ResolveWriteAsync(object stream, LythonSourceSpan span, ExecutionContext context)
        {
            try
            {
                return RequireWriteMethod(await PyTextStream.ResolveMemberAsync(stream, "write", span, context).ConfigureAwait(false), span);
            }
            catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("AttributeError"), error.Identity))
            {
                throw MissingWriteMethod(span);
            }
        }

        private static ICallable RequireWriteMethod(object value, LythonSourceSpan span)
            => value is ICallable callable && IsCallable(value)
                ? callable : throw MissingWriteMethod(span);

        private static LythonRuntimeException MissingWriteMethod(LythonSourceSpan span)
            => new("TypeError", "argument 1 must have a 'write' method", span);

        private static CsvOptions GetOptions(
            object[] arguments,
            CsvOptionArgumentLayout layout,
            LythonSourceSpan span)
        {
            ValidateDialect(arguments, layout.Dialect, span);

            var delimiter = GetCharacterOption(arguments, layout.Delimiter, PyStringOps.CommaLiteral, "delimiter", allowNone: false, span).RequireNotNull();
            if (delimiter.AsString() is "\r" or "\n")
            {
                throw CsvError("csv delimiter cannot be a newline.", span);
            }

            var quotechar = GetCharacterOption(arguments, layout.QuoteCharacter, PyString.FromString("\""), "quotechar", allowNone: true, span);
            var quoting = GetQuoting(arguments, layout.Quoting, span);
            var doublequote = GetBooleanOption(arguments, layout.DoubleQuote, defaultValue: true, "doublequote", span);
            var escapechar = GetCharacterOption(arguments, layout.EscapeCharacter, null, "escapechar", allowNone: true, span);
            var skipinitialspace = GetBooleanOption(arguments, layout.SkipInitialSpace, defaultValue: false, "skipinitialspace", span);
            var lineterminator = GetStringOption(arguments, layout.LineTerminator, PyString.FromString("\n"), "lineterminator", span);
            var strict = GetBooleanOption(arguments, layout.Strict, defaultValue: false, "strict", span);

            if (quoting == CsvQuotingMode.None && escapechar is null)
            {
                // This is legal until escaping is actually required.
            }

            return new CsvOptions(delimiter, quotechar, quoting, doublequote, escapechar, skipinitialspace, lineterminator, strict);
        }

        private static void ValidateDialect(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return;
            }

            if (PyStringOps.TryAsString(arguments[index], out var dialect) &&
                string.Equals(dialect.AsString(), "excel", StringComparison.Ordinal))
            {
                return;
            }

            throw new LythonRuntimeException("TypeError", "csv dialect registry is unsupported; pass explicit CSV options instead.", span);
        }

        private static PyString GetStringOption(object[] arguments, int index, PyString defaultValue, string name, LythonSourceSpan span)
        {
            // Omitted options arrive as PyNone through the binder (which trims
            // only the unassigned suffix), so None always means the default
            // here, matching the character options below.
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var value))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            return value;
        }

        private static PyString? GetCharacterOption(object[] arguments, int index, PyString? defaultValue, string name, bool allowNone, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            return GetRequiredCharacter(arguments[index], name, allowNone, span);
        }

        private static PyString? GetRequiredCharacter(object value, string name, bool allowNone, LythonSourceSpan span)
        {
            if (value is PyNone)
            {
                if (allowNone)
                {
                    return null;
                }

                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (text.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be one character.", span);
            }

            return text;
        }

        private static CsvQuotingMode GetQuoting(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return CsvQuotingMode.Minimal;
            }

            if (!PyNumberOps.TryAsInteger(arguments[index], out var integer) || integer < 0 || integer > (int)CsvQuotingMode.None)
            {
                throw new LythonRuntimeException("TypeError", "csv quoting must be one of the QUOTE_* constants.", span);
            }

            return (CsvQuotingMode)(int)integer;
        }

        private static bool GetBooleanOption(object[] arguments, int index, bool defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (arguments[index] is not bool value)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a bool.", span);
            }

            return value;
        }

        private static PyString[] ToCsvFieldNames(object value, string message, LythonSourceSpan span, ExecutionContext context)
        {
            // The drain list doubles geometrically beside the retained array, so
            // the drain rides a transient reservation while the final array commits
            // at the slot rate; names themselves stay aliased to existing owners.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var names = new List<PyString>();
            var chargedCapacity = 0;
            foreach (var item in ToSequence(value, span, context))
            {
                AddCsvFieldName(names, item, scratch, ref chargedCapacity, message, span);
            }

            return FinishCsvFieldNames(names, context, span);
        }

        private static async ValueTask<PyString[]> ToCsvFieldNamesAsync(object value, string message, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var names = new List<PyString>();
            var chargedCapacity = 0;
            await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
            {
                AddCsvFieldName(names, item, scratch, ref chargedCapacity, message, span);
            }

            return FinishCsvFieldNames(names, context, span);
        }

        private static void AddCsvFieldName(List<PyString> names, object item, MemoryGovernor.TemporaryMemoryReservation scratch,
            ref int chargedCapacity, string message, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(item, out var name))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            if (names.Count == names.Capacity)
            {
                var predicted = names.Capacity == 0 ? 4L : (long)names.Capacity * 2L;
                scratch.Grow(checked(16L * (predicted - chargedCapacity)), span);
            }

            names.Add(name);
            if (names.Capacity > chargedCapacity)
            {
                scratch.Grow(checked(16L * (names.Capacity - chargedCapacity)), span);
                chargedCapacity = names.Capacity;
            }
        }

        private static PyString[] FinishCsvFieldNames(List<PyString> names, ExecutionContext context, LythonSourceSpan span)
        {
            var governor = context.MemoryGovernor;
            var owned = checked(32L + 16L * names.Count);
            governor.EnsureCanReserve(owned, span);
            // An empty table also needs a distinct storage owner: ToArray()
            // would return a shared singleton that never becomes unreachable.
            var result = names.Count == 0 ? new PyString[0] : names.ToArray();
            governor.Reserve(owned, span);
            governor.Commit(owned);
            context.State.CallTemporaries.TrackFreshMutable(result, owned, span);
            return result;
        }

        private static PyString[] ToFieldNameList(PyList row, LythonSourceSpan span, ExecutionContext context)
        {
            // The header array is retained by the reader; names stay aliased.
            var owned = checked(32L + 16L * row.Count);
            context.MemoryGovernor.Reserve(owned, span);
            context.MemoryGovernor.Commit(owned);
            var names = new PyString[row.Count];
            context.State.CallTemporaries.TrackFreshMutable(names, owned, span);
            for (var i = 0; i < row.Count; i++)
            {
                if (!PyStringOps.TryAsString(row[i], out var name))
                {
                    throw CsvError("csv.DictReader header row must contain strings.", span);
                }

                names[i] = name;
            }

            return names;
        }

        private static object RestKey(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static object RestValue(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static CsvExtrasAction GetExtrasAction(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return CsvExtrasAction.Raise;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var action))
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(..., extrasaction=...) expects a string.", span);
            }

            var text = action.AsString();
            if (!string.Equals(text, "raise", StringComparison.Ordinal) &&
                !string.Equals(text, "ignore", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("ValueError", "extrasaction must be 'raise' or 'ignore'.", span);
            }

            return text == "ignore" ? CsvExtrasAction.Ignore : CsvExtrasAction.Raise;
        }

    }

    // Records parse one physical line at a time and stream through a shared
    // cursor: parsed rows are yielded once and never retained (except the
    // DictReader header row), so a full scan only carries the current record.
    // Field/row payloads use the shared reclamation pool. Source/parser shell
    // and actual scratch capacities have one separate source snapshot, so
    // dropping that source never refunds an escaping field or row.
    internal sealed class CsvRecordSource : IPyOwnershipSnapshot, IMemoryStorageEdges
    {
        void IMemoryStorageEdges.VisitStorageChildren(MemoryStorageWalk visitor)
        {
            visitor.Add(_origin); visitor.Add(_cursor); visitor.Add(_asyncCursor);
            visitor.Add(_indexed); visitor.Add(_pushedBack); visitor.Add(_context); visitor.Add(_parser);
        }
        private readonly CsvRecordParser _parser;
        private IEnumerator<object>? _cursor;
        private object? _origin;
        private IAsyncEnumerator<object>? _asyncCursor;
        private PyList? _indexed;
        private int _position;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private PyList? _pushedBack;
        private bool _completed;

        // Source/parser objects, builders and list shells; backing is charged
        // from actual capacities, independently of emitted values.
        internal const long ShellBytes = 512;
        internal long OwnedBytes => ShellBytes + _parser.StorageBytes;
        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes)
            => OwnershipSnapshot.Owned(_context.MemoryGovernor, OwnedBytes, out bytes);
        internal bool Completed => _completed;
        internal void StorageChanged() => ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);

        public CsvRecordSource(object source, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
        {
            _context = context;
            _span = span;
            _origin = source;
            // Lists read live by index below, so validation is the type test;
            // every other source validates by enumerating now. Element strings
            // are checked as each line is pulled either way. The asynchronous
            // cursor materializes lazily on first async use so synchronous runs
            // never pay for it; sharing one origin across modes interleaves
            // positions exactly like two readers over one file in CPython.
            const long initialBytes = ShellBytes + 32; // initial 16-char builder
            context.MemoryGovernor.Reserve(initialBytes, span);
            try
            {
                if (source is PyList list)
                {
                    _indexed = list;
                }
                else
                {
                    _cursor = ToSequence(source, span, context).GetEnumerator();
                }
                _parser = new CsvRecordParser(options, context, span, this);
                context.MemoryGovernor.Commit(initialBytes);
            }
            catch
            {
                context.MemoryGovernor.ReleaseReserved(initialBytes);
                _cursor?.Dispose();
                throw;
            }
            try
            {
                context.State.CallTemporaries.TrackFreshMutable(this, OwnedBytes, span);
            }
            catch
            {
                _cursor?.Dispose();
                throw;
            }
        }

        public int PhysicalLineCount { get; private set; }

        internal ChargeReclamationPool Pool => _context.State.CallTemporaries;

        // The returned record stays live during conversion. The resulting
        // field-name table retains its strings; no extra header holder is needed.
        public PyList? PullHeader()
        {
            if (!TryMoveNext(out var row))
            {
                return null;
            }

            return row;
        }

        // Replays one pulled record (the validated first data row when explicit
        // field names were supplied) so iteration still yields every row.
        public void PushBack(PyList row)
        {
            _pushedBack = row;
        }

        // Advances the shared cursor; every iterator over this source observes
        // the same position, matching CPython (a second pass sees nothing new).
        public bool TryMoveNext(out PyList row)
        {
            if (_pushedBack is not null)
            {
                row = _pushedBack;
                _pushedBack = null;
                return true;
            }

            while (true)
            {
                if (_parser.TryTakeReady(out row))
                {
                    return true;
                }

                if (_completed)
                {
                    row = null!;
                    return false;
                }

                Pull();
            }
        }

        // Asynchronous twin: lines arrive through the async host boundary so
        // genuinely-delayed hosts suspend per pull. Parsing, charging and
        // reclamation stay shared and synchronous; only acquisition awaits. A
        // null row marks exhaustion; parsed rows are never null.
        public async ValueTask<PyList?> TryMoveNextAsync()
        {
            if (_pushedBack is not null)
            {
                var replay = _pushedBack;
                _pushedBack = null;
                return replay;
            }

            while (true)
            {
                if (_parser.TryTakeReady(out var row))
                {
                    return row;
                }

                if (_completed)
                {
                    return null;
                }

                await PullAsync().ConfigureAwait(false);
            }
        }

        public ValueTask<PyList?> PullHeaderAsync() => TryMoveNextAsync();

        private void Pull()
        {
            object current;
            if (_indexed is not null)
            {
                // Lists read live by index, so appends, replacements, removals
                // and clears all behave like CPython instead of detaching on
                // wholesale storage replacement.
                if (_position >= _indexed.Count)
                {
                    FinishExhausted();
                    return;
                }

                current = _indexed[_position];
                _position++;
            }
            else
            {
                bool moved;
                try
                {
                    moved = _cursor!.MoveNext();
                }
                catch (InvalidOperationException)
                {
                    // Version-checked iterators (large-list storage, views)
                    // surface concurrent modification as a CLR failure; fail
                    // explicitly instead of leaking it.
                    throw new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
                }

                if (!moved)
                {
                    FinishExhausted();
                    return;
                }

                current = _cursor.Current;
            }

            _context.CheckExecution(_span);
            if (!PyStringOps.TryAsString(current, out var line))
            {
                throw new LythonRuntimeException("TypeError", "csv.reader(csvfile) expects an iterable of strings.", _span);
            }

            PhysicalLineCount++;
            try
            {
                _parser.Feed(line.AsString());
            }
            catch
            {
                // The failed line is consumed either way; drop its partial
                // record so the next pull starts clean instead of poisoning
                // every later row. (A non-string element throws before any
                // feed, leaving legitimately pending multiline state alone.)
                _parser.ResetRecord();
                throw;
            }

        }

        private async ValueTask PullAsync()
        {
            if (_indexed is not null)
            {
                // Indexed lists never touch the host, so the async pull shares
                // the live-index read exactly, including advancing past the
                // element before feeding it: the failed line is consumed
                // either way so catching a mid-stream error and continuing
                // resumes at the following element.
                if (_position >= _indexed.Count)
                {
                    await FinishExhaustedAsync().ConfigureAwait(false);
                    return;
                }

                var current = _indexed[_position];
                _position++;
                FeedPulledLine(current);
                return;
            }

            _asyncCursor ??= ToSequenceAsync(_origin!, _span, _context).GetAsyncEnumerator();
            bool moved;
            try
            {
                moved = await _asyncCursor.MoveNextAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Version-checked iterators (large-list storage, views)
                // surface concurrent modification as a CLR failure; fail
                // explicitly instead of leaking it.
                throw new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
            }

            if (!moved)
            {
                await FinishExhaustedAsync().ConfigureAwait(false);
                return;
            }

            FeedPulledLine(_asyncCursor.Current);
        }

        private void FeedPulledLine(object current)
        {
            // Parsing, charging and reclamation are synchronous; only line
            // acquisition awaits, so the shared parser sees an identical call
            // sequence either way.
            _context.CheckExecution(_span);
            if (!PyStringOps.TryAsString(current, out var line))
            {
                throw new LythonRuntimeException("TypeError", "csv.reader(csvfile) expects an iterable of strings.", _span);
            }

            PhysicalLineCount++;
            try
            {
                _parser.Feed(line.AsString());
            }
            catch
            {
                // The failed line is consumed either way; drop its partial
                // record so the next pull starts clean instead of poisoning
                // every later row. (A non-string element throws before any
                // feed, leaving legitimately pending multiline state alone.)
                _parser.ResetRecord();
                throw;
            }

        }

        private void FinishExhausted()
            => FinishExhaustedAsync().GetAwaiter().GetResult();

        private async ValueTask FinishExhaustedAsync()
        {
            _completed = true;
            try
            {
                _parser.Finish();
            }
            catch
            {
                _parser.ResetRecord();
                throw;
            }
            finally
            {
                // Keep the producer alive through disposal, then detach it even
                // on cleanup failure. Queued/replayed records stay independently
                // funded. Body errors never reach this terminal path.
                try
                {
                    _cursor?.Dispose();
                }
                finally
                {
                    try
                    {
                        if (_asyncCursor is not null)
                            await _asyncCursor.DisposeAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        _cursor = null;
                        _asyncCursor = null;
                        _origin = null;
                        _indexed = null;
                        _parser.ReleaseScratch();
                    }
                }
            }
        }
    }

    internal sealed class CsvRecordParser
    {
        private readonly CsvOptions _options;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly string _delimiter;
        private readonly string? _quoteCharacter;
        private readonly string? _escapeCharacter;
        private readonly List<object> _row = new();
        private readonly StringBuilder _field = new(16);
        private readonly CsvRecordSource _owner;
        private readonly List<PyList> _ready = new();
        private int _readyHead;
        private bool _inQuotes;
        private bool _fieldStarted;
        private bool _afterQuote;
        private bool _recordStarted;

        internal long StorageBytes => checked(16L * _row.Capacity + 2L * _field.Capacity + 16L * _ready.Capacity);

        public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span, CsvRecordSource owner)
        {
            _options = options;
            _context = context;
            _span = span;
            _owner = owner;
            _delimiter = options.Delimiter.AsString();
            _quoteCharacter = options.QuoteChar?.AsString();
            _escapeCharacter = options.EscapeChar?.AsString();
        }

        // Takes the next completed record, if any. Records completed by one
        // Feed call queue here until the source drains them.
        public bool TryTakeReady(out PyList row)
        {
            if (_readyHead >= _ready.Count)
            {
                if (_owner.Completed) ReleaseReadyStorage();
                row = null!;
                return false;
            }

            row = _ready[_readyHead];
            _ready[_readyHead] = null!;
            _readyHead++;
            if (_readyHead > 1024 && _readyHead >= _ready.Count / 2)
            {
                _ready.RemoveRange(0, _readyHead);
                _readyHead = 0;
            }

            if (_owner.Completed && _readyHead >= _ready.Count) ReleaseReadyStorage();

            return true;
        }

        public void Feed(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (_inQuotes)
                {
                    if (TryConsumeEscape(text, ref i))
                    {
                        continue;
                    }

                    if (MatchesQuoteAt(text, i))
                    {
                        var quoteCharacter = _quoteCharacter.RequireNotNull();
                        if (_options.DoubleQuote && MatchesCsvAt(text, i + quoteCharacter.Length, quoteCharacter))
                        {
                            // A doubled quote denotes one literal quote, including for a
                            // non-BMP Python character represented by two UTF-16 code units.
                            AppendField(quoteCharacter);
                            i += (2 * quoteCharacter.Length) - 1;
                        }
                        else
                        {
                            _inQuotes = false;
                            _afterQuote = true;
                            i += quoteCharacter.Length - 1;
                        }

                        continue;
                    }

                    AppendField(c);
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    FinishRecord();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    continue;
                }

                if (MatchesCsvAt(text, i, _delimiter))
                {
                    FinishField();
                    i += _delimiter.Length - 1;
                    _recordStarted = true;
                    _afterQuote = false;
                    continue;
                }

                if (_options.SkipInitialSpace && !_fieldStarted && _field.Length == 0 && c == ' ')
                {
                    continue;
                }

                if (MatchesQuoteAt(text, i) && !_fieldStarted)
                {
                    _inQuotes = true;
                    _fieldStarted = true;
                    _recordStarted = true;
                    i += _quoteCharacter.RequireNotNull().Length - 1;
                    continue;
                }

                if (_afterQuote)
                {
                    // Once a quoted field closes, only a delimiter or physical line ending
                    // may follow it; accepting ordinary text here would hide malformed CSV.
                    throw CsvError("Invalid csv input.", _span);
                }

                if (TryConsumeEscape(text, ref i))
                {
                    continue;
                }

                _fieldStarted = true;
                _recordStarted = true;
                AppendField(c);
            }

            if (_inQuotes)
            {
                if (text.Length == 0 || (text[^1] != '\n' && text[^1] != '\r'))
                {
                    AppendField('\n');
                }

                return;
            }

            if (text.Length == 0)
            {
                EnqueueRecord(TrackRow(new PyList([], _context.MemoryGovernor, _span)));
                return;
            }

            if (text[^1] != '\n' && text[^1] != '\r')
            {
                FinishRecord();
            }
        }

        public void Finish()
        {
            if (_inQuotes)
            {
                throw CsvError("Invalid csv input.", _span);
            }
        }

        private bool TryConsumeEscape(string text, ref int index)
        {
            if (_escapeCharacter is null || !MatchesCsvAt(text, index, _escapeCharacter))
            {
                return false;
            }

            if (index + _escapeCharacter.Length >= text.Length)
            {
                if (_options.Strict)
                {
                    throw CsvError("Invalid csv input.", _span);
                }

                AppendField(_escapeCharacter);
                index += _escapeCharacter.Length - 1;
                return true;
            }

            index += _escapeCharacter.Length;
            AppendField(text[index]);
            _fieldStarted = true;
            _recordStarted = true;
            return true;
        }

        private void AddRowCell(object cell)
        {
            EnsureListCapacity(_row);
            _row.Add(cell);
        }

        private void EnsureListCapacity<T>(List<T> values)
        {
            if (values.Count < values.Capacity) return;
            var before = values.Capacity;
            var next = before == 0 ? 4 : checked(before * 2);
            // Fund old and replacement backing simultaneously; a denied or
            // failed allocation leaves the old source snapshot unchanged.
            using var replacement = _context.MemoryGovernor.ReserveTemporary(16L * next, _span);
            values.Capacity = next;
            replacement.Commit(16L * next);
            _context.MemoryGovernor.Release(16L * before);
            _owner.StorageChanged();
        }

        private void EnsureFieldCapacity(int additional)
        {
            var required = checked(_field.Length + additional);
            if (required <= _field.Capacity) return;
            var before = _field.Capacity;
            var next = Math.Max(required, before == 0 ? 16 : checked(before * 2));
            using var replacement = _context.MemoryGovernor.ReserveTemporary(2L * next, _span);
            _field.EnsureCapacity(next);
            replacement.Commit(2L * next);
            _context.MemoryGovernor.Release(2L * before);
            _owner.StorageChanged();
        }

        private void AppendField(char value)
        {
            EnsureFieldCapacity(1);
            _field.Append(value);
        }

        private void AppendField(string value)
        {
            EnsureFieldCapacity(value.Length);
            _field.Append(value);
        }

        private void FinishField()
        {
            // Decoded fields commit their payload here and register with the
            // reclamation pool; row and table backing is charged separately
            // by the governed row containers.
            var payload = PyString.FromString(_field.ToString(), _context.MemoryGovernor, _span);
            _context.State.CallTemporaries.TrackFreshString(payload, _span);
            AddRowCell(payload);
            _field.Clear();
            _fieldStarted = false;
            _afterQuote = false;
        }

        private void EnqueueRecord(PyList record)
        {
            EnsureListCapacity(_ready);
            _ready.Add(record);
        }

        private void FinishRecord()
        {
            if (!_recordStarted && !_fieldStarted && _field.Length == 0 && _row.Count == 0)
            {
                EnqueueRecord(TrackRow(new PyList([], _context.MemoryGovernor, _span)));
                ReleaseRowScratch();
                return;
            }

            FinishField();
            EnqueueRecord(TrackRow(new PyList(_row, _context.MemoryGovernor, _span)));
            _row.Clear();
            ReleaseRowScratch();
            _recordStarted = false;
            _afterQuote = false;
        }

        // Drops the in-progress record after a failed feed so the next pull
        // starts clean. Completed queue entries are untouched.
        public void ResetRecord()
        {
            _row.Clear();
            _field.Clear();
            _inQuotes = false;
            _fieldStarted = false;
            _afterQuote = false;
            _recordStarted = false;
        }

        private void ReleaseRowScratch()
        {
            if (_row.Capacity > 1024)
            {
                var before = _row.Capacity;
                _row.TrimExcess();
                _context.MemoryGovernor.Release(16L * (before - _row.Capacity));
                _owner.StorageChanged();
            }
        }

        internal void ReleaseScratch()
        {
            var before = StorageBytes;
            _row.Clear();
            _row.Capacity = 0;
            _field.Clear();
            _field.Capacity = 0;
            // EOF can precede consumption of records already queued by Feed.
            // Their slots stay funded until the last record is handed out.
            if (_readyHead >= _ready.Count)
            {
                _ready.Clear();
                _ready.Capacity = 0;
                _readyHead = 0;
            }
            _context.MemoryGovernor.Release(before - StorageBytes);
            _owner.StorageChanged();
        }

        private void ReleaseReadyStorage()
        {
            var before = _ready.Capacity;
            _ready.Clear();
            _ready.Capacity = 0;
            _readyHead = 0;
            _context.MemoryGovernor.Release(16L * before);
            _owner.StorageChanged();
        }

        private PyList TrackRow(PyList record)
        {
            // Row backing reclaims with the row: the pool releases the
            // snapshotted backing charges once the row is dropped, while
            // wholesale replacement (Clear, slice-assignment) notifies the
            // pool through the value itself.
            _context.State.CallTemporaries.TrackFreshMutable(record, record.CommittedStorageBytes, _span);
            return record;
        }

        private bool MatchesQuoteAt(string text, int index)
            => _quoteCharacter is not null && MatchesCsvAt(text, index, _quoteCharacter);
    }


    private static bool MatchesCsvAt(string text, int index, string value)
    {
        if (index + value.Length > text.Length)
        {
            return false;
        }

        return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    internal sealed record CsvOptions(
        PyString Delimiter,
        PyString? QuoteChar,
        CsvQuotingMode Quoting,
        bool DoubleQuote,
        PyString? EscapeChar,
        bool SkipInitialSpace,
        PyString LineTerminator,
        bool Strict);

    private static LythonRuntimeException CsvError(string message, LythonSourceSpan span)
        => new(ModuleException("csv", "Error"), message, span);

}
