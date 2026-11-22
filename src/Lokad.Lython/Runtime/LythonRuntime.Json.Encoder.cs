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
                sort = await JsonOptionTruthAsync(sort, context, span, asynchronous).ConfigureAwait(false);
                skip = await JsonOptionTruthAsync(skip, context, span, asynchronous).ConfigureAwait(false);
                allowNan = await JsonOptionTruthAsync(allowNan, context, span, asynchronous).ConfigureAwait(false);
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
                return await JsonModule.SerializeJsonTextCoreAsync(text, JsonDumpOptions.Default with { EnsureAscii = ascii }, context, span, asynchronous).ConfigureAwait(false);
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
            return await JsonModule.SerializeJsonTextCoreAsync(value, options, context, span, asynchronous).ConfigureAwait(false);
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
                var text = await JsonModule.SerializeJsonTextCoreAsync(arguments[0], options, context, span, asynchronous).ConfigureAwait(false);
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
                "encode" => BoundCallable.Create(encoder.Encode, LythonKnownCallableSignatures.JsonEncoderEncode, encoder.EncodeAsync),
                "iterencode" => BoundCallable.Create(encoder.IterEncode, LythonKnownCallableSignatures.JsonEncoderIterencode, encoder.IterEncodeAsync),
                "default" => BoundCallable.Create(JsonEncoderObject.Default, LythonKnownCallableSignatures.JsonEncoderDefault),
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
    internal sealed class JsonEncodeIterator : PyIteratorBase
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
        private EmitFrame _outerFrame;
        private EmitFrame[] _nestedFrames = [];
        private int _frameCount;
        private HashSet<object>? _nestedMarkers;
        private object? _rootValue;
        private bool _finished;
        private GovernedByteBuilder? _builder;
        private int _advancing;

        private JsonEncodeIterator(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            _rootValue = value;
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

        public override ValueTask<PyIterationResult> TryMoveNextAsync() => TryMoveNextCoreAsync(true);

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
                        while (true)
                        {
                            _context.CheckExecution(_span);
                            var (advanced, chunkReady) = await AdvanceAsync(builder, asynchronous).ConfigureAwait(false);
                            if (!advanced)
                            {
                                Finish();
                                return PyIterationResult.End;
                            }
                            if (chunkReady && builder.Length != 0) break;
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
                    await InvokeFileWriteAsync(target, _builder!.WrittenMemory, writeSpan ?? _span, _context, asynchronous).ConfigureAwait(false);
                }
            }
            finally { Volatile.Write(ref _advancing, 0); }
        }

        // The eager iterator is private, so only exposed chunk iteration needs
        // the reentry guard. Its working storage covers suspended callbacks too.
        internal static async ValueTask WriteRootToBuilderAsync(object value, JsonDumpOptions options,
            GovernedByteBuilder builder, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var governor = context.MemoryGovernor;
            governor.Reserve(StorageBytes, span);
            try
            {
                context.CheckExecution(span);
                var iterator = new JsonEncodeIterator(value, options, context, span);
                try
                {
                    while (true)
                    {
                        context.CheckExecution(span);
                        var (advanced, _) = await iterator.AdvanceAsync(builder, asynchronous).ConfigureAwait(false);
                        if (!advanced) return;
                    }
                }
                finally { iterator.Finish(); }
            }
            finally { governor.ReleaseReserved(StorageBytes); }
        }

        private ref EmitFrame FrameAt(int index)
            => ref (index == 0 ? ref _outerFrame : ref _nestedFrames[index - 1]);

        private void PushFrame(EmitFrame frame)
        {
            FrameAt(_frameCount) = frame;
            _frameCount++;
        }

        private static long EstimateFrameStorage(int capacity) => capacity == 0 ? 0 : 96L + 128L * capacity;

        private void EnsureFrameSlot()
        {
            if (_frameCount <= _nestedFrames.Length) return;
            var capacity = _nestedFrames.Length == 0 ? 4 : checked(_nestedFrames.Length * 2);
            // Admit the replacement array and its cycle markers while the current
            // backing is live. At most one marker is retained for each frame.
            var bytes = EstimateFrameStorage(capacity);
            var governor = _context.MemoryGovernor;
            governor.Reserve(bytes, _span);
            EmitFrame[] expanded;
            try
            {
                _context.CheckExecution(_span);
                expanded = new EmitFrame[capacity];
                for (var offset = 0; offset < _nestedFrames.Length; offset += 256)
                {
                    _context.CheckExecution(_span);
                    Array.Copy(_nestedFrames, offset, expanded, offset, Math.Min(256, _nestedFrames.Length - offset));
                }
                if (_options.CheckCircular)
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

        private void Finish()
        {
            _finished = true;
            _rootValue = null;
            _builder?.Release();
            _builder = null;
            while (_frameCount != 0) CloseFrame();
            var bytes = EstimateFrameStorage(_nestedFrames.Length);
            _nestedFrames = [];
            _nestedMarkers = null;
            _context.MemoryGovernor.Release(bytes);
        }

        // Advances the machine; true means more work remains (with a complete
        // chunk when chunkReady). Structural order mirrors the eager renderer
        // exactly: separators, indent prefixes, keys and values concatenate to
        // the same text encode() produces.
        private ValueTask<(bool Advanced, bool ChunkReady)> AdvanceAsync(GovernedByteBuilder builder, bool asynchronous)
        {
            if (_rootValue is { } root)
            {
                _rootValue = null;
                return AdvanceValueAsync(root, 0, builder, asynchronous);
            }

            while (_frameCount != 0)
            {
                var frameIndex = _frameCount - 1;
                ref var frame = ref FrameAt(frameIndex);
                if (frame.IsDefault) { CloseFrame(); continue; }
                if (!frame.Opened)
                {
                    OpenFrame(ref frame, frameIndex);
                    builder.Append(frame.Sequence is not null ? (byte)'[' : (byte)'{');
                    return new((true, true));
                }

                if (frame.Sequence is { } sequence)
                {
                    var depth = frame.Depth;
                    var index = frame.Index;
                    if (index >= sequence.Count)
                    {
                        CloseFrame();
                        if (index > 0)
                            JsonModule.AppendJsonContainerSuffix(builder, _options, depth, _context, _span);
                        builder.Append((byte)']');
                        return new((true, true));
                    }

                    var element = sequence[index];
                    if (index > 0) JsonModule.AppendSeparator(builder, _options.ItemSeparator, _context, _span);
                    AppendPrettyPrefix(builder, depth + 1);
                    frame.Index++;
                    return AdvanceValueAsync(element, depth + 1, builder, asynchronous);
                }

                var pending = TryTakePairAsync(frameIndex, asynchronous);
                return pending.IsCompleted
                    ? AdvancePairAsync(frameIndex, pending.GetAwaiter().GetResult(), builder, asynchronous)
                    : CompletePairAsync(pending, frameIndex, builder, asynchronous);
            }
            return new((false, false));
        }

        private ValueTask<(bool Advanced, bool ChunkReady)> AdvancePairAsync(int frameIndex,
            (bool Found, object Key, object Value) pair, GovernedByteBuilder builder, bool asynchronous)
        {
            ref var frame = ref FrameAt(frameIndex);
            var depth = frame.Depth;
            var index = frame.Index;
            if (!pair.Found)
            {
                CloseFrame();
                if (index > 0)
                    JsonModule.AppendJsonContainerSuffix(builder, _options, depth, _context, _span);
                builder.Append((byte)'}');
                return new((true, true));
            }

            if (index > 0) JsonModule.AppendSeparator(builder, _options.ItemSeparator, _context, _span);
            AppendPrettyPrefix(builder, depth + 1);
            var pending = JsonModule.AppendJsonObjectKeyAsync(builder, pair.Key, _options, _context, _span, asynchronous);
            if (!pending.IsCompleted) return CompleteKeyAsync(pending, frameIndex, pair.Value, builder, asynchronous);
            pending.GetAwaiter().GetResult();
            return AdvancePairValueAsync(frameIndex, pair.Value, builder, asynchronous);
        }

        private ValueTask<(bool Advanced, bool ChunkReady)> AdvancePairValueAsync(int frameIndex,
            object value, GovernedByteBuilder builder, bool asynchronous)
        {
            JsonModule.AppendSeparator(builder, _options.KeySeparator, _context, _span);
            ref var frame = ref FrameAt(frameIndex);
            frame.Index++;
            return AdvanceValueAsync(value, frame.Depth + 1, builder, asynchronous);
        }

        private async ValueTask<(bool Advanced, bool ChunkReady)> CompletePairAsync(
            ValueTask<(bool Found, object Key, object Value)> pending, int frameIndex,
            GovernedByteBuilder builder, bool asynchronous)
            => await AdvancePairAsync(frameIndex, await pending.ConfigureAwait(false), builder, asynchronous).ConfigureAwait(false);

        private async ValueTask<(bool Advanced, bool ChunkReady)> CompleteKeyAsync(ValueTask pending,
            int frameIndex, object value, GovernedByteBuilder builder, bool asynchronous)
        {
            await pending.ConfigureAwait(false);
            return await AdvancePairValueAsync(frameIndex, value, builder, asynchronous).ConfigureAwait(false);
        }

        private ValueTask<(bool Advanced, bool ChunkReady)> AdvanceValueAsync(object value, int depth,
            GovernedByteBuilder builder, bool asynchronous, int defaultDepth = 0)
        {
            // Completed default replacements stay in the existing traversal.
            // Only a genuinely pending callback retains continuation state.
            while (true)
            {
                _context.CheckExecution(_span);
                if (depth >= ExecutionState.MaxInterpreterDepth || defaultDepth >= ExecutionState.MaxInterpreterDepth)
                    throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded while encoding a JSON object",
                        _span, LythonFailureReason.InterpreterStackLimit, ExecutionState.MaxInterpreterDepth,
                        Math.Max(depth, defaultDepth));

                switch (value)
                {
                    case PyNone:
                        builder.Append("null"u8);
                        return new((true, true));
                    case PyString text:
                        JsonModule.AppendJsonString(builder, text, _options.EnsureAscii, _context, _span);
                        return new((true, true));
                    case bool boolean:
                        builder.Append(boolean ? "true"u8 : "false"u8);
                        return new((true, true));
                    case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                        _context.CheckExecution(_span);
                        JsonModule.AppendJsonInteger(builder, integer, _context, _span);
                        return new((true, true));
                    case double floating:
                        return CompleteValue(JsonModule.AppendJsonDoubleAsync(builder, floating, _options,
                            _context, _span, asynchronous), chunkReady: true);
                    case PyList list:
                        EnsureFrameSlot();
                        PushFrame(new EmitFrame(list, depth) { Sequence = list });
                        return new((true, false));
                    case object tupleLike when PyTupleLike.TryGetItems(tupleLike, out var tuple):
                        EnsureFrameSlot();
                        PushFrame(new EmitFrame(tupleLike, depth) { Sequence = tuple });
                        return new((true, false));
                    case PyDict dict:
                        return CompleteValue(PushDictFrameAsync(dict, depth, asynchronous), chunkReady: false);
                    case PyCounter counter:
                        return CompleteValue(PushDictFrameAsync(counter.InnerDict, depth, asynchronous), chunkReady: false);
                    case PyDefaultDict defaultDict:
                        return CompleteValue(PushDictFrameAsync(defaultDict.InnerDict, depth, asynchronous), chunkReady: false);
                    default:
                        if (_options.DefaultCallable is null)
                            throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonModule.JsonValueTypeName(value)}");
                        if (_options.CheckCircular)
                        {
                            EnsureFrameSlot();
                            var frameIndex = _frameCount;
                            PushFrame(new EmitFrame(value, depth) { IsDefault = true });
                            OpenFrame(ref FrameAt(frameIndex), frameIndex);
                        }
                        var replacement = JsonModule.InvokeJsonCallbackCoreAsync(_options.DefaultCallable, value,
                            _context, _span, asynchronous);
                        if (!replacement.IsCompleted)
                            return CompleteDefaultAsync(replacement, depth, builder, asynchronous, defaultDepth + 1);
                        value = replacement.GetAwaiter().GetResult();
                        defaultDepth++;
                        break;
                }
            }
        }

        private static ValueTask<(bool Advanced, bool ChunkReady)> CompleteValue(ValueTask pending, bool chunkReady)
        {
            if (!pending.IsCompleted) return AwaitValueAsync(pending, chunkReady);
            pending.GetAwaiter().GetResult();
            return new((true, chunkReady));
        }

        private static async ValueTask<(bool Advanced, bool ChunkReady)> AwaitValueAsync(ValueTask pending, bool chunkReady)
        {
            await pending.ConfigureAwait(false);
            return (true, chunkReady);
        }

        private async ValueTask<(bool Advanced, bool ChunkReady)> CompleteDefaultAsync(ValueTask<object> pending,
            int depth, GovernedByteBuilder builder, bool asynchronous, int defaultDepth)
            => await AdvanceValueAsync(await pending.ConfigureAwait(false), depth, builder, asynchronous, defaultDepth).ConfigureAwait(false);

        private void AppendPrettyPrefix(GovernedByteBuilder builder, int depth)
        {
            if (_options.IndentUnit is null)
            {
                return;
            }

            JsonModule.AppendJsonValuePrefix(builder, _options, depth, 0, _context, _span);
        }

        private ValueTask PushDictFrameAsync(PyDict dict, int depth, bool asynchronous)
        {
            if (dict.Count == 0)
            {
                PrepareDictFrame(dict, depth, sorted: false);
                return ValueTask.CompletedTask;
            }
            var pending = JsonOptionTruthAsync(_options.SortKeys, _context, _span, asynchronous);
            if (!pending.IsCompleted) return CompleteDictFrameAsync(pending, dict, depth);
            PrepareDictFrame(dict, depth, pending.GetAwaiter().GetResult());
            return ValueTask.CompletedTask;
        }

        private void PrepareDictFrame(PyDict dict, int depth, bool sorted)
        {
            // Reserve the frame slot before retaining an item cursor or sorted pairs.
            EnsureFrameSlot();
            var frame = new EmitFrame(dict, depth);
            if (!sorted)
            {
                frame.DictPairs = new PyDict.ItemCursor(dict);
                PushFrame(frame);
                return;
            }

            var bytes = checked(32L * dict.Count);
            var governor = _context.MemoryGovernor;
            governor.Reserve(bytes, _span);
            try
            {
                frame.SortedEntries = JsonModule.CollectSortedJsonEntries(dict, _context, _span);
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

        private async ValueTask CompleteDictFrameAsync(ValueTask<bool> pending, PyDict dict, int depth)
            => PrepareDictFrame(dict, depth, await pending.ConfigureAwait(false));

        private ValueTask<(bool Found, object Key, object Value)> TryTakePairAsync(int frameIndex, bool asynchronous)
        {
            ref var frame = ref FrameAt(frameIndex);
            while (true)
            {
                _context.CheckExecution(_span);
                object key, value;
                if (frame.SortedEntries is not null)
                {
                    if (frame.SortedIndex >= frame.SortedEntries.Count) return new((false, PyNone.Instance, PyNone.Instance));
                    (key, value) = frame.SortedEntries[frame.SortedIndex++];
                }
                else
                {
                    if (!frame.DictPairs.MoveNext(out var pair, _context, _span)) return new((false, PyNone.Instance, PyNone.Instance));
                    key = pair.Key; value = pair.Value;
                }
                if (JsonModule.IsSupportedJsonObjectKey(key)) return new((true, key, value));
                var pending = JsonOptionTruthAsync(_options.SkipKeys, _context, _span, asynchronous);
                if (!pending.IsCompleted) return CompleteSkippedPairAsync(pending, frameIndex, asynchronous);
                if (!pending.GetAwaiter().GetResult())
                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            }
        }

        private async ValueTask<(bool Found, object Key, object Value)> CompleteSkippedPairAsync(
            ValueTask<bool> pending, int frameIndex, bool asynchronous)
        {
            if (!await pending.ConfigureAwait(false))
                throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            return await TryTakePairAsync(frameIndex, asynchronous).ConfigureAwait(false);
        }

        private void OpenFrame(ref EmitFrame frame, int frameIndex)
        {
            // The outer frame is already its own marker. Nested frames
            // keep the existing constant-time membership test.
            if (frameIndex != 0 && _nestedMarkers is not null
                && (ReferenceEquals(_outerFrame.Container, frame.Container) || !_nestedMarkers.Add(frame.Container)))
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", _span);
            frame.Opened = true;
        }

        private void CloseFrame()
        {
            var frameIndex = --_frameCount;
            ref var frame = ref FrameAt(frameIndex);
            var bytes = frame.ScratchBytes;
            var pairs = frame.DictPairs;
            if (frameIndex != 0 && frame.Opened) _nestedMarkers?.Remove(frame.Container);
            frame = default;
            pairs.Dispose();
            _context.MemoryGovernor.Release(bytes);
        }
    }
}
