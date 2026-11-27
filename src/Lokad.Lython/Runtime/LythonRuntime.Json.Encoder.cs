using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Native instances have one mutable attribute table. Guest instances use
    // their ordinary attributes; a bound engine method owns no mirrored state.
    internal sealed class JsonEncoderObject : IPyTruthyValue, IPyRenderableValue, IPyMutableDynamicAttributes
    {
        internal JsonEncoderObject() { Attributes = new(StringComparer.Ordinal); }
        internal JsonEncoderObject(PyInstance owner) { GuestOwner = owner; }
        internal Dictionary<string, object>? Attributes { get; }
        internal PyInstance? GuestOwner { get; }
        private object Owner => (object?)GuestOwner ?? this;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => JsonEncoderMembers.TryGetMember(this, name, out value);
        public bool TrySetMember(string name, object value)
        {
            if (Attributes is null || name is not ("skipkeys" or "ensure_ascii" or "check_circular" or "allow_nan" or "sort_keys" or "indent" or "item_separator" or "key_separator" or "default")) return false;
            Attributes[name] = value;
            return true;
        }

        private ValueTask<object> ReadAsync(string name, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            => ReadWrapperAttributeAsync(Owner, name, context, span, asynchronous);

        private async ValueTask<JsonDumpOptions> GetDumpOptionsAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous, bool oneShot)
        {
            var circular = await JsonOptionTruthAsync(await ReadAsync("check_circular", context, span, asynchronous).ConfigureAwait(false), context, span, asynchronous).ConfigureAwait(false);
            var ascii = await JsonOptionTruthAsync(await ReadAsync("ensure_ascii", context, span, asynchronous).ConfigureAwait(false), context, span, asynchronous).ConfigureAwait(false);
            var allowNan = await ReadAsync("allow_nan", context, span, asynchronous).ConfigureAwait(false);
            var indent = JsonModule.ParseJsonIndent(await ReadAsync("indent", context, span, asynchronous).ConfigureAwait(false), span, context);
            var callback = await ReadAsync("default", context, span, asynchronous).ConfigureAwait(false);
            var key = JsonModule.RequireJsonSeparator(await ReadAsync("key_separator", context, span, asynchronous).ConfigureAwait(false), span);
            var item = JsonModule.RequireJsonSeparator(await ReadAsync("item_separator", context, span, asynchronous).ConfigureAwait(false), span);
            var sort = await ReadAsync("sort_keys", context, span, asynchronous).ConfigureAwait(false);
            var skip = await ReadAsync("skipkeys", context, span, asynchronous).ConfigureAwait(false);
            if (oneShot)
            {
                sort = RuntimeValue(await JsonOptionTruthAsync(sort, context, span, asynchronous).ConfigureAwait(false));
                skip = RuntimeValue(await JsonOptionTruthAsync(skip, context, span, asynchronous).ConfigureAwait(false));
                allowNan = RuntimeValue(await JsonOptionTruthAsync(allowNan, context, span, asynchronous).ConfigureAwait(false));
            }
            return new(skip, ascii, circular, allowNan, indent, item, key, callback, sort);
        }

        public bool IsTruthy() => true;
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<json.encoder.JSONEncoder object>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Encode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => EncodeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> EncodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => EncodeCoreAsync(arguments, span, context, true);
        private ValueTask<object> EncodeCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1) throw RuntimeErrors.Type("JSONEncoder.encode() takes exactly one argument", span);
            return EncodeValueAsync(arguments[0], span, context, asynchronous);
        }

        // Native module calls already own their value; public/guest method
        // invocation retains its ordinary argument and frame checks.
        internal async ValueTask<object> EncodeValueAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (value is PyString text)
            {
                var ascii = await JsonOptionTruthAsync(await ReadAsync("ensure_ascii", context, span, asynchronous).ConfigureAwait(false), context, span, asynchronous).ConfigureAwait(false);
                return await JsonEncodeIterator.EncodeToStringAsync(text, JsonDumpOptions.Default with { EnsureAscii = ascii }, context, span, asynchronous).ConfigureAwait(false);
            }
            if (GuestOwner is not null)
            {
                var method = await ReadAsync("iterencode", context, span, asynchronous).ConfigureAwait(false);
                CallArgumentValue[] call = [CallArgumentValue.Positional(value), CallArgumentValue.Keyword("_one_shot", true)];
                var chunks = asynchronous
                    ? await InvokeCallableTargetAsync(method, span, span, context, () => ValueTask.FromResult(call)).ConfigureAwait(false)
                    : InvokeCallableTarget(method, span, span, context, call);
                return asynchronous ? await StringMembers.JoinAsync(PyString.Empty, chunks, span, context).ConfigureAwait(false)
                    : StringMembers.Join(PyString.Empty, chunks, span, context);
            }
            var options = await GetDumpOptionsAsync(context, span, asynchronous, true).ConfigureAwait(false);
            return await JsonEncodeIterator.EncodeToStringAsync(value, options, context, span, asynchronous).ConfigureAwait(false);
        }

        public object IterEncode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => IterEncodeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> IterEncodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => IterEncodeCoreAsync(arguments, span, context, true);
        private async ValueTask<object> IterEncodeCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length is < 1 or > 2) throw RuntimeErrors.Type("JSONEncoder.iterencode() expects an object and optional _one_shot", span);
            context.CheckExecution(span);
            var oneShot = arguments.Length == 2 && await JsonOptionTruthAsync(arguments[1], context, span, asynchronous).ConfigureAwait(false);
            var options = await GetDumpOptionsAsync(context, span, asynchronous, oneShot).ConfigureAwait(false);
            if (oneShot)
            {
                var text = await JsonEncodeIterator.EncodeToStringAsync(arguments[0], options, context, span, asynchronous).ConfigureAwait(false);
                return new PyTuple(new object[] { text }, context.MemoryGovernor, span);
            }
            return JsonEncodeIterator.Create(arguments[0], options, context, span);
        }

        public static object Default(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1) throw RuntimeErrors.Type("JSONEncoder.default() takes exactly one argument", span);
            throw RuntimeErrors.Type("Object of type " + UnboundTypeMethod.PythonTypeName(arguments[0], context) + " is not JSON serializable", span);
        }
    }

    internal sealed class JsonEncoderClass : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public static readonly JsonEncoderClass Instance = new();
        private JsonEncoderClass() { }
        public string Name => "json.JSONEncoder";
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" or "__qualname__" => PyString.FromString("JSONEncoder"),
                "__module__" => PyString.FromString("json.encoder"),
                "item_separator" => JsonDumpOptions.Default.ItemSeparator,
                "key_separator" => JsonDumpOptions.Default.KeySeparator,
                _ => MissingMemberValue.Instance
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => TryGetMember(name, out value);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ConstructAsync(arguments, null, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ConstructAsync(arguments, null, span, context, true);

        internal static ValueTask<object> ConstructAsync(CallArgumentValue[] arguments, PyInstance? guest, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            object skip = false, ascii = true, circular = true, nan = true, sort = false;
            object indent = PyNone.Instance, separators = PyNone.Instance, callback = PyNone.Instance;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional) throw RuntimeErrors.Type("JSONEncoder.__init__() accepts keyword arguments only", span);
                switch (argument.KeywordName)
                {
                    case "skipkeys": skip = argument.Value; break;
                    case "ensure_ascii": ascii = argument.Value; break;
                    case "check_circular": circular = argument.Value; break;
                    case "allow_nan": nan = argument.Value; break;
                    case "sort_keys": sort = argument.Value; break;
                    case "indent": indent = argument.Value; break;
                    case "separators": separators = argument.Value; break;
                    case "default": callback = argument.Value; break;
                    default: throw RuntimeErrors.Type("JSONEncoder.__init__() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
                }
            }
            return InitializeAsync(guest is null ? new JsonEncoderObject() : guest,
                skip, ascii, circular, nan, sort, indent, separators, callback, span, context, asynchronous);
        }

        private static async ValueTask<object> InitializeAsync(object instance, object skip, object ascii,
            object circular, object nan, object sort, object indent, object separatorValue, object callback,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            await AssignWrapperAttributeAsync(instance, "skipkeys", skip, context, span, asynchronous).ConfigureAwait(false);
            await AssignWrapperAttributeAsync(instance, "ensure_ascii", ascii, context, span, asynchronous).ConfigureAwait(false);
            await AssignWrapperAttributeAsync(instance, "check_circular", circular, context, span, asynchronous).ConfigureAwait(false);
            await AssignWrapperAttributeAsync(instance, "allow_nan", nan, context, span, asynchronous).ConfigureAwait(false);
            await AssignWrapperAttributeAsync(instance, "sort_keys", sort, context, span, asynchronous).ConfigureAwait(false);
            await AssignWrapperAttributeAsync(instance, "indent", indent, context, span, asynchronous).ConfigureAwait(false);
            if (separatorValue is not PyNone)
            {
                var separators = await JsonModule.ParseJsonSeparatorsAsync(separatorValue, span, context, asynchronous).ConfigureAwait(false);
                await AssignWrapperAttributeAsync(instance, "item_separator", separators.ItemSeparator, context, span, asynchronous).ConfigureAwait(false);
                await AssignWrapperAttributeAsync(instance, "key_separator", separators.KeySeparator, context, span, asynchronous).ConfigureAwait(false);
            }
            else if (indent is not PyNone)
                await AssignWrapperAttributeAsync(instance, "item_separator", JsonModule.PrettyItemSeparator, context, span, asynchronous).ConfigureAwait(false);
            if (callback is not PyNone)
                await AssignWrapperAttributeAsync(instance, "default", callback, context, span, asynchronous).ConfigureAwait(false);
            return instance;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class 'json.JSONEncoder'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal static class JsonEncoderMembers
    {
        public static bool TryGetMember(JsonEncoderObject encoder, string name, [MaybeNullWhen(false)] out object value)
        {
            if (encoder.Attributes is not null && encoder.Attributes.TryGetValue(name, out value!)) return true;
            value = name switch
            {
                "encode" => BoundCallable.Create(encoder.Encode, LythonKnownCallableSignatures.Json.EncoderEncode, encoder.EncodeAsync),
                "iterencode" => BoundCallable.Create(encoder.IterEncode, LythonKnownCallableSignatures.Json.EncoderIterencode, encoder.IterEncodeAsync),
                "default" => BoundCallable.Create(JsonEncoderObject.Default, LythonKnownCallableSignatures.Json.EncoderDefault),
                "item_separator" => JsonDumpOptions.Default.ItemSeparator,
                "key_separator" => JsonDumpOptions.Default.KeySeparator,
                _ => MissingMemberValue.Instance
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    // Genuinely incremental encoder traversal: an explicit-stack machine over
    // the same rendering helpers as encode(), so joined chunks equal encode()
    // output exactly while each advance visits only what its chunk needs. A
    // container opener is consumable before later elements (and their default
    // hooks) are visited. The iterator owns reusable UTF-8 scratch; public chunks
    // are independent. Sort scratch ends when its frame is relinquished.
    internal sealed class JsonEncodeIterator : PyIteratorBase, IPyAsyncIteratorValue
    {
        private const long StorageBytes = 224;

        private struct EmitFrame(object container, int depth)
        {
            public bool IsDefault;
            public readonly object Container = container;
            public IReadOnlyList<object>? Sequence;
            public PyDict.ItemCursor DictPairs;
            public List<(object OriginalKey, object Value)>? SortedEntries;
            public int SortedIndex;
            public int Index;
            public readonly int Depth = depth;
            public long ScratchBytes;
            public bool Opened;
        }

        private readonly JsonDumpOptions _options;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private EmitTraversal _traversal;
        private bool _finished;
        private GovernedByteBuilder? _builder;
        private int _advancing;

        private JsonEncodeIterator(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            _traversal = new EmitTraversal(value);
            _options = options;
            _context = context;
            _span = span;
        }

        internal static JsonEncodeIterator Create(object value, JsonDumpOptions options, ExecutionContext context,
            LythonSourceSpan span)
        {
            var governor = context.MemoryGovernor;
            governor.Reserve(StorageBytes, span);
            JsonEncodeIterator iterator;
            try
            {
                context.CheckExecution(span);
                iterator = new JsonEncodeIterator(value, options, context, span);
            }
            catch
            {
                governor.ReleaseReserved(StorageBytes);
                throw;
            }
            governor.Commit(StorageBytes);
            return iterator;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<json.encoder._iterencode object>");
        }

        public override PyIterationResult TryMoveNext()
        {
            return TryMoveNextCoreAsync(false).GetAwaiter().GetResult();
        }

        public ValueTask<PyIterationResult> TryMoveNextAsync() => TryMoveNextCoreAsync(true);

        internal async ValueTask WriteToAsync(ExecutionContext.TextFileHandle target, LythonSourceSpan span, bool asynchronous)
            => _ = await TryMoveNextCoreAsync(asynchronous, target, span).ConfigureAwait(false);

        private async ValueTask<PyIterationResult> TryMoveNextCoreAsync(bool asynchronous,
            ExecutionContext.TextFileHandle? target = null, LythonSourceSpan? writeSpan = null)
        {
            if (Interlocked.CompareExchange(ref _advancing, 1, 0) != 0)
            {
                _context.CheckExecution(_span);
                throw new LythonRuntimeException("ValueError", "generator already executing", _span);
            }
            try
            {
                while (true)
                {
                    try
                    {
                        _context.CheckExecution(_span);
                        if (_finished) return PyIterationResult.End;
                        var builder = _builder ??= new GovernedByteBuilder(_context.MemoryGovernor, _span);
                        builder.Clear();
                        if (!await EmitAsync(this, null, builder, _options, _context, _span, asynchronous).ConfigureAwait(false))
                        {
                            Finish();
                            return PyIterationResult.End;
                        }

                        // Public/custom chunks must survive later advances independently.
                        if (target is null) return PyIterationResult.Yield(builder.ToPyString());
                    }
                    catch (InvalidOperationException ex)
                    {
                        Finish();
                        throw new LythonRuntimeException("TypeError", ex.Message, _span);
                    }
                    catch
                    {
                        Finish();
                        throw;
                    }
                    // Consumer failure leaves the iterator resumable. The guard and
                    // admitted builder remain live until this native copy finishes.
                    await InvokeFileWriteChunkAsync(target, _builder!.WrittenMemory, writeSpan ?? _span, _context, asynchronous).ConfigureAwait(false);
                }
            }
            finally { Volatile.Write(ref _advancing, 0); }
        }

        internal static async ValueTask<object> EncodeToStringAsync(object value, JsonDumpOptions options,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            try
            {
                context.CheckExecution(span);
                var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
                try
                {
                    await EmitAsync(null, value, builder, options, context, span, asynchronous).ConfigureAwait(false);
                    return builder.ToPyStringAndRelease();
                }
                finally { builder.Release(); }
            }
            catch (InvalidOperationException error)
            {
                throw new LythonRuntimeException("TypeError", error.Message, span);
            }
        }

        private void Finish()
        {
            _finished = true;
            _builder?.Release();
            _builder = null;
            _traversal.Release(_context);
        }

        internal void FinishPrivate()
        {
            try { Finish(); }
            finally { _context.MemoryGovernor.Release(StorageBytes); }
        }

        private static ref EmitTraversal GetTraversal(JsonEncodeIterator? iterator, ref EmitTraversal eager)
            => ref (iterator is null ? ref eager : ref iterator._traversal);

        // A pending render owns its inline frame and callback state until
        // the last await and cleanup complete. Boolean options and the
        // builtin error-only default do not require this temporary storage.
        private static void AdmitContinuation(ref long reserved, ExecutionContext context, LythonSourceSpan span)
        {
            if (reserved != 0) return;
            const long ContinuationBytes = 512;
            context.MemoryGovernor.Reserve(ContinuationBytes, span);
            reserved = ContinuationBytes;
        }

        private static async ValueTask<bool> EmitAsync(JsonEncodeIterator? iterator, object? eagerValue,
            GovernedByteBuilder builder, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span,
            bool asynchronous)
        {
            var eager = new EmitTraversal(eagerValue);
            var stopAtChunk = iterator is not null;
            var builtinDefault = ReferenceEquals(options, JsonDumpOptions.Default);
            long reserved = 0;
            try
            {
                while (true)
                {
                    context.CheckExecution(span);
                    ref var traversal = ref GetTraversal(iterator, ref eager);
                    var value = traversal.PendingValue;
                    traversal.PendingValue = null;
                    var frameIndex = traversal.FrameCount - 1;
                    var depth = frameIndex < 0 ? 0 : traversal.FrameAt(frameIndex).Depth + 1;
                    if (value is null)
                    {
                        if (frameIndex < 0) return false;
                        ref var frame = ref traversal.FrameAt(frameIndex);
                        if (frame.IsDefault)
                        {
                            traversal.CloseFrame(context);
                            continue;
                        }
                        if (!frame.Opened)
                        {
                            traversal.OpenFrame(frameIndex, span);
                            builder.Append(frame.Sequence is not null ? (byte)'[' : (byte)'{');
                        }
                        else if (frame.Sequence is { } sequence)
                        {
                            var index = frame.Index;
                            if (index >= sequence.Count)
                            {
                                traversal.CloseFrame(context);
                                if (index > 0)
                                    JsonModule.AppendJsonIndent(builder, options.IndentUnit, depth - 1, context, span);
                                builder.Append((byte)']');
                            }
                            else
                            {
                                value = sequence[index];
                                if (index > 0) JsonModule.AppendSeparator(builder, options.ItemSeparator, context, span);
                                JsonModule.AppendJsonIndent(builder, options.IndentUnit, depth, context, span);
                                frame.Index++;
                            }
                        }
                        else
                        {
                            (object Key, object Value) pair;
                            bool found;
                            while (true)
                            {
                                context.CheckExecution(span);
                                found = GetTraversal(iterator, ref eager).TryTakePair(frameIndex, context, span, out pair);
                                if (!found || JsonModule.IsSupportedJsonObjectKey(pair.Key)) break;
                                if (asynchronous && options.SkipKeys is not bool)
                                    AdmitContinuation(ref reserved, context, span);
                                if (!await JsonOptionTruthAsync(options.SkipKeys, context, span, asynchronous).ConfigureAwait(false))
                                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                            }
                            var index = GetTraversal(iterator, ref eager).FrameAt(frameIndex).Index;
                            if (!found)
                            {
                                GetTraversal(iterator, ref eager).CloseFrame(context);
                                if (index > 0)
                                    JsonModule.AppendJsonIndent(builder, options.IndentUnit, depth - 1, context, span);
                                builder.Append((byte)'}');
                            }
                            else
                            {
                                if (index > 0) JsonModule.AppendSeparator(builder, options.ItemSeparator, context, span);
                                JsonModule.AppendJsonIndent(builder, options.IndentUnit, depth, context, span);
                                if (asynchronous && options.AllowNan is not bool && pair.Key is double key && !double.IsFinite(key))
                                    AdmitContinuation(ref reserved, context, span);
                                await JsonModule.AppendJsonObjectKeyAsync(builder, pair.Key, options, context, span, asynchronous).ConfigureAwait(false);
                                JsonModule.AppendSeparator(builder, options.KeySeparator, context, span);
                                GetTraversal(iterator, ref eager).FrameAt(frameIndex).Index++;
                                value = pair.Value;
                            }
                        }
                    }

                    var chunkReady = true;
                    for (var defaultDepth = 0; value is not null; defaultDepth++)
                    {
                        context.CheckExecution(span);
                        if (depth >= ExecutionState.MaxInterpreterDepth || defaultDepth >= ExecutionState.MaxInterpreterDepth)
                            throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded while encoding a JSON object",
                                span, LythonFailureReason.InterpreterStackLimit, ExecutionState.MaxInterpreterDepth,
                                Math.Max(depth, defaultDepth));

                        switch (value)
                        {
                            case PyNone:
                                builder.Append("null"u8);
                                break;
                            case PyString text:
                                JsonModule.AppendJsonString(builder, text, options.EnsureAscii, context, span);
                                break;
                            case bool boolean:
                                builder.Append(boolean ? "true"u8 : "false"u8);
                                break;
                            case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                                context.CheckExecution(span);
                                JsonModule.AppendJsonInteger(builder, integer, context, span);
                                break;
                            case double floating:
                                if (asynchronous && options.AllowNan is not bool && !double.IsFinite(floating))
                                    AdmitContinuation(ref reserved, context, span);
                                await JsonModule.AppendJsonDoubleAsync(builder, floating, options, context, span, asynchronous).ConfigureAwait(false);
                                break;
                            case PyList list:
                                GetTraversal(iterator, ref eager).EnsureFrameSlot(options, context, span);
                                GetTraversal(iterator, ref eager).PushFrame(new EmitFrame(list, depth) { Sequence = list });
                                chunkReady = false;
                                break;
                            case object tupleLike when PyTupleLike.TryGetItems(tupleLike, out var tuple):
                                GetTraversal(iterator, ref eager).EnsureFrameSlot(options, context, span);
                                GetTraversal(iterator, ref eager).PushFrame(new EmitFrame(tupleLike, depth) { Sequence = tuple });
                                chunkReady = false;
                                break;
                            case PyDict or PyCounter or PyDefaultDict:
                                var dict = value switch
                                {
                                    PyCounter counter => counter.InnerDict,
                                    PyDefaultDict defaultDict => defaultDict.InnerDict,
                                    _ => (PyDict)value
                                };
                                var sorted = false;
                                if (dict.Count != 0)
                                {
                                    if (asynchronous && options.SortKeys is not bool)
                                        AdmitContinuation(ref reserved, context, span);
                                    sorted = await JsonOptionTruthAsync(options.SortKeys, context, span, asynchronous).ConfigureAwait(false);
                                }
                                GetTraversal(iterator, ref eager).PrepareDictFrame(dict, depth, sorted, options, context, span);
                                chunkReady = false;
                                break;
                            default:
                                if (stopAtChunk && builder.Length != 0)
                                {
                                    // Retain the selected value across a published prefix even
                                    // if the consumer changes the source container.
                                    GetTraversal(iterator, ref eager).PendingValue = value;
                                    break;
                                }
                                if (options.DefaultCallable is null)
                                    throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonModule.JsonValueTypeName(value)}");
                                if (options.CheckCircular)
                                {
                                    ref var current = ref GetTraversal(iterator, ref eager);
                                    current.EnsureFrameSlot(options, context, span);
                                    var markerIndex = current.FrameCount;
                                    current.PushFrame(new EmitFrame(value, depth) { IsDefault = true });
                                    current.OpenFrame(markerIndex, span);
                                }
                                if (asynchronous && !builtinDefault) AdmitContinuation(ref reserved, context, span);
                                value = await JsonModule.InvokeJsonCallbackCoreAsync(options.DefaultCallable, value,
                                    context, span, asynchronous && !builtinDefault).ConfigureAwait(false);
                                continue;
                        }
                        break;
                    }
                    if (stopAtChunk && chunkReady && builder.Length != 0) return true;
                }
            }
            finally
            {
                try { if (iterator is null) eager.Release(context); }
                finally { if (reserved != 0) context.MemoryGovernor.ReleaseReserved(reserved); }
            }
        }

        // Iterator frames survive across chunks. Eager frames have the same
        // lifetime rules, but are owned by the invocation of EmitAsync.
        private struct EmitTraversal(object? value)
        {
            private EmitFrame _outerFrame;
            private EmitFrame[] _nestedFrames = [];
            private HashSet<object>? _nestedMarkers;
            internal object? PendingValue = value;
            internal int FrameCount;

            [UnscopedRef]
            internal ref EmitFrame FrameAt(int index)
                => ref (index == 0 ? ref _outerFrame : ref _nestedFrames[index - 1]);

            internal void PushFrame(EmitFrame frame)
            {
                FrameAt(FrameCount) = frame;
                FrameCount++;
            }

            private static long EstimateFrameStorage(int capacity) => capacity == 0 ? 0 : 96L + 128L * capacity;

            internal void EnsureFrameSlot(JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
            {
                if (FrameCount <= _nestedFrames.Length) return;
                var capacity = _nestedFrames.Length == 0 ? 4 : checked(_nestedFrames.Length * 2);
                // Admit the replacement array and its cycle markers while the current
                // backing is live. At most one marker is retained for each frame.
                var bytes = EstimateFrameStorage(capacity);
                var governor = context.MemoryGovernor;
                governor.Reserve(bytes, span);
                EmitFrame[] expanded;
                try
                {
                    context.CheckExecution(span);
                    expanded = new EmitFrame[capacity];
                    for (var offset = 0; offset < _nestedFrames.Length; offset += 256)
                    {
                        context.CheckExecution(span);
                        Array.Copy(_nestedFrames, offset, expanded, offset, Math.Min(256, _nestedFrames.Length - offset));
                    }
                    if (options.CheckCircular)
                    {
                        _nestedMarkers ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
                        _nestedMarkers.EnsureCapacity(capacity);
                    }
                }
                catch
                {
                    governor.ReleaseReserved(bytes);
                    throw;
                }
                var priorBytes = EstimateFrameStorage(_nestedFrames.Length);
                governor.Commit(bytes);
                _nestedFrames = expanded;
                governor.Release(priorBytes);
            }

            internal void PrepareDictFrame(PyDict dict, int depth, bool sorted,
                JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
            {
                // Reserve the frame slot before retaining an item cursor or sorted pairs.
                EnsureFrameSlot(options, context, span);
                var frame = new EmitFrame(dict, depth);
                if (!sorted)
                {
                    frame.DictPairs = new PyDict.ItemCursor(dict);
                    PushFrame(frame);
                    return;
                }

                var bytes = checked(32L * dict.Count);
                var governor = context.MemoryGovernor;
                governor.Reserve(bytes, span);
                try
                {
                    frame.SortedEntries = JsonModule.CollectSortedJsonEntries(dict, context, span);
                }
                catch
                {
                    governor.ReleaseReserved(bytes);
                    throw;
                }
                governor.Commit(bytes);
                frame.ScratchBytes = bytes;
                PushFrame(frame);
            }

            internal void OpenFrame(int frameIndex, LythonSourceSpan span)
            {
                ref var frame = ref FrameAt(frameIndex);
                // The outer frame is already its own marker. Nested frames
                // keep the existing constant-time membership test.
                if (frameIndex != 0 && _nestedMarkers is not null
                    && (ReferenceEquals(_outerFrame.Container, frame.Container) || !_nestedMarkers.Add(frame.Container)))
                    throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
                frame.Opened = true;
            }

            internal void CloseFrame(ExecutionContext context)
            {
                var frameIndex = --FrameCount;
                ref var frame = ref FrameAt(frameIndex);
                var bytes = frame.ScratchBytes;
                var pairs = frame.DictPairs;
                if (frameIndex != 0 && frame.Opened) _nestedMarkers?.Remove(frame.Container);
                frame = default;
                pairs.Dispose();
                context.MemoryGovernor.Release(bytes);
            }

            internal void Release(ExecutionContext context)
            {
                PendingValue = null;
                while (FrameCount != 0) CloseFrame(context);
                var bytes = EstimateFrameStorage(_nestedFrames.Length);
                _nestedFrames = [];
                _nestedMarkers = null;
                context.MemoryGovernor.Release(bytes);
            }

            internal bool TryTakePair(int frameIndex, ExecutionContext context, LythonSourceSpan span,
                out (object Key, object Value) pair)
            {
                ref var frame = ref FrameAt(frameIndex);
                if (frame.SortedEntries is null)
                {
                    var found = frame.DictPairs.MoveNext(out var item, context, span);
                    pair = (item.Key, item.Value);
                    return found;
                }
                if (frame.SortedIndex < frame.SortedEntries.Count)
                {
                    pair = frame.SortedEntries[frame.SortedIndex++];
                    return true;
                }
                pair = default;
                return false;
            }
        }
    }
}
