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
        private async ValueTask<object> EncodeCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1) throw RuntimeErrors.Type("JSONEncoder.encode() takes exactly one argument", span);
            context.CheckExecution(span);
            if (arguments[0] is PyString text)
            {
                var ascii = await JsonOptionTruthAsync(await ReadAsync("ensure_ascii", context, span, asynchronous).ConfigureAwait(false), context, span, asynchronous).ConfigureAwait(false);
                return await JsonModule.SerializeJsonTextCoreAsync(text, JsonDumpOptions.Default with { EnsureAscii = ascii }, context, span, asynchronous).ConfigureAwait(false);
            }
            if (GuestOwner is not null)
            {
                var method = await ReadAsync("iterencode", context, span, asynchronous).ConfigureAwait(false);
                CallArgumentValue[] call = [CallArgumentValue.Positional(arguments[0]), CallArgumentValue.Keyword("_one_shot", true)];
                var chunks = asynchronous
                    ? await InvokeCallableTargetAsync(method, span, span, context, () => ValueTask.FromResult(call)).ConfigureAwait(false)
                    : InvokeCallableTarget(method, span, span, context, call);
                return asynchronous ? await StringMembers.JoinAsync(PyString.Empty, chunks, span, context).ConfigureAwait(false)
                    : StringMembers.Join(PyString.Empty, chunks, span, context);
            }
            var options = await GetDumpOptionsAsync(context, span, asynchronous, true).ConfigureAwait(false);
            return await JsonModule.SerializeJsonTextCoreAsync(arguments[0], options, context, span, asynchronous).ConfigureAwait(false);
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
            return new JsonEncodeIterator(arguments[0], options, context, span);
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

        internal static async ValueTask<object> ConstructAsync(CallArgumentValue[] arguments, PyInstance? guest, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var values = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["skipkeys"] = false, ["ensure_ascii"] = true, ["check_circular"] = true,
                ["allow_nan"] = true, ["sort_keys"] = false, ["indent"] = PyNone.Instance,
                ["separators"] = PyNone.Instance, ["default"] = PyNone.Instance
            };
            foreach (var argument in arguments)
            {
                if (argument.IsPositional) throw RuntimeErrors.Type("JSONEncoder.__init__() accepts keyword arguments only", span);
                if (argument.KeywordName is null || !values.ContainsKey(argument.KeywordName))
                    throw RuntimeErrors.Type("JSONEncoder.__init__() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
                values[argument.KeywordName] = argument.Value;
            }
            object instance = guest is null ? new JsonEncoderObject() : guest;
            foreach (var name in new[] { "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "sort_keys", "indent" })
                await AssignWrapperAttributeAsync(instance, name, values[name], context, span, asynchronous).ConfigureAwait(false);
            if (values["separators"] is not PyNone)
            {
                var separators = await JsonModule.ParseJsonSeparatorsAsync(values["separators"], span, context, asynchronous).ConfigureAwait(false);
                await AssignWrapperAttributeAsync(instance, "item_separator", separators.ItemSeparator, context, span, asynchronous).ConfigureAwait(false);
                await AssignWrapperAttributeAsync(instance, "key_separator", separators.KeySeparator, context, span, asynchronous).ConfigureAwait(false);
            }
            else if (values["indent"] is not PyNone)
                await AssignWrapperAttributeAsync(instance, "item_separator", JsonModule.PrettyItemSeparator, context, span, asynchronous).ConfigureAwait(false);
            if (values["default"] is not PyNone)
                await AssignWrapperAttributeAsync(instance, "default", values["default"], context, span, asynchronous).ConfigureAwait(false);
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
    // hooks) are visited. Per-advance scratch is scope-bound; sort scratch
    // stays funded across yields and ends when its frame is relinquished.
    internal sealed class JsonEncodeIterator : PyIteratorBase
    {
        private sealed class EmitFrame
        {
            public bool IsList;
            public bool IsDefault;
            public object Container = null!;
            public IEnumerator<object>? ListItems;
            public IEnumerator<KeyValuePair<object, object>>? DictPairs;
            public List<(object OriginalKey, object Value)>? SortedEntries;
            public int SortedIndex;
            public int Index;
            public int Depth;
            public long ScratchBytes;
            public bool Opened;
        }

        private readonly JsonDumpOptions _options;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly Stack<EmitFrame> _stack = new();
        private readonly HashSet<object>? _markers;
        private object? _rootValue;
        private bool _hasRoot;
        private long _scratchBytes;
        private bool _finished;

        public JsonEncodeIterator(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            _rootValue = value;
            _hasRoot = true;
            _options = options;
            _context = context;
            _span = span;
            _markers = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<json.encoder._iterencode object>");
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = TryMoveNextCoreAsync(false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }

        public override ValueTask<PyIterationResult> TryMoveNextAsync() => TryMoveNextCoreAsync(true);

        private async ValueTask<PyIterationResult> TryMoveNextCoreAsync(bool asynchronous)
        {
            var builder = new GovernedByteBuilder(_context.MemoryGovernor, _span);
            try
            {
                _context.CheckExecution(_span);
                if (_finished) return PyIterationResult.End;
                while (true)
                {
                    _context.CheckExecution(_span);
                    var (advanced, chunkReady) = await AdvanceAsync(builder, asynchronous).ConfigureAwait(false);
                    if (!advanced)
                    {
                        Finish();
                        return PyIterationResult.End;
                    }

                    if (!chunkReady || builder.Length == 0)
                    {
                        continue;
                    }

                    var chunk = builder.ToPyString();
                    return PyIterationResult.Yield(chunk);
                }
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
            finally { builder.Release(); }
        }

        private void GrowScratch(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            _context.MemoryGovernor.Reserve(bytes, _span);

            _context.MemoryGovernor.Commit(bytes);
            _scratchBytes += bytes;
        }

        private void ReleaseScratch(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            _context.MemoryGovernor.Release(bytes);
            _scratchBytes -= bytes;
        }

        private void Finish()
        {
            _finished = true;
            _hasRoot = false;
            _rootValue = null;
            while (_stack.TryPeek(out var frame)) CloseFrame(frame);
            _markers?.Clear();
        }

        // Advances the machine; true means more work remains (with a complete
        // chunk when chunkReady). Structural order mirrors the eager renderer
        // exactly: separators, indent prefixes, keys and values concatenate to
        // the same text encode() produces.
        private async ValueTask<(bool Advanced, bool ChunkReady)> AdvanceAsync(GovernedByteBuilder builder, bool asynchronous)
        {
            var chunkReady = false;
            if (_hasRoot)
            {
                _hasRoot = false;
                return await AdvanceValueAsync(_rootValue!, 0, builder, asynchronous).ConfigureAwait(false);
            }

            while (_stack.Count > 0)
            {
                var frame = _stack.Peek();
                if (frame.IsDefault) { CloseFrame(frame); continue; }
                if (!frame.Opened)
                {
                    frame.Opened = true;
                    if (_markers is not null && !_markers.Add(frame.Container))
                    {
                        throw new LythonRuntimeException("ValueError", "Circular reference detected.", _span);
                    }

                    builder.Append(frame.IsList ? (byte)'[' : (byte)'{');
                    chunkReady = true;
                    return (true, chunkReady);
                }

                if (frame.IsList)
                {
                    if (frame.ListItems is null || !frame.ListItems.MoveNext())
                    {
                        CloseFrame(frame);
                        if (frame.Index > 0)
                        {
                            JsonModule.AppendJsonContainerSuffix(builder, _options, frame.Depth, _context, _span);
                        }

                        builder.Append((byte)']');
                        chunkReady = true;
                        return (true, chunkReady);
                    }

                    var element = frame.ListItems.Current;
                    if (frame.Index > 0)
                    {
                        JsonModule.AppendSeparator(builder, _options.ItemSeparator, _context, _span);
                    }

                    AppendPrettyPrefix(builder, frame.Depth + 1);
                    frame.Index++;
                    return await AdvanceValueAsync(element, frame.Depth + 1, builder, asynchronous).ConfigureAwait(false);
                }

                var (hasPair, key, pairValue) = await TryTakePairAsync(frame, asynchronous).ConfigureAwait(false);
                if (!hasPair)
                {
                    CloseFrame(frame);
                    if (frame.Index > 0)
                    {
                        JsonModule.AppendJsonContainerSuffix(builder, _options, frame.Depth, _context, _span);
                    }

                    builder.Append((byte)'}');
                    chunkReady = true;
                    return (true, chunkReady);
                }

                if (frame.Index > 0)
                {
                    JsonModule.AppendSeparator(builder, _options.ItemSeparator, _context, _span);
                }

                AppendPrettyPrefix(builder, frame.Depth + 1);
                await JsonModule.AppendJsonObjectKeyAsync(builder, key, _options, _context, _span, asynchronous).ConfigureAwait(false);
                JsonModule.AppendSeparator(builder, _options.KeySeparator, _context, _span);
                frame.Index++;
                return await AdvanceValueAsync(pairValue, frame.Depth + 1, builder, asynchronous).ConfigureAwait(false);
            }

            return (false, false);
        }

        private async ValueTask<(bool Advanced, bool ChunkReady)> AdvanceValueAsync(object value, int depth, GovernedByteBuilder builder, bool asynchronous, int defaultDepth = 0)
        {
            var chunkReady = false;
            _context.CheckExecution(_span);
            if (depth >= ExecutionLimits.MaxInterpreterDepth || defaultDepth >= ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Attributed(
                    "RecursionError",
                    "maximum recursion depth exceeded while encoding a JSON object",
                    _span, LythonFailureReason.InterpreterStackLimit, ExecutionLimits.MaxInterpreterDepth,
                    Math.Max(depth, defaultDepth));
            }

            switch (value)
            {
                case PyNone:
                    builder.Append("null"u8);
                    chunkReady = true;
                    return (true, chunkReady);
                case PyString text:
                    JsonModule.AppendJsonString(builder, text, _options.EnsureAscii, _context, _span);
                    chunkReady = true;
                    return (true, chunkReady);
                case bool boolean:
                    builder.Append(boolean ? "true"u8 : "false"u8);
                    chunkReady = true;
                    return (true, chunkReady);
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    _context.CheckExecution(_span);
                    JsonModule.AppendJsonInteger(builder, integer, _context, _span);
                    return (true, true);
                case double floating:
                    await JsonModule.AppendJsonDoubleAsync(builder, floating, _options, _context, _span, asynchronous).ConfigureAwait(false);
                    chunkReady = true;
                    return (true, chunkReady);
                case PyList list:
                    PushListFrame(list, list.GetEnumerator(), depth);
                    return (true, chunkReady);
                case object tupleLike when PyTupleLike.TryGetItems(tupleLike, out var tuple):
                    PushListFrame(tupleLike, tuple.GetEnumerator(), depth);
                    return (true, chunkReady);
                case PyDict dict:
                    await PushDictFrameAsync(dict, depth, asynchronous).ConfigureAwait(false);
                    return (true, chunkReady);
                case PyCounter counter:
                    await PushDictFrameAsync(counter.InnerDict, depth, asynchronous).ConfigureAwait(false);
                    return (true, chunkReady);
                case PyDefaultDict defaultDict:
                    await PushDictFrameAsync(defaultDict.InnerDict, depth, asynchronous).ConfigureAwait(false);
                    return (true, chunkReady);
                default:
                    if (_options.DefaultCallable is null)
                    {
                        throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonModule.JsonValueTypeName(value)}");
                    }

                    if (_markers is not null)
                    {
                        if (!_markers.Add(value)) throw new LythonRuntimeException("ValueError", "Circular reference detected.", _span);
                        _stack.Push(new EmitFrame { IsDefault = true, Opened = true, Container = value });
                    }
                    var replacement = await JsonModule.InvokeJsonCallbackCoreAsync(_options.DefaultCallable, value, _context, _span, asynchronous).ConfigureAwait(false);
                    return await AdvanceValueAsync(replacement, depth, builder, asynchronous, defaultDepth + 1).ConfigureAwait(false);
            }
        }

        private void AppendPrettyPrefix(GovernedByteBuilder builder, int depth)
        {
            if (_options.IndentUnit is null)
            {
                return;
            }

            JsonModule.AppendJsonValuePrefix(builder, _options, depth, 0, _context, _span);
        }

        private void PushListFrame(object container, IEnumerator<object> items, int depth)
        {
            _stack.Push(new EmitFrame
            {
                IsList = true,
                Container = container,
                ListItems = items,
                Depth = depth,
            });
        }

        private async ValueTask PushDictFrameAsync(PyDict dict, int depth, bool asynchronous)
        {
            var frame = new EmitFrame
            {
                IsList = false,
                Container = dict,
                Depth = depth,
            };

            if (dict.Count == 0 || !await JsonOptionTruthAsync(_options.SortKeys, _context, _span, asynchronous).ConfigureAwait(false))
            {
                frame.DictPairs = dict.GetEnumerator();
                _stack.Push(frame);
                return;
            }

            // Sorting retains one reference pair per entry beside the live
            // dict; fund that scratch before building it like the eager path.
            var estimate = checked(32L * dict.Count);
            GrowScratch(estimate);
            try
            {
                var entries = JsonModule.CollectSortedJsonEntries(dict, _context, _span);
                frame.SortedEntries = entries;
                frame.ScratchBytes = estimate;
                _stack.Push(frame);
            }
            catch
            {
                ReleaseScratch(estimate);
                throw;
            }
        }

        private async ValueTask<(bool Found, object Key, object Value)> TryTakePairAsync(EmitFrame frame, bool asynchronous)
        {
            while (true)
            {
                _context.CheckExecution(_span);
                object key, value;
                if (frame.SortedEntries is not null)
                {
                    if (frame.SortedIndex >= frame.SortedEntries.Count) return (false, PyNone.Instance, PyNone.Instance);
                    (key, value) = frame.SortedEntries[frame.SortedIndex++];
                }
                else
                {
                    if (frame.DictPairs is null || !frame.DictPairs.MoveNext()) return (false, PyNone.Instance, PyNone.Instance);
                    var pair = frame.DictPairs.Current;
                    key = pair.Key; value = pair.Value;
                }
                if (JsonModule.IsSupportedJsonObjectKey(key)) return (true, key, value);
                if (!await JsonOptionTruthAsync(_options.SkipKeys, _context, _span, asynchronous).ConfigureAwait(false))
                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            }
        }

        private void CloseFrame(EmitFrame frame)
        {
            _ = _stack.Pop();
            if (_markers is not null)
            {
                _ = _markers.Remove(frame.Container);
            }

            frame.Container = null!;
            frame.SortedEntries = null;
            frame.ListItems?.Dispose();
            frame.DictPairs?.Dispose();
            frame.ListItems = null;
            frame.DictPairs = null;
            if (frame.ScratchBytes > 0)
            {
                ReleaseScratch(frame.ScratchBytes);
                frame.ScratchBytes = 0;
            }
        }
    }
}
