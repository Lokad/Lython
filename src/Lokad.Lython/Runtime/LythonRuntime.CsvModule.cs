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
                "writer" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvWriter, Writer),
                "DictReader" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictReader, DictReader, DictReaderAsync),
                "DictWriter" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictWriter, DictWriter),
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
            context.CheckExecutionBudget(span);

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            var records = new CsvRecordSource(arguments[0], options, span, context);
            return new CsvReaderObject(records, context.MemoryGovernor, span);
        }

        private object DictReader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);

            ExecutionContext.TextFileHandle? file = null;
            if (arguments.Length > 0 && arguments[0] is not PyNone)
            {
                if (arguments[0] is ExecutionContext.TextFileHandle handle)
                {
                    file = handle;
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", "csv.writer(fileobj[, dialect][, ...]) expects a text file handle.", span);
                }
            }

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            return new CsvWriterObject(options, file, context.MemoryGovernor, span);
        }

        private object DictWriter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(fileobj, fieldnames, ...) expects a text file handle and field names.", span);
            }

            var fieldNames = ToCsvFieldNames(arguments[1], "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", span, context);
            var restVal = arguments.Length > 2 && arguments[2] is not PyNone ? arguments[2] : PyString.Empty;
            var extrasAction = GetExtrasAction(arguments, 3, span);
            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            // Own the dictionary-writer shell beside the governed writer and field names.
            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            return new CsvDictWriterObject(new CsvWriterObject(options, file, context.MemoryGovernor, span), fieldNames, restVal, extrasAction);
        }

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

            if (arguments[index] is not BigInteger integer || integer < 0 || integer > (int)CsvQuotingMode.None)
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

            var result = names.ToArray();
            var owned = checked(32L + 16L * result.Length);
            context.MemoryGovernor.Reserve(owned, span);
            context.MemoryGovernor.Commit(owned);
            return result;
        }

        private static PyString[] ToFieldNameList(PyList row, LythonSourceSpan span, ExecutionContext context)
        {
            // The header array is retained by the reader; names stay aliased.
            var owned = checked(32L + 16L * row.Count);
            context.MemoryGovernor.Reserve(owned, span);
            context.MemoryGovernor.Commit(owned);
            var names = new PyString[row.Count];
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
    // Field payloads commit at parse time and register with the reclamation
    // pool; sweeps release charges for fields the guest dropped while keeping
    // every retained alias charged. The field-builder transient lives as long
    // as the source; it is released once the source is exhausted, or held
    // (safe direction) when the consumer abandons the tail.
    internal sealed class CsvRecordSource
    {
        private readonly CsvRecordParser _parser;
        private readonly IEnumerator<object>? _cursor;
        private readonly object _origin;
        private IAsyncEnumerator<object>? _asyncCursor;
        private readonly PyList? _indexed;
        private int _position;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly MemoryGovernor.TemporaryMemoryReservation _fieldScratch;
        private readonly ChargeReclamationPool _pool;
        private PyList? _pushedBack;
        private PyList? _headerRow;
        private long _pulls;
        private bool _completed;

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
            if (source is PyList list)
            {
                _indexed = list;
            }
            else
            {
                _cursor = ToSequence(source, span, context).GetEnumerator();
            }
            _fieldScratch = context.MemoryGovernor.ReserveTemporary(0, span);
            _pool = new ChargeReclamationPool(context.MemoryGovernor);
            context.State.RegisterCsvSource(this, _pool, _fieldScratch);
            _parser = new CsvRecordParser(options, context, span, _fieldScratch, _pool);
        }

        public int PhysicalLineCount { get; private set; }

        internal ChargeReclamationPool Pool => _pool;

        // Pulls the first record for header inference; the header stays
        // retained (its field aliases remain charged through live entries)
        // while data rows stream past.
        public PyList? PullHeader()
        {
            if (!TryMoveNext(out var row))
            {
                return null;
            }

            _headerRow = row;
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

        public async ValueTask<PyList?> PullHeaderAsync()
        {
            _headerRow = await TryMoveNextAsync().ConfigureAwait(false);
            return _headerRow;
        }

        private void Pull()
        {
            _context.State.NoteCsvPull();
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

            _context.CheckExecutionBudget(_span);
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

            if ((++_pulls & 255) == 0)
            {
                _pool.Sweep();
            }
        }

        private async ValueTask PullAsync()
        {
            _context.State.NoteCsvPull();
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

            _asyncCursor ??= ToSequenceAsync(_origin, _span, _context).GetAsyncEnumerator();
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
            _context.CheckExecutionBudget(_span);
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

            if ((++_pulls & 255) == 0)
            {
                _pool.Sweep();
            }
        }

        private void FinishExhausted()
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
                _cursor?.Dispose();
                _fieldScratch.Dispose();
                _pool.Sweep(full: true);
            }
        }

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
                _cursor?.Dispose();
                if (_asyncCursor is not null)
                {
                    await _asyncCursor.DisposeAsync().ConfigureAwait(false);
                    _asyncCursor = null;
                }

                _fieldScratch.Dispose();
                _pool.Sweep(full: true);
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
        private MemoryGovernor.TemporaryMemoryReservation? _rowScratch;
        private long _chargedRowCapacity;
        private readonly StringBuilder _field = new();
        private readonly MemoryGovernor.TemporaryMemoryReservation _fieldScratch;
        private readonly ChargeReclamationPool _pool;
        private readonly List<PyList> _ready = new();
        private int _readyHead;
        private long _chargedFieldCapacity;
        private long _chargedReadyCapacity;
        private bool _inQuotes;
        private bool _fieldStarted;
        private bool _afterQuote;
        private bool _recordStarted;

        public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation fieldScratch, ChargeReclamationPool pool)
        {
            _options = options;
            _context = context;
            _span = span;
            _fieldScratch = fieldScratch;
            _pool = pool;
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

            return true;
        }

        public void Feed(string text)
        {
            NoteFieldCapacity();
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
                            _field.Append(quoteCharacter);
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

                    _field.Append(c);
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
                _field.Append(c);
            }

            if (_inQuotes)
            {
                if (text.Length == 0 || (text[^1] != '\n' && text[^1] != '\r'))
                {
                    _field.Append('\n');
                }

                return;
            }

            if (text.Length == 0)
            {
                EnqueueRecord(new PyList([], _context.MemoryGovernor, _span));
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

                _field.Append(_escapeCharacter);
                index += _escapeCharacter.Length - 1;
                return true;
            }

            index += _escapeCharacter.Length;
            _field.Append(text[index]);
            _fieldStarted = true;
            _recordStarted = true;
            return true;
        }

        private void AddRowCell(object cell)
        {
            // The row scratch doubles geometrically like the drains beside
            // it; cover each capacity step (including the old/new overlap)
            // before the cell lands, and release the reservation with the
            // finished record below.
            _rowScratch ??= _context.MemoryGovernor.ReserveTemporary(0, _span);
            if (_row.Count == _row.Capacity)
            {
                var predicted = _row.Capacity == 0 ? 4L : (long)_row.Capacity * 2L;
                _rowScratch.Grow(checked(16L * (predicted - _chargedRowCapacity)), _span);
            }

            _row.Add(cell);
            if (_row.Capacity > _chargedRowCapacity)
            {
                _rowScratch.Grow(checked(16L * (_row.Capacity - _chargedRowCapacity)), _span);
                _chargedRowCapacity = _row.Capacity;
            }
        }

        private void NoteFieldCapacity()
        {
            // StringBuilder doubles geometrically; cover the live peak
            // incrementally. Checked at Feed and FinishField boundaries so
            // growth between checks stays within one physical line, which
            // the line stage already bounds.
            if (_field.Capacity > _chargedFieldCapacity)
            {
                _fieldScratch.Grow(checked(2L * (_field.Capacity - _chargedFieldCapacity)), _span);
                _chargedFieldCapacity = _field.Capacity;
            }
        }

        private void FinishField()
        {
            NoteFieldCapacity();
            // Decoded fields commit their payload here and register with the
            // reclamation pool; row and table backing is charged separately
            // by the governed row containers.
            var payload = PyString.FromString(_field.ToString(), _context.MemoryGovernor, _span);
            _pool.TrackString(payload);
            AddRowCell(payload);
            _field.Clear();
            _fieldStarted = false;
            _afterQuote = false;
        }

        private void EnqueueRecord(PyList record)
        {
            // Ready slots ride the field scratch beside the builder: one Feed
            // call can complete many records, and the transient releases with
            // the source once exhausted.
            if (_ready.Count == _ready.Capacity)
            {
                var predicted = _ready.Capacity == 0 ? 4L : (long)_ready.Capacity * 2L;
                _fieldScratch.Grow(checked(16L * (predicted - _chargedReadyCapacity)), _span);
                _chargedReadyCapacity = _ready.Capacity == 0 ? 4L : (long)_ready.Capacity * 2L;
            }

            _ready.Add(record);
            if (_ready.Capacity > _chargedReadyCapacity)
            {
                _fieldScratch.Grow(checked(16L * (_ready.Capacity - _chargedReadyCapacity)), _span);
                _chargedReadyCapacity = _ready.Capacity;
            }
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
            _rowScratch?.Dispose();
            _rowScratch = null;
            _chargedRowCapacity = 0;
            if (_row.Capacity > 1024)
            {
                // A single wide record must not pin megabytes of scratch
                // backing for the rest of the source; regrowth rides the
                // reservation above.
                _row.TrimExcess();
            }
        }

        private PyList TrackRow(PyList record)
        {
            // Row backing reclaims with the row: the pool releases the
            // snapshotted backing charges once the row is dropped, while
            // wholesale replacement (Clear, slice-assignment) notifies the
            // pool through the value itself.
            _pool.TrackMutable(record, record.CommittedStorageBytes);
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