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
                var records = new CsvRecordSource(args[0] as PyList, cursor, args[0] as ExecutionContext.TextFileHandle, format, span, context);
                cursor = null;
                return dictionary ? new CsvDictReaderObject(records, fields, args[2], args[3], context, span) : new CsvReaderObject(records);
            }
            finally { if (cursor is not null) await cursor.DisposeAsync().ConfigureAwait(false); }
        }

        private static CsvWriterObject CreateWriter(CsvOptions options, object? write, LythonSourceSpan span, ExecutionContext context)
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
                var bytes = value.Utf8Bytes.Span;
                if (bytes.Length == 1 && (bytes[0] is 10 or 13 || bytes[0] == 32 && !allowSpace))
                    throw new LythonRuntimeException("ValueError", $"bad {name} value", span);
                if (CsvWriterMembers.TerminatorContains(terminator.Utf8Bytes.Span, bytes, context, span))
                    throw new LythonRuntimeException("ValueError", $"bad {name} or lineterminator value", span);
            }
            void ValidatePair(PyString? left, string leftName, PyString? right, string rightName)
            {
                if (left is not null && right is not null && left.Utf8Bytes.Span.SequenceEqual(right.Utf8Bytes.Span))
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
        private ExecutionContext.TextFileHandle? _nativeFile;
        private int _position;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private bool _completed;

        // Source/parser objects, builders and list shells; backing is charged
        // from actual capacities, independently of emitted values.
        internal const long ShellBytes = 512;
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

        internal bool TryMoveNext(out object[] row)
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

        internal async ValueTask<object[]?> TryMoveNextAsync()
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

                if (_nativeFile is not null)
                {
                    var before = _parser.InputBuffer.Length;
                    bool read;
                    try { read = await _nativeFile.ReadLineIntoAsync(_parser.InputBuffer).ConfigureAwait(false); }
                    catch { _parser.ResetRecord(); throw; }
                    if (!read) await FinishExhaustedAsync().ConfigureAwait(false);
                    else FeedNativeLine(before);
                    continue;
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
                    _parser.ResetRecord();
                    throw new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
                }
                catch { _parser.ResetRecord(); throw; }

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
            if (_nativeFile is not null)
            {
                var before = _parser.InputBuffer.Length;
                bool read;
                try { read = _nativeFile.ReadLineInto(_parser.InputBuffer); }
                catch { _parser.ResetRecord(); throw; }
                if (!read) FinishExhausted();
                else FeedNativeLine(before);
                return;
            }
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
                    _parser.ResetRecord();
                    throw new LythonRuntimeException("RuntimeError", "csv source mutated during iteration.", _span);
                }
                catch { _parser.ResetRecord(); throw; }

                if (!moved)
                {
                    FinishExhausted();
                    return;
                }

            }

            FeedPulledLine(current);
        }

        private void FeedNativeLine(int before)
        {
            _context.CheckExecution(_span);
            PhysicalLineCount++;
            try { _parser.FeedOwnedLine(before); }
            catch { _parser.ResetRecord(); throw; }
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
                _parser.Feed(line.Utf8Bytes.Span);
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
                    _nativeFile = null;
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
        private readonly List<CsvField> _fields = new();
        private readonly GovernedByteBuilder _record;
        private int _fieldStart;
        private long _stringBytes;
        private object[]? _readyRecord;
        private enum FieldState { RecordStart, FieldStart, Bare, Quoted, AfterQuote }
        private FieldState _state;
        private bool _fieldWasQuoted;

        // Field descriptors and native record bytes are private working storage.
        // Returned strings acquire independent payloads before publication.
        private readonly struct CsvField(int start, int length, object? converted)
        {
            internal readonly int Start = start, Length = length;
            internal readonly object? Converted = converted;
        }

        internal long StorageBytes => checked(16L * _fields.Capacity + _record.CommittedCapacity);
        internal GovernedByteBuilder InputBuffer => _record;

        internal void FeedOwnedLine(int before)
        {
            var line = _record.WrittenSpan[before..];
            _record.Truncate(before);
            // CSV decoding only removes syntax bytes. Writes stay behind the
            // consumed input, and the already-admitted backing cannot grow.
            // Truncate/FinishRecord retain it until this Feed has completed.
            Feed(line);
        }

        public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            _options = options;
            _context = context;
            _span = span;
            _record = new GovernedByteBuilder(context.MemoryGovernor, span, 16);
        }

        // One source item completes at most one row. No subsequent item is
        // pulled until this independently funded result has been transferred.
        public bool TryTakeReady(out object[] row)
        {
            if (_readyRecord is null)
            {
                row = null!;
                return false;
            }
            row = _readyRecord;
            _readyRecord = null;
            return true;
        }

        public void Feed(ReadOnlySpan<byte> text)
        {
            if (_readyRecord is not null) throw new InvalidOperationException("CSV input advanced before consuming its record.");
            var delimiter = _options.Delimiter.Utf8Bytes.Span;
            var quote = _options.Quoting != CsvQuotingMode.None && _options.QuoteChar is { } q
                ? q.Utf8Bytes.Span : default;
            var escape = _options.EscapeChar is { } e ? e.Utf8Bytes.Span : default;
            // Lead bytes find candidates; full scalar matches below distinguish
            // Unicode dialect characters sharing the same UTF-8 prefix.
            Span<byte> unquoted = stackalloc byte[5];
            unquoted[0] = (byte)'\r'; unquoted[1] = (byte)'\n'; unquoted[2] = delimiter[0];
            var bareCount = 3;
            if (!escape.IsEmpty) unquoted[bareCount++] = escape[0];
            var startCount = bareCount;
            if (!quote.IsEmpty) unquoted[startCount++] = quote[0];
            Span<byte> quoted = stackalloc byte[2];
            var quotedCount = 0;
            if (!quote.IsEmpty) quoted[quotedCount++] = quote[0];
            if (!escape.IsEmpty) quoted[quotedCount++] = escape[0];

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

                var candidates = _state == FieldState.Quoted ? quoted[..quotedCount]
                    : unquoted[..(_state == FieldState.FieldStart ? startCount : bareCount)];
                var found = text.Slice(i, width).IndexOfAny(candidates);
                var ordinary = found < 0 ? width : found;
                if (ordinary != 0)
                {
                    if (_state == FieldState.AfterQuote) throw CsvError("Invalid csv input.", _span);
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
                        i = ConsumeEscape(text, i, escape);
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
                    FinishRecord(text[(i + 1)..]);
                    _context.CheckExecution(_span);
                    return;
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
                    if (_state == FieldState.AfterQuote) throw CsvError("Invalid csv input.", _span);
                    if (!escape.IsEmpty && remaining.StartsWith(escape)) i = ConsumeEscape(text, i, escape);
                    else
                    {
                        _record.Append(text.Slice(i, 1));
                        _state = FieldState.Bare;
                        i++;
                    }
                }
            }
            _context.CheckExecution(_span);
            if (_state == FieldState.Quoted) return;
            if (text.IsEmpty)
            {
                FinishRecord();
                return;
            }
            if (text[^1] != (byte)'\n' && text[^1] != (byte)'\r') FinishRecord();
        }

        public void Finish()
        {
            if (_state == FieldState.Quoted) throw CsvError("Invalid csv input.", _span);
        }

        private int ConsumeEscape(ReadOnlySpan<byte> text, int index, ReadOnlySpan<byte> escape)
        {
            index += escape.Length;
            if (index >= text.Length)
            {
                if (_options.Strict) throw CsvError("Invalid csv input.", _span);
                _record.Append(escape);
                return index;
            }
            // Consume one Python character, including a supplementary scalar.
            Rune.DecodeFromUtf8(text[index..], out _, out var width);
            _record.Append(text.Slice(index, width));
            if (_state == FieldState.FieldStart) _state = FieldState.Bare;
            return index + width;
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
            var length = _record.Length - _fieldStart;
            object? converted = null;
            if (_options.Quoting == CsvQuotingMode.NonNumeric && !_fieldWasQuoted && length != 0)
            {
                // Conversion must still fail at this field, before another
                // source line can be fetched or later field syntax inspected.
                var payload = PyString.CopyFromUtf8(_record.WrittenSpan.Slice(_fieldStart, length),
                    _context.MemoryGovernor, _span);
                converted = ParseOwnedFloatText(payload, _context, _span);
            }
            EnsureListCapacity(_fields);
            _fields.Add(new CsvField(_fieldStart, length, converted));
            if (converted is null && length != 0)
                _stringBytes = checked(_stringBytes + PyString.EstimateApproximateBytes(length));
            _fieldStart = _record.Length;
            _state = FieldState.FieldStart;
            _fieldWasQuoted = false;
        }

        private void FinishRecord(ReadOnlySpan<byte> remainder = default)
        {
            if (!(_state == FieldState.RecordStart && _record.Length == _fieldStart && _fields.Count == 0))
                FinishField();
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
            // Admit all ordinary field payloads once, while working bytes and
            // old capacities remain funded. The private array includes headroom
            // for adoption by csv.reader; each value keeps independent funding.
            var rowBytes = PyList.EstimateCapacityBytes(_fields.Count);
            using var storage = _context.MemoryGovernor.ReserveTemporary(checked(_stringBytes + rowBytes), _span);
            _context.CheckExecution(_span);
            var row = _fields.Count == 0 ? Array.Empty<object>() : new object[_fields.Count];
            var index = 0;
            foreach (var field in _fields)
            {
                _context.CheckExecution(_span);
                object cell;
                if (field.Converted is { } converted) cell = converted;
                else if (field.Length == 0) cell = PyString.Empty;
                else
                {
                    var bytes = new byte[field.Length];
                    for (var offset = 0; offset < bytes.Length;)
                    {
                        _context.CheckExecution(_span);
                        var count = Math.Min(4096, bytes.Length - offset);
                        _record.WrittenSpan.Slice(field.Start + offset, count).CopyTo(bytes.AsSpan(offset, count));
                        offset += count;
                    }
                    cell = PyString.FromOwnedUtf8(bytes, _context.MemoryGovernor, _span, storage);
                }
                row[index++] = cell;
            }
            _context.CheckExecution(_span);
            _readyRecord = row;
            storage.Commit(rowBytes);
            _fields.Clear();
            _record.Truncate(0);
            _fieldStart = 0;
            _stringBytes = 0;
            ReleaseRowScratch();
            _state = FieldState.RecordStart;
        }

        // Drops the in-progress record after a failed feed so the next pull
        // starts clean. Completed result ownership is independent of scratch.
        public void ResetRecord()
        {
            _fields.Clear();
            _record.Truncate(0);
            _fieldStart = 0;
            _stringBytes = 0;
            _state = FieldState.RecordStart;
            _fieldWasQuoted = false;
        }

        private void ReleaseRowScratch()
        {
            if (_fields.Capacity > 1024)
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
