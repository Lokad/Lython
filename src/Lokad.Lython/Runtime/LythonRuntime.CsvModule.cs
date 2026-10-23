using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

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
                "reader" => ReaderCallable,
                "writer" => WriterCallable,
                "DictReader" => DictReaderCallable,
                "DictWriter" => DictWriterCallable,
                "Error" => new ExceptionTypeValue(ModuleException("csv", "Error")),
                "QUOTE_MINIMAL" => new BigInteger((int)CsvQuotingMode.Minimal),
                "QUOTE_ALL" => new BigInteger((int)CsvQuotingMode.All),
                "QUOTE_NONNUMERIC" => new BigInteger((int)CsvQuotingMode.NonNumeric),
                "QUOTE_NONE" => new BigInteger((int)CsvQuotingMode.None),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private enum FactoryKind { Reader, Writer, DictReader, DictWriter }
        private static readonly CsvFactory ReaderCallable = new(LythonKnownCallableSignatures.CsvReader, FactoryKind.Reader);
        private static readonly CsvFactory WriterCallable = new(LythonKnownCallableSignatures.CsvWriter, FactoryKind.Writer);
        private static readonly CsvFactory DictReaderCallable = new(LythonKnownCallableSignatures.CsvDictReader, FactoryKind.DictReader);
        private static readonly CsvFactory DictWriterCallable = new(LythonKnownCallableSignatures.CsvDictWriter, FactoryKind.DictWriter);

        private sealed class CsvFactory(LythonCallableSignature signature, FactoryKind kind) : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes
        {
            public string Name => signature.Name;
            public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
                => CreateAsync(CallBinder.BindNamedArgumentsWithPresence(args, span, signature, PythonCallableKind.Builtin), kind, span, context, false).GetAwaiter().GetResult();
            public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
                => CreateAsync(CallBinder.BindNamedArgumentsWithPresence(args, span, signature, PythonCallableKind.Builtin), kind, span, context, true);
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "__name__" or "__qualname__" => PyString.FromString(Name[4..]),
                    "__module__" => ExceptionTypeValue.SharedModuleLabel("csv"),
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<built-in function {Name[4..]}>");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private static async ValueTask<object> CreateAsync(BoundCallArguments bound, FactoryKind kind,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var args = bound.Values;
            var dictionary = kind is FactoryKind.DictReader or FactoryKind.DictWriter;
            var fields = dictionary ? await NormalizeFieldNamesAsync(args[1], span, context, asynchronous).ConfigureAwait(false) : PyNone.Instance;
            if (kind is FactoryKind.Writer or FactoryKind.DictWriter)
            {
                var action = kind == FactoryKind.DictWriter ? GetExtrasAction(bound, span) : PyNone.Instance;
                var write = !bound.Assigned[0] || args[0] is PyNone && kind == FactoryKind.Writer ? null
                    : asynchronous ? await ResolveWriteAsync(args[0], span, context).ConfigureAwait(false) : ResolveWrite(args[0], span, context);
                var options = await GetOptionsAsync(bound, dictionary ? CsvOptionArgumentLayout.Dictionary : CsvOptionArgumentLayout.Standard,
                    span, context, asynchronous).ConfigureAwait(false);
                var writer = CreateWriter(options, write, span, context);
                return kind == FactoryKind.Writer ? writer : new CsvDictWriterObject(writer, fields,
                    bound.Assigned[2] ? args[2] : PyString.Empty, action, span);
            }
            // Python acquires the source before evaluating dialect truth callbacks.
            // Transfer the one cursor only after options and parser construction succeed.
            PyIteration.Cursor? cursor = null;
            try
            {
                if (args[0] is not PyList)
                    cursor = asynchronous ? await PyIteration.Cursor.CreateAsync(args[0], span, context).ConfigureAwait(false)
                        : PyIteration.Cursor.Create(args[0], span, context);
                var format = await GetOptionsAsync(bound, dictionary ? CsvOptionArgumentLayout.Dictionary : CsvOptionArgumentLayout.Standard,
                    span, context, asynchronous).ConfigureAwait(false);
                var records = new CsvRecordSource(args[0] as PyList, cursor, format, span, context);
                cursor = null;
                return dictionary ? new CsvDictReaderObject(records, fields, args[2], args[3], context, span) : new CsvReaderObject(records);
            }
            finally { if (cursor is not null) await cursor.DisposeAsync().ConfigureAwait(false); }
        }

        private static CsvWriterObject CreateWriter(CsvOptions options, ICallable? write, LythonSourceSpan span, ExecutionContext context)
            => new(options, write, context.MemoryGovernor, span);

        private static async ValueTask<object> NormalizeFieldNamesAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (value is PyNone) return value;
            var iterator = asynchronous ? await IterAsync([value], span, context).ConfigureAwait(false) : Iter([value], span, context);
            if (!ReferenceEquals(iterator, value)) return value;
            using var items = asynchronous ? await PyIteration.MaterializeLeasedAsync(value, span, context).ConfigureAwait(false)
                : PyIteration.MaterializeLeased(value, span, context);
            return new PyList(items.Items, context.MemoryGovernor, span);
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

        private static async ValueTask<CsvOptions> GetOptionsAsync(BoundCallArguments bound, CsvOptionArgumentLayout layout,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var args = bound.Values;
            if (bound.Assigned[layout.Dialect] && args[layout.Dialect] is not PyNone &&
                !(args[layout.Dialect] is PyString dialect && dialect.Utf8Bytes.Span.SequenceEqual("excel"u8)))
                throw RuntimeErrors.Type("csv dialect registry is unsupported; pass explicit CSV options instead.", span);
            var delimiter = Character(layout.Delimiter, "delimiter", PyStringOps.CommaLiteral, false)!;
            var quote = Character(layout.QuoteCharacter, "quotechar", PyString.FromString("\""), true);
            var escape = Character(layout.EscapeCharacter, "escapechar", null, true);
            var quoting = quote is null ? CsvQuotingMode.None : CsvQuotingMode.Minimal;
            if (bound.Assigned[layout.Quoting])
            {
                if (!PyNumberOps.TryAsInteger(args[layout.Quoting], out var value) || value < 0 || value > 3)
                    throw RuntimeErrors.Type("csv quoting must be one of the supported QUOTE_* constants.", span);
                quoting = (CsvQuotingMode)(int)value;
            }
            if (quote is null && quoting != CsvQuotingMode.None) throw RuntimeErrors.Type("quotechar must be set if quoting is enabled", span);
            var doubleQuote = await Truth(layout.DoubleQuote, true).ConfigureAwait(false);
            var skip = await Truth(layout.SkipInitialSpace, false).ConfigureAwait(false);
            var strict = await Truth(layout.Strict, false).ConfigureAwait(false);
            var terminator = bound.Assigned[layout.LineTerminator] ? args[layout.LineTerminator] as PyString : PyString.FromString("\r\n");
            if (terminator is null) throw RuntimeErrors.Type("csv lineterminator must be a string.", span);
            return new CsvOptions(delimiter, quote, quoting, doubleQuote, escape, skip, terminator, strict);

            PyString? Character(int index, string name, PyString? fallback, bool allowNone)
            {
                if (!bound.Assigned[index]) return fallback;
                if (allowNone && args[index] is PyNone) return null;
                if (args[index] is not PyString text || text.Length != 1) throw RuntimeErrors.Type($"csv {name} must be one character.", span);
                return text;
            }
            async ValueTask<bool> Truth(int index, bool fallback)
                => !bound.Assigned[index] ? fallback : asynchronous
                    ? await IsTruthyAsync(args[index], context, span).ConfigureAwait(false) : IsTruthy(args[index], context, span);
        }

        private static object GetExtrasAction(BoundCallArguments bound, LythonSourceSpan span)
        {
            if (!bound.Assigned[3]) return PyString.FromString("raise");
            if (bound.Values[3] is not PyString text) throw RuntimeErrors.Type("csv extrasaction must be a string.", span);
            // Only these fixed ASCII labels can match; reject before decoding large inputs.
            var length = text.Utf8Bytes.Length;
            if (length == 5 && text.AsString().Equals("raise", StringComparison.OrdinalIgnoreCase)) return PyString.FromString("raise");
            if (length == 6 && text.AsString().Equals("ignore", StringComparison.OrdinalIgnoreCase)) return PyString.FromString("ignore");
            throw new LythonRuntimeException("ValueError", "extrasaction must be 'raise' or 'ignore'.", span);
        }
    }


    // The parser owns scratch; escaping row/field estimates stay batch-owned.
    // Acquisition may await; parsing and storage admission remain synchronous.
    internal sealed class CsvRecordSource
    {
        private readonly CsvRecordParser _parser;
        private PyIteration.Cursor? _cursor;
        private PyList? _indexed;
        private int _position;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private bool _completed;

        // Source/parser objects, builders and list shells; backing is charged
        // from actual capacities, independently of emitted values.
        internal const long ShellBytes = 512;
        internal long OwnedBytes => ShellBytes + _parser.StorageBytes;
        internal bool Completed => _completed;

        private CsvRecordSource(CsvOptions options, LythonSourceSpan span, ExecutionContext context)
        {
            _context = context;
            _span = span;
            using var storage = context.MemoryGovernor.ReserveTemporary(ShellBytes + 32, span);
            _parser = new CsvRecordParser(options, context, span, this);
            storage.Commit(ShellBytes + 32);
        }

        public CsvRecordSource(object source, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
            : this(options, span, context)
        {
            try
            {
                if (source is PyList list) _indexed = list;
                else _cursor = PyIteration.Cursor.Create(source, span, context);
            }
            catch { DiscardConstruction(); throw; }
        }

        internal CsvRecordSource(PyList? indexed, PyIteration.Cursor? cursor, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
            : this(options, span, context)
        {
            _indexed = indexed;
            _cursor = cursor;
        }

        private void DiscardConstruction()
        {
            _parser.ReleaseScratch();
            _context.MemoryGovernor.Release(ShellBytes);
        }

        public int PhysicalLineCount { get; private set; }

        // Advances the shared cursor; every iterator over this source observes
        // the same position, matching CPython (a second pass sees nothing new).
        public bool TryMoveNext(out PyList row)
        {
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

        public async ValueTask<PyList?> TryMoveNextAsync()
        {
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
                        continue;
                    }

                    var current = _indexed[_position];
                    _position++;
                    FeedPulledLine(current);
                    continue;
                }

                PyIterationResult next;
                try
                {
                    next = await _cursor!.TryMoveNextAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // Version-checked iterators (large-list storage, views)
                    // surface concurrent modification as a CLR failure; fail
                    // explicitly instead of leaking it.
                    throw new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
                }

                if (!next.HasValue)
                {
                    await FinishExhaustedAsync().ConfigureAwait(false);
                    continue;
                }

                FeedPulledLine(next.Value);
            }
        }

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
                    moved = _cursor!.TryMoveNext(out current!);
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

            }

            FeedPulledLine(current);
        }

        private void FeedPulledLine(object current)
        {
            _context.CheckExecution(_span);
            if (!PyStringOps.TryAsString(current, out var line))
            {
                throw new LythonRuntimeException("TypeError", "csv.reader(csvfile) expects an iterable of strings.", _span);
            }

            PhysicalLineCount++;
            using var decoded = _context.MemoryGovernor.ReserveTemporary(checked(32L + 2L * line.GetUtf16CodeUnitCount()), _span);
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
                try
                {
                    if (_cursor is not null) await _cursor.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    _cursor = null;
                    _indexed = null;
                    _parser.ReleaseScratch();
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
        private bool _fieldWasQuoted;
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
                if ((i & 4095) == 0) _context.CheckExecution(_span);
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
                            _afterQuote = _options.DoubleQuote;
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
                    _fieldWasQuoted = true;
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
            // Fields remain batch-owned; row/table backing is admitted separately.
            var payload = PyString.FromString(_field.ToString(), _context.MemoryGovernor, _span);
            object cell = _options.Quoting == CsvQuotingMode.NonNumeric && !_fieldWasQuoted && _field.Length != 0
                ? ParseOwnedFloatText(payload, _context, _span) : payload;
            AddRowCell(cell);
            _field.Clear();
            _fieldStarted = false;
            _fieldWasQuoted = false;
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
                EnqueueRecord(new PyList([], _context.MemoryGovernor, _span));
                ReleaseRowScratch();
                return;
            }

            FinishField();
            EnqueueRecord(new PyList(_row, _context.MemoryGovernor, _span));
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
            _fieldWasQuoted = false;
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
        }

        private void ReleaseReadyStorage()
        {
            var before = _ready.Capacity;
            _ready.Clear();
            _ready.Capacity = 0;
            _readyHead = 0;
            _context.MemoryGovernor.Release(16L * before);
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
