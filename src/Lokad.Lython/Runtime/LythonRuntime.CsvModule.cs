using System.Buffers;
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
        private static readonly CsvFactory ReaderCallable = new(LythonKnownCallableSignatures.Csv.Reader, FactoryKind.Reader);
        private static readonly CsvFactory WriterCallable = new(LythonKnownCallableSignatures.Csv.Writer, FactoryKind.Writer);
        private static readonly CsvFactory DictReaderCallable = new(LythonKnownCallableSignatures.Csv.DictReader, FactoryKind.DictReader);
        private static readonly CsvFactory DictWriterCallable = new(LythonKnownCallableSignatures.Csv.DictWriter, FactoryKind.DictWriter);

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

        private static async ValueTask<object> CreateAsync(BoundBuiltinArguments bound, FactoryKind kind,
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
                var records = new CsvRecordSource(args[0] as PyList, cursor, args[0] as ExecutionContext.TextFileHandle, format, span, context);
                cursor = null;
                return dictionary ? new CsvDictReaderObject(records, fields, args[2], args[3], context, span) : new CsvReaderObject(records);
            }
            finally { if (cursor is not null) await cursor.DisposeAsync().ConfigureAwait(false); }
        }

        private static CsvWriterObject CreateWriter(CsvOptions options, object? write, LythonSourceSpan span, ExecutionContext context)
            => new(options, write, context, span);

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
        private static object ResolveWrite(object stream, LythonSourceSpan span, ExecutionContext context)
        {
            if (stream is ExecutionContext.TextFileHandle or ExecutionContext.BinaryFileHandle) return stream;
            try
            {
                return RequireWriteMethod(PyTextStream.ResolveMember(stream, "write", span, context), span);
            }
            catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("AttributeError"), error.Identity))
            {
                throw MissingWriteMethod(span);
            }
        }

        private static async ValueTask<object> ResolveWriteAsync(object stream, LythonSourceSpan span, ExecutionContext context)
        {
            if (stream is ExecutionContext.TextFileHandle or ExecutionContext.BinaryFileHandle) return stream;
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

        private static async ValueTask<CsvOptions> GetOptionsAsync(BoundBuiltinArguments bound, CsvOptionArgumentLayout layout,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var args = bound.Values;
            if (bound.Assigned[layout.Dialect] && args[layout.Dialect] is not PyNone &&
                !(args[layout.Dialect] is PyString dialect && dialect.Utf8Span.SequenceEqual("excel"u8)))
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
            ValidateCharacter(delimiter, "delimiter", true);
            ValidateCharacter(escape, "escapechar", !skip);
            ValidateCharacter(quote, "quotechar", !skip);
            ValidatePair(delimiter, "delimiter", escape, "escapechar");
            ValidatePair(delimiter, "delimiter", quote, "quotechar");
            ValidatePair(escape, "escapechar", quote, "quotechar");
            return new CsvOptions(delimiter, quote, quoting, doubleQuote, escape, skip, terminator, strict);

            void ValidateCharacter(PyString? value, string name, bool allowSpace)
            {
                if (value is null) return;
                var bytes = value.Utf8Span;
                if (bytes.Length == 1 && (bytes[0] is 10 or 13 || bytes[0] == 32 && !allowSpace))
                    throw new LythonRuntimeException("ValueError", $"bad {name} value", span);
                if (CsvWriterMembers.TerminatorContains(terminator.Utf8Span, bytes, context, span))
                    throw new LythonRuntimeException("ValueError", $"bad {name} or lineterminator value", span);
            }
            void ValidatePair(PyString? left, string leftName, PyString? right, string rightName)
            {
                if (left is not null && right is not null && left.Utf8Span.SequenceEqual(right.Utf8Span))
                    throw new LythonRuntimeException("ValueError", $"bad {leftName} or {rightName} value", span);
            }
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

        private static object GetExtrasAction(BoundBuiltinArguments bound, LythonSourceSpan span)
        {
            if (!bound.Assigned[3]) return PyString.FromString("raise");
            if (bound.Values[3] is not PyString text) throw RuntimeErrors.Type("csv extrasaction must be a string.", span);
            // Only these fixed ASCII labels can match; reject before decoding large inputs.
            var length = text.ByteLength;
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
        private ExecutionContext.TextFileHandle? _nativeFile;
        private int _position;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private bool _completed;

        // Source/parser objects, builders, list shells and the dialect's
        // fixed-size search sets. Backing and emitted values are charged separately.
        internal const long ShellBytes = 1024;
        internal long OwnedBytes => ShellBytes + _parser.StorageBytes;

        private CsvRecordSource(CsvOptions options, LythonSourceSpan span, ExecutionContext context)
        {
            _context = context;
            _span = span;
            using var storage = context.MemoryGovernor.ReserveTemporary(ShellBytes, span);
            _parser = new CsvRecordParser(options, context, span);
            storage.Commit(ShellBytes);
        }

        public CsvRecordSource(object source, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
            : this(options, span, context)
        {
            try
            {
                if (source is PyList list) _indexed = list;
                else _cursor = PyIteration.Cursor.Create(source, span, context);
                _nativeFile = source as ExecutionContext.TextFileHandle;
            }
            catch { DiscardConstruction(); throw; }
        }

        internal CsvRecordSource(PyList? indexed, PyIteration.Cursor? cursor, ExecutionContext.TextFileHandle? nativeFile,
            CsvOptions options, LythonSourceSpan span, ExecutionContext context)
            : this(options, span, context)
        {
            _indexed = indexed;
            _cursor = cursor;
            _nativeFile = nativeFile;
        }

        private void DiscardConstruction()
        {
            _parser.ReleaseScratch();
            _context.MemoryGovernor.Release(ShellBytes);
        }

        public int PhysicalLineCount { get; private set; }

        // Advances the shared cursor; every iterator over this source observes
        // the same position, matching CPython (a second pass sees nothing new).
        internal PyList AdoptRecord(object[] record)
            => PyList.FromTransferredArray(record, _context.MemoryGovernor, _span);

        // Private DictReader rows never need a guest list. Their capacity stays
        // funded until either public adoption or the consumer's awaited finally.
        internal void ReleaseRecord(object[] record)
            => _context.MemoryGovernor.Release(PyList.EstimateCapacityBytes(record.Length));

        internal bool CanUnpackDirectly => _nativeFile is not null;

        internal int UnpackNext(Memory<ExecutableSlot> destination)
        {
            _nativeFile?.CheckCsvReadReentry();
            while (_nativeFile is { } file)
            {
                int count;
                try { count = file.ReadCsvLine(this, destination, asynchronous: false).GetAwaiter().GetResult(); }
                catch { _parser.ResetRecord(); throw; }
                if (count == EndOfNativeInput) return FinishUnpacked(destination);
                if (count >= 0) return count;
            }
            return -1;
        }

        internal async ValueTask<int> UnpackNextAsync(Memory<ExecutableSlot> destination)
        {
            _nativeFile?.CheckCsvReadReentry();
            while (_nativeFile is { } file)
            {
                int count;
                try { count = await file.ReadCsvLine(this, destination, asynchronous: true).ConfigureAwait(false); }
                catch { _parser.ResetRecord(); throw; }
                if (count == EndOfNativeInput) return FinishUnpacked(destination);
                if (count >= 0) return count;
            }
            return -1;
        }

        internal bool TryMoveNext([NotNullWhen(true)] out object[]? row)
        {
            row = (object[]?)ReadNext(null);
            return row is not null;
        }

        internal object? ReadNext(CsvDictReaderObject? dictionary)
        {
            _nativeFile?.CheckCsvReadReentry();
            TryReadRecord(dictionary, asynchronous: false, out var row, out _, out _);
            return row;
        }

        internal ValueTask<object?> TryMoveNextAsync(CsvDictReaderObject? dictionary = null)
        {
            // A budget hook can try to read the same source. Reject that pull
            // before its error path can reset the active parser's descriptors.
            _nativeFile?.CheckCsvReadReentry();
            return TryReadRecord(dictionary, asynchronous: true, out var row, out var line, out var item)
                ? new(row) : ResumeReadRecord(dictionary, line, item);
        }

        // A row transfers out of the parser before any further source pull.
        // Completed sources share this loop. Only an outstanding file or
        // iterator operation needs to carry the loop into a continuation.
        private bool TryReadRecord(CsvDictReaderObject? dictionary, bool asynchronous, out object? row,
            out ValueTask<int>? pendingLine, out ValueTask<PyIterationResult> pendingItem)
        {
            pendingLine = null;
            pendingItem = default;
            while (!_parser.TryTakeCompleted(out row) && !_completed)
            {
                if (_nativeFile is { } file)
                {
                    int count;
                    try
                    {
                        var line = file.ReadCsvLine(this, null, asynchronous, dictionary);
                        if (asynchronous && !line.IsCompleted)
                        {
                            pendingLine = line;
                            return false;
                        }
                        count = line.GetAwaiter().GetResult();
                    }
                    catch { _parser.ResetRecord(); throw; }
                    if (count == EndOfNativeInput) FinishExhausted();
                }
                else if (_indexed is { } indexed)
                {
                    // A failed line is still consumed. Index the live list so
                    // replacement of its storage and mutations remain visible.
                    if (_position < indexed.Count)
                    {
                        var current = indexed[_position];
                        _position++;
                        FeedPulledLine(current);
                    }
                    else FinishExhausted();
                }
                else
                {
                    PyIterationResult next;
                    try
                    {
                        if (asynchronous)
                        {
                            var item = _cursor!.TryMoveNextAsync();
                            if (!item.IsCompleted)
                            {
                                pendingItem = item;
                                return false;
                            }
                            next = item.GetAwaiter().GetResult();
                        }
                        else next = _cursor!.TryMoveNext();
                    }
                    catch (InvalidOperationException) { throw CursorMutation(); }
                    catch { _parser.ResetRecord(); throw; }
                    AcceptCursorItem(next);
                }
            }
            return true;
        }

        private async ValueTask<object?> ResumeReadRecord(CsvDictReaderObject? dictionary, ValueTask<int>? pendingLine,
            ValueTask<PyIterationResult> pendingItem)
        {
            object? row;
            do
            {
                if (pendingLine is { } line)
                {
                    int count;
                    try { count = await line.ConfigureAwait(false); }
                    catch { _parser.ResetRecord(); throw; }
                    if (count == EndOfNativeInput) FinishExhausted();
                }
                else
                {
                    PyIterationResult next;
                    try { next = await pendingItem.ConfigureAwait(false); }
                    catch (InvalidOperationException) { throw CursorMutation(); }
                    catch { _parser.ResetRecord(); throw; }
                    AcceptCursorItem(next);
                }
            } while (!TryReadRecord(dictionary, asynchronous: true, out row, out pendingLine, out pendingItem));
            return row;
        }

        private LythonRuntimeException CursorMutation()
        {
            // Version-checked CLR iterators surface concurrent modification
            // as a native failure. The next pull starts with a clean record.
            _parser.ResetRecord();
            return new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
        }

        private void AcceptCursorItem(PyIterationResult next)
        {
            if (next.HasValue) FeedPulledLine(next.Value);
            else FinishExhausted();
        }

        internal const int EndOfNativeInput = -2;
        internal GovernedByteBuilder NativeLineBuffer => _parser.InputBuffer;
        internal bool CanBorrowNativeLine => _parser.CanDescribeLiteralRecord;

        internal int ConsumeStagedNativeLine(int before, Memory<ExecutableSlot>? destination)
        {
            _context.CheckExecution(_span);
            PhysicalLineCount++;
            return _parser.ConsumeStagedLine(before, destination);
        }

        internal int ConsumeBorrowedNativeLine(ReadOnlySpan<byte> line, Memory<ExecutableSlot>? destination,
            CsvDictReaderObject? dictionary)
        {
            _context.CheckExecution(_span);
            PhysicalLineCount++;
            return _parser.ConsumeBorrowedLine(line, destination, dictionary);
        }

        private void FeedPulledLine(object current)
        {
            _context.CheckExecution(_span);
            if (!PyStringOps.TryAsString(current, out var line))
            {
                _parser.ResetRecord();
                throw CsvError($"iterator should return strings, not {RuntimeErrors.OperandTypeName(current)} (the file should be opened in text mode)", _span);
            }

            PhysicalLineCount++;
            try
            {
                _parser.Feed(line);
            }
            catch
            {
                // The failed line is consumed either way; drop its partial
                // record so the next pull starts clean instead of poisoning
                // every later row. A failed __next__ begins a fresh record on
                // its next invocation, including after source/type failures.
                _parser.ResetRecord();
                throw;
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
                ReleaseExhaustedInput();
            }
        }

        private int FinishUnpacked(Memory<ExecutableSlot> destination)
        {
            _completed = true;
            try { return _parser.FinishUnpacked(destination.Span); }
            catch { _parser.ResetRecord(); throw; }
            finally { ReleaseExhaustedInput(); }
        }

        private void ReleaseExhaustedInput()
        {
            try
            {
                _cursor?.Dispose();
            }
            finally
            {
                _cursor = null;
                _indexed = null;
                _nativeFile = null;
                _parser.ReleaseScratch();
            }
        }
    }

    internal sealed class CsvRecordParser
    {
        private readonly CsvOptions _options;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly List<CsvField> _fields = new();
        private readonly GovernedByteBuilder _record;
        private readonly PyString? _activeQuote;
        private readonly SearchValues<byte> _fieldStartBytes;
        private readonly SearchValues<byte> _unquotedBytes;
        private readonly SearchValues<byte> _quotedBytes;
        private readonly int _literalDelimiter;
        private int _fieldStart;
        private long _stringBytes;
        private int _payloadBytes;
        // A buffered file can complete an inferred-header dictionary directly.
        // Otherwise the same slot publishes the private array for the caller.
        private object? _readyRecord;
        private const int ReusableFieldCapacity = 1024;
        private enum FieldState { RecordStart, FieldStart, Bare, Quoted, AfterQuote, AfterEscapedNewline }
        private FieldState _state;
        private bool _fieldWasQuoted;

        // Field descriptors and native record bytes are private working storage.
        // Long fields share one immutable final payload. Short fields retain
        // inline bytes; every field keeps its own Python string identity.
        // Converted numbers stay unboxed until the final row is admitted.
        private readonly struct CsvField(int start, int length, double number = 0)
        {
            internal readonly int Start = start, Length = length;
            internal readonly double Number = number;
            internal bool IsNumber => Start < 0;
        }

        internal long StorageBytes => checked(16L * _fields.Capacity + _record.CommittedCapacity);
        internal GovernedByteBuilder InputBuffer => _record;

        internal int ConsumeStagedLine(int before, Memory<ExecutableSlot>? destination)
            => DescribeOwnedLine(before) ? PublishRecord(_record.WrittenSpan, destination) : -1;

        private int PublishRecord(ReadOnlySpan<byte> source, Memory<ExecutableSlot>? destination,
            CsvDictReaderObject? dictionary = null)
        {
            if (destination is { } slots) return UnpackRecord(source, slots.Span);
            EmitRecord(source, dictionary);
            return -1;
        }

        private bool DescribeOwnedLine(int before)
        {
            var line = _record.WrittenSpan[before..];
            if (before == 0 && CanDescribeLiteralRecord)
            {
                return TryDescribeLiteralRecord(line, out var nextField) || ResumeRecordAtField(line, nextField);
            }
            _record.Truncate(before);
            // CSV decoding only removes syntax bytes. Writes stay behind the
            // consumed input, and the already-admitted backing cannot grow.
            // Keep the backing alive until the completed record is consumed.
            return DescribeLine(line);
        }

        internal bool CanDescribeLiteralRecord => _state == FieldState.RecordStart &&
            _literalDelimiter >= 0 && _fields.Capacity != 0;

        private bool TryDescribeLiteralRecord(ReadOnlySpan<byte> line, out int nextField)
        {
            if (_readyRecord is not null) throw new InvalidOperationException("CSV input advanced before consuming its record.");
            nextField = 0;
            if (line.IsEmpty) return true;
            if (line[0] is (byte)'\r' or (byte)'\n')
            {
                _context.CheckExecution(_span);
                ValidateRecordEnd(line[1..]);
                return true;
            }
            // Already-funded literal fields keep their original byte offsets.
            // If the next field needs transformation or more scratch, the
            // general parser continues at that field without repeating the
            // prefix. The admitted buffer keeps those earlier ranges live.
            var quote = _activeQuote is { } active ? active.Utf8Span : default;
            var quoteStart = quote.IsEmpty ? -1 : quote[0];
            // Delimiters and line endings take precedence over a quote at
            // the same byte. Within a bare field, quotes are ordinary data.
            if (quoteStart == _literalDelimiter || quoteStart is '\r' or '\n') quoteStart = -1;
            var start = 0;
            while (_fields.Count < _fields.Capacity)
            {
                int end;
                if (start < line.Length && line[start] == quoteStart)
                {
                    if (quote.Length != 1) break;
                    var close = FindLiteralQuote(line, start + 1, (byte)quoteStart);
                    if (close < 0) break;
                    // Doubled quotes, trailing text and partial quoted fields
                    // preserve their original parser state and error order.
                    end = close + 1;
                    if (end < line.Length && line[end] != _literalDelimiter && line[end] is not ((byte)'\r' or (byte)'\n')) break;
                    DescribeField(new CsvField(start + 1, close - start - 1));
                }
                else
                {
                    end = FindLiteralFieldEnd(line, start);
                    DescribeField(CreateField(line, start, end - start, quoted: false));
                }

                if (end == line.Length) return true;
                if (line[end] is (byte)'\r' or (byte)'\n')
                {
                    ValidateRecordEnd(line[(end + 1)..]);
                    return true;
                }
                start = end + 1;
            }
            nextField = start;
            return false;
        }

        private int FindLiteralFieldEnd(ReadOnlySpan<byte> line, int offset)
        {
            while (offset < line.Length)
            {
                _context.CheckExecution(_span);
                var count = Math.Min(4096, line.Length - offset);
                var marker = line.Slice(offset, count).IndexOfAny(_unquotedBytes);
                if (marker >= 0) return offset + marker;
                offset += count;
            }
            return offset;
        }

        private int FindLiteralQuote(ReadOnlySpan<byte> line, int offset, byte marker)
        {
            while (offset < line.Length)
            {
                _context.CheckExecution(_span);
                var count = Math.Min(4096, line.Length - offset);
                var close = line.Slice(offset, count).IndexOf(marker);
                if (close >= 0) return offset + close;
                offset += count;
            }
            return -1;
        }

        private bool ResumeRecordAtField(ReadOnlySpan<byte> line, int start)
        {
            _record.Truncate(start);
            _fieldStart = start;
            _state = FieldState.FieldStart;
            return DescribeLine(line[start..]);
        }

        public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            _options = options;
            _context = context;
            _span = span;
            _record = new GovernedByteBuilder(context.MemoryGovernor, span, 16);

            _activeQuote = options.Quoting == CsvQuotingMode.None ? null : options.QuoteChar;
            var delimiter = options.Delimiter.Utf8Span;
            var quote = _activeQuote is { } q ? q.Utf8Span : default;
            var escape = options.EscapeChar is { } e ? e.Utf8Span : default;
            _literalDelimiter = delimiter.Length == 1 && escape.IsEmpty && !options.SkipInitialSpace ? delimiter[0] : -1;
            // The dialect is fixed for the reader. These sets find lead bytes;
            // the parser checks the full scalars before interpreting syntax.
            Span<byte> unquoted = stackalloc byte[5];
            unquoted[0] = (byte)'\r';
            unquoted[1] = (byte)'\n';
            unquoted[2] = delimiter[0];
            var bareCount = 3;
            if (!escape.IsEmpty) unquoted[bareCount++] = escape[0];
            _unquotedBytes = SearchValues.Create(unquoted[..bareCount]);
            if (quote.IsEmpty) _fieldStartBytes = _unquotedBytes;
            else
            {
                unquoted[bareCount] = quote[0];
                _fieldStartBytes = SearchValues.Create(unquoted[..(bareCount + 1)]);
            }

            Span<byte> quoted = stackalloc byte[2];
            var quotedCount = 0;
            if (!quote.IsEmpty) quoted[quotedCount++] = quote[0];
            if (!escape.IsEmpty) quoted[quotedCount++] = escape[0];
            _quotedBytes = SearchValues.Create(quoted[..quotedCount]);
        }

        // One source item completes at most one row. No subsequent item is
        // pulled until this independently funded result has been transferred.
        public bool TryTakeReady(out object[] row)
        {
            var present = TryTakeCompleted(out var record);
            row = (object[])record!;
            return present;
        }

        internal bool TryTakeCompleted(out object? row)
        {
            row = _readyRecord;
            _readyRecord = null;
            return row is not null;
        }

        public void Feed(ReadOnlySpan<byte> text)
        {
            if (DescribeLine(text)) EmitRecord(_record.WrittenSpan);
        }

        internal void Feed(PyString text)
        {
            if (CanDescribeLiteralRecord) _ = ConsumeBorrowedLine(text.Utf8Span, null);
            else Feed(text.Utf8Span);
        }

        internal int ConsumeBorrowedLine(ReadOnlySpan<byte> line, Memory<ExecutableSlot>? destination,
            CsvDictReaderObject? dictionary = null)
        {
            if (TryDescribeLiteralRecord(line, out var nextField)) return PublishRecord(line, destination, dictionary);

            // Retain only the already described prefix if the general parser
            // needs to transform or continue the record. Its offsets stay valid.
            if (nextField != 0)
            {
                var prefix = _record.GetWriteWindow(nextField, _context.State.CancellationToken).Span;
                for (var offset = 0; offset < prefix.Length;)
                {
                    _context.CheckExecution(_span);
                    var count = Math.Min(4096, prefix.Length - offset);
                    line.Slice(offset, count).CopyTo(prefix.Slice(offset, count));
                    offset += count;
                }
                _record.Advance(prefix.Length);
            }
            return ResumeRecordAtField(line, nextField) ? PublishRecord(_record.WrittenSpan, destination) : -1;
        }

        private bool DescribeLine(ReadOnlySpan<byte> text)
        {
            if (_readyRecord is not null) throw new InvalidOperationException("CSV input advanced before consuming its record.");
            var delimiter = _options.Delimiter.Utf8Span;
            var quote = _activeQuote is { } q ? q.Utf8Span : default;
            var escape = _options.EscapeChar is { } e ? e.Utf8Span : default;

            for (var i = 0; i < text.Length;)
            {
                _context.CheckExecution(_span);
                if (_state == FieldState.RecordStart && text[i] is not ((byte)'\r' or (byte)'\n'))
                    _state = FieldState.FieldStart;
                var width = Math.Min(4096, text.Length - i);
                if (_state == FieldState.FieldStart && _options.SkipInitialSpace && text[i] == (byte)' ')
                {
                    var next = text.Slice(i, width).IndexOfAnyExcept((byte)' ');
                    i += next < 0 ? width : next;
                    if (next < 0) continue;
                    width -= next;
                }

                var candidates = _state == FieldState.Quoted ? _quotedBytes
                    : _state == FieldState.FieldStart ? _fieldStartBytes : _unquotedBytes;
                var found = text.Slice(i, width).IndexOfAny(candidates);
                var ordinary = found < 0 ? width : found;
                if (ordinary != 0)
                {
                    if (_state == FieldState.AfterQuote) ResumeUnquotedField();
                    _record.Append(text.Slice(i, ordinary));
                    _context.CheckExecution(_span);
                    if (_state == FieldState.FieldStart) _state = FieldState.Bare;
                    i += ordinary;
                }
                if (found < 0) continue;

                var remaining = text[i..];
                if (_state == FieldState.Quoted)
                {
                    if (!escape.IsEmpty && remaining.StartsWith(escape))
                    {
                        if (!ConsumeEscape(text, ref i, escape)) return false;
                    }
                    else if (!quote.IsEmpty && remaining.StartsWith(quote))
                    {
                        if (_options.DoubleQuote && remaining[quote.Length..].StartsWith(quote))
                        {
                            _record.Append(quote);
                            i += 2 * quote.Length;
                        }
                        else
                        {
                            _state = _options.DoubleQuote ? FieldState.AfterQuote : FieldState.Bare;
                            i += quote.Length;
                        }
                    }
                    else { _record.Append(text.Slice(i, 1)); i++; }
                    continue;
                }
                var c = text[i];
                if (c is (byte)'\r' or (byte)'\n')
                {
                    DescribeRecordEnd(text[(i + 1)..]);
                    return true;
                }
                else if (remaining.StartsWith(delimiter))
                {
                    FinishField();
                    i += delimiter.Length;
                }
                else if (_state == FieldState.FieldStart && !quote.IsEmpty && remaining.StartsWith(quote))
                {
                    _state = FieldState.Quoted;
                    _fieldWasQuoted = true;
                    i += quote.Length;
                }
                else
                {
                    if (_state == FieldState.AfterQuote)
                    {
                        ResumeUnquotedField();
                        // Immediately after a closing quote this character is
                        // literal, even when it is the configured escape.
                        Rune.DecodeFromUtf8(remaining, out _, out var literalWidth);
                        _record.Append(remaining[..literalWidth]);
                        i += literalWidth;
                    }
                    else if (!escape.IsEmpty && remaining.StartsWith(escape))
                    {
                        if (!ConsumeEscape(text, ref i, escape)) return false;
                    }
                    else
                    {
                        _record.Append(text.Slice(i, 1));
                        if (_state == FieldState.FieldStart) _state = FieldState.Bare;
                        i++;
                    }
                }
            }
            _context.CheckExecution(_span);
            if (_state is FieldState.Quoted or FieldState.AfterEscapedNewline) return false;
            DescribeRecordEnd();
            return true;
        }

        public void Finish()
        {
            if (DescribeEndOfInput()) EmitRecord(_record.WrittenSpan);
        }

        internal int FinishUnpacked(Span<ExecutableSlot> destination)
            => DescribeEndOfInput() ? UnpackRecord(_record.WrittenSpan, destination) : -1;

        private bool DescribeEndOfInput()
        {
            if (_state == FieldState.RecordStart) return false;
            if (_options.Strict) throw CsvError("unexpected end of data", _span);
            DescribeRecordEnd();
            return true;
        }

        private void ResumeUnquotedField()
        {
            if (_options.Strict)
                throw CsvError($"'{_options.Delimiter.AsString()}' expected after '{_options.QuoteChar!.AsString()}'", _span);
            _state = FieldState.Bare;
        }

        // Returns whether the item's implicit end marker remains to process.
        private bool ConsumeEscape(ReadOnlySpan<byte> text, ref int index, ReadOnlySpan<byte> escape)
        {
            index += escape.Length;
            var quoted = _state == FieldState.Quoted;
            if (index == text.Length)
            {
                _record.Append("\n"u8);
                if (!quoted) _state = FieldState.Bare;
                _context.CheckExecution(_span);
                return false;
            }
            // Consume one Python character, including a supplementary scalar.
            Rune.DecodeFromUtf8(text[index..], out var escaped, out var width);
            _record.Append(text.Slice(index, width));
            if (!quoted)
                _state = escaped.Value is '\n' or '\r' ? FieldState.AfterEscapedNewline : FieldState.Bare;
            index += width;
            return true;
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

        private void FinishField()
        {
            var field = CreateField(_record.WrittenSpan, _fieldStart, _record.Length - _fieldStart, _fieldWasQuoted);
            EnsureListCapacity(_fields);
            DescribeField(field);
            _fieldStart = _record.Length;
            _state = FieldState.FieldStart;
            _fieldWasQuoted = false;
        }

        private CsvField CreateField(ReadOnlySpan<byte> source, int start, int length, bool quoted)
        {
            if (_options.Quoting != CsvQuotingMode.NonNumeric || quoted || length == 0)
                return new CsvField(start, length);
            // Conversion must still fail at this field before another line
            // can be fetched or later field syntax inspected.
            return new CsvField(-1, 0, ParseFloatText(source.Slice(start, length), _context, _span));
        }

        private void DescribeField(CsvField field)
        {
            _fields.Add(field);
            if (!field.IsNumber && field.Length != 0)
            {
                _stringBytes = checked(_stringBytes + PyString.EstimateApproximateBytes(field.Length));
                if (field.Length > PyString.InlineUtf8Capacity)
                    _payloadBytes = checked(_payloadBytes + field.Length);
            }
        }

        private void DescribeRecordEnd(ReadOnlySpan<byte> remainder = default)
        {
            if (!(_state == FieldState.RecordStart && _record.Length == _fieldStart && _fields.Count == 0))
                FinishField();
            ValidateRecordEnd(remainder);
        }

        private void ValidateRecordEnd(ReadOnlySpan<byte> remainder)
        {
            // CPython accepts trailing CR/LF in an iterator item, but another
            // unquoted record belongs in a separate item. Validate before any
            // row is published, after this field's numeric conversion/errors.
            for (var offset = 0; offset < remainder.Length;)
            {
                _context.CheckExecution(_span);
                var count = Math.Min(4096, remainder.Length - offset);
                if (remainder.Slice(offset, count).IndexOfAnyExcept((byte)'\r', (byte)'\n') >= 0)
                    throw CsvError("new-line character seen in unquoted field - do you need to open the file with newline=''?", _span);
                offset += count;
            }
        }

        private void EmitRecord(ReadOnlySpan<byte> source, CsvDictReaderObject? dictionary = null)
        {
            // Admit all ordinary field payloads once, while working bytes and
            // old capacities remain funded. The private array includes headroom
            // for adoption by csv.reader; each value keeps independent funding.
            var rowBytes = PyList.EstimateCapacityBytes(_fields.Count);
            var layout = _fields.Capacity <= ReusableFieldCapacity ? dictionary?.PrivateLayout(_fields.Count) : null;
            var shellBytes = layout is null ? 0 : PyDict.RecordKeys.CsvRecordShellBytes;
            var resultBytes = checked(_stringBytes + rowBytes + shellBytes);
            var governor = _context.MemoryGovernor;
            governor.Reserve(resultBytes, _span);
            try
            {
                _context.CheckExecution(_span);
                var row = _fields.Count == 0 ? Array.Empty<object>() : new object[_fields.Count];
                // The field estimate covers the final backing and every shell.
                // Windows never borrow the parser buffer or refund a sibling.
                var payload = _payloadBytes == 0 ? Array.Empty<byte>() : new byte[_payloadBytes];
                var payloadOffset = 0;
                var index = 0;
                foreach (var field in _fields)
                {
                    _context.CheckExecution(_span);
                    row[index++] = ReadField(source, field, payload, ref payloadOffset);
                }
                _context.CheckExecution(_span);
                object result = row;
                if (layout is not null)
                {
                    // Admission can expose or replace the header. A raw row
                    // falls back to the live DictReader protocol after the pull.
                    if (ReferenceEquals(layout, dictionary!.PrivateLayout(row.Length)))
                        result = layout.AdoptFundedCsvRecord(row, governor, _span);
                    else
                    {
                        resultBytes -= shellBytes;
                        governor.ReleaseReserved(shellBytes);
                        _context.CheckExecution(_span);
                    }
                }
                governor.Commit(resultBytes);
                _readyRecord = result;
            }
            catch { governor.ReleaseReserved(resultBytes); throw; }
            ResetRecord();
            ReleaseRowScratch();
        }

        private int UnpackRecord(ReadOnlySpan<byte> source, Span<ExecutableSlot> destination)
        {
            var count = _fields.Count;
            if (count == destination.Length)
            {
                // These slots already belong to the frame. Admit only the
                // escaping UTF-8 values and scalar headroom before writing.
                var bytes = checked(_stringBytes + RuntimeMemoryEstimates.ScalarStorageBytes * count);
                var governor = _context.MemoryGovernor;
                governor.Reserve(bytes, _span);
                try
                {
                    _context.CheckExecution(_span);
                    var payload = _payloadBytes == 0 ? Array.Empty<byte>() : new byte[_payloadBytes];
                    var payloadOffset = 0;
                    var index = count;
                    foreach (var field in _fields)
                    {
                        _context.CheckExecution(_span);
                        // A parsed number has no text range. All short text
                        // can stay in the admitted slot until observed.
                        destination[--index] = field.Length is > 0 and <= ExecutableSlot.ShortUtf8Capacity
                            ? ExecutableSlot.FromShortUtf8(source.Slice(field.Start, field.Length), governor)
                            : ExecutableSlot.FromObject(ReadField(source, field, payload, ref payloadOffset));
                    }
                    _context.CheckExecution(_span);
                    governor.Commit(bytes);
                }
                catch
                {
                    destination.Clear();
                    governor.ReleaseReserved(bytes);
                    throw;
                }
            }

            // The caller diagnoses arity at the unpack instruction, after
            // the row has been consumed and the parser is ready to continue.
            ResetRecord();
            ReleaseRowScratch();
            return count;
        }

        private object ReadField(ReadOnlySpan<byte> source, CsvField field, byte[] payload, ref int payloadOffset)
        {
            if (field.IsNumber) return field.Number;
            if (field.Length == 0) return PyString.Empty;
            var text = field.Length <= PyString.InlineUtf8Capacity
                ? PyString.CreateReservedUtf8(field.Length, _context.MemoryGovernor) : null;
            var bytes = text is null ? payload.AsSpan(payloadOffset, field.Length) : text.UnpublishedUtf8;
            for (var offset = 0; offset < bytes.Length;)
            {
                _context.CheckExecution(_span);
                var count = Math.Min(4096, bytes.Length - offset);
                source.Slice(field.Start + offset, count).CopyTo(bytes.Slice(offset, count));
                offset += count;
            }
            if (text is null)
            {
                text = PyString.CreateReservedUtf8Window(payload, payloadOffset, field.Length, _context.MemoryGovernor);
                payloadOffset += field.Length;
            }
            else text.CompleteUnpublishedUtf8(field.Length);
            return text;
        }

        // Drops the in-progress record after a failed feed so the next pull
        // starts clean. Completed result ownership is independent of scratch.
        public void ResetRecord()
        {
            ResetFields();
            _record.Truncate(0);
            _fieldStart = 0;
            _state = FieldState.RecordStart;
            _fieldWasQuoted = false;
        }

        private void ResetFields()
        {
            _fields.Clear();
            _stringBytes = 0;
            _payloadBytes = 0;
        }

        private void ReleaseRowScratch()
        {
            if (_fields.Capacity > ReusableFieldCapacity)
            {
                var before = _fields.Capacity;
                _fields.TrimExcess();
                _context.MemoryGovernor.Release(16L * (before - _fields.Capacity));
            }
        }

        internal void ReleaseScratch()
        {
            _record.Release();
            var before = StorageBytes;
            _fields.Clear();
            _fields.Capacity = 0;
            _fieldStart = 0;
            _stringBytes = 0;
            _payloadBytes = 0;
            _context.MemoryGovernor.Release(before - StorageBytes);
        }

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
