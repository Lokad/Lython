using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Ordinary JSONEncoder instances (N38): reusable and independent, each
    // carrying its parsed dump options plus the raw values behind the
    // readable attributes below. Subclasses (N39) and custom cls=...
    // classes fail explicitly through the dump options gate.
    internal sealed record JsonEncoderOptions(
        object SkipKeys,
        object EnsureAscii,
        object CheckCircular,
        object AllowNan,
        object SortKeys,
        object Indent,
        JsonDumpOptions Dump);

    internal sealed class JsonEncoderObject : IPyTruthyValue, IPyRenderableValue
    {
        internal JsonEncoderObject(JsonEncoderOptions options, PyString itemSeparator, PyString keySeparator, ICallable? defaultHook)
        {
            Options = options;
            ItemSeparator = itemSeparator;
            KeySeparator = keySeparator;
            DefaultHook = defaultHook;
        }

        internal JsonEncoderOptions Options { get; }

        internal PyString ItemSeparator { get; }

        internal PyString KeySeparator { get; }

        internal ICallable? DefaultHook { get; }

        public bool IsTruthy() => true;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<json.encoder.JSONEncoder object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Encode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.encode() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            context.CheckExecutionBudget(span);
            return JsonModule.SerializeJsonText(arguments[0], Options.Dump, context, span);
        }

        public object IterEncode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.iterencode() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            context.CheckExecutionBudget(span);
            var iterator = new JsonEncodeIterator(arguments[0], Options.Dump, context, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(iterator, PyIteratorBase.IteratorValueBytes, span);
            return iterator;
        }

        // The base default() is not a fallback call to encode(): even an
        // otherwise encodable value fails here unless a default= hook (or an
        // N39 override) supplies the behavior.
        public object Default(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.default() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            throw new LythonRuntimeException("TypeError", "Object of type " + JsonDefaultTypeName(arguments[0], context) + " is not JSON serializable", span);
        }

        private static string JsonDefaultTypeName(object value, ExecutionContext context)
            => value switch
            {
                PyString => "str",
                PyBytes => "bytes",
                PyList => "list",
                PyDict => "dict",
                PyTuple => "tuple",
                PySet => "set",
                bool => "bool",
                BigInteger or int => "int",
                double => "float",
                PyDecimal => "Decimal",
                PyNone or null => "NoneType",
                PyInstance instance => instance.Type.Name,
                _ => UnboundTypeMethod.PythonTypeName(value, context),
            };
    }

    // The stable json.JSONEncoder class identity: construction binds the
    // keyword-only dump options while module-function layouts stay
    // positional-first on their own signatures. Cached by the module, so
    // identity and isinstance agree across direct, imported and aliased
    // references.
    internal sealed class JsonEncoderClass : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public static readonly JsonEncoderClass Instance = new();

        private JsonEncoderClass()
        {
        }

        public string Name => "json.JSONEncoder";

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__" || name == "__qualname__")
            {
                value = PyString.FromString("JSONEncoder");
                return true;
            }

            if (name == "__module__")
            {
                value = PyString.FromString("json.encoder");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = context;
            _ = span;
            return TryGetMember(memberName, out value);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            object skipKeys = false;
            object ensureAscii = true;
            object checkCircular = true;
            object allowNan = true;
            object sortKeys = false;
            object indent = PyNone.Instance;
            object separators = PyNone.Instance;
            var hasSeparators = false;
            ICallable? defaultHook = null;
            var positional = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional++;
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "skipkeys":
                        skipKeys = argument.Value;
                        break;
                    case "ensure_ascii":
                        ensureAscii = argument.Value;
                        break;
                    case "check_circular":
                        checkCircular = argument.Value;
                        break;
                    case "allow_nan":
                        allowNan = argument.Value;
                        break;
                    case "sort_keys":
                        sortKeys = argument.Value;
                        break;
                    case "indent":
                        indent = argument.Value;
                        break;
                    case "separators":
                        separators = argument.Value;
                        hasSeparators = true;
                        break;
                    case "default":
                        defaultHook = JsonModule.OptionalJsonCallable(argument.Value, "default", span);
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "JSONEncoder.__init__() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
                }
            }

            if (positional > 0)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.__init__() takes 1 positional argument but " + (positional + 1) + " were given.", span);
            }

            var indentUnit = JsonModule.ParseJsonIndent(indent, span);
            var parsedSeparators = JsonModule.ParseJsonSeparators(hasSeparators ? separators : PyNone.Instance, !ReferenceEquals(indent, PyNone.Instance), span, context);
            var dump = new JsonDumpOptions(
                JsonModule.ParseJsonBoolOption(skipKeys, defaultValue: false),
                JsonModule.ParseJsonBoolOption(ensureAscii, defaultValue: true),
                JsonModule.ParseJsonBoolOption(checkCircular, defaultValue: true),
                JsonModule.ParseJsonBoolOption(allowNan, defaultValue: true),
                indentUnit,
                parsedSeparators.ItemSeparator,
                parsedSeparators.KeySeparator,
                defaultHook,
                JsonModule.ParseJsonBoolOption(sortKeys, defaultValue: false));
            return new JsonEncoderObject(
                new JsonEncoderOptions(skipKeys, ensureAscii, checkCircular, allowNan, sortKeys, indent, dump),
                PyString.FromString(parsedSeparators.ItemSeparator),
                PyString.FromString(parsedSeparators.KeySeparator),
                defaultHook);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'json.JSONEncoder'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal static class JsonEncoderMembers
    {
        public static bool TryGetMember(JsonEncoderObject encoder, string name, [MaybeNullWhen(false)] out object value)
        {
            switch (name)
            {
                case "encode":
                    value = BoundCallable.Create((arguments, span, context) => encoder.Encode(arguments, span, context), LythonKnownCallableSignatures.JsonEncoderEncode);
                    return true;
                case "iterencode":
                    value = BoundCallable.Create((arguments, span, context) => encoder.IterEncode(arguments, span, context), LythonKnownCallableSignatures.JsonEncoderIterencode);
                    return true;
                case "default":
                    value = encoder.DefaultHook is not null
                        ? encoder.DefaultHook
                        : BoundCallable.Create((arguments, span, context) => encoder.Default(arguments, span, context), LythonKnownCallableSignatures.JsonEncoderDefault);
                    return true;
                case "skipkeys":
                    value = encoder.Options.SkipKeys;
                    return true;
                case "ensure_ascii":
                    value = encoder.Options.EnsureAscii;
                    return true;
                case "check_circular":
                    value = encoder.Options.CheckCircular;
                    return true;
                case "allow_nan":
                    value = encoder.Options.AllowNan;
                    return true;
                case "sort_keys":
                    value = encoder.Options.SortKeys;
                    return true;
                case "indent":
                    value = encoder.Options.Indent;
                    return true;
                case "item_separator":
                    value = encoder.ItemSeparator;
                    return true;
                case "key_separator":
                    value = encoder.KeySeparator;
                    return true;
                default:
                    value = MissingMemberValue.Instance;
                    return false;
            }
        }
    }

    // Genuinely incremental encoder traversal: an explicit-stack machine over
    // the same rendering helpers as encode(), so joined chunks equal encode()
    // output exactly while each advance visits only what its chunk needs. A
    // container opener is consumable before later elements (and their default
    // hooks) are visited. Per-advance scratch is scope-bound; sort scratch
    // spanning yields is pool-tracked for drop safety (see GrowScratch).
    internal sealed class JsonEncodeIterator : PyIteratorBase
    {
        private sealed class EmitFrame
        {
            public bool IsList;
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
        private int _defaultDepth;
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
            _context.CheckExecutionBudget(_span);
            if (_finished)
            {
                value = PyNone.Instance;
                return false;
            }

            try
            {
                using var charge = new JsonModule.JsonGrowthCharge(_context.MemoryGovernor, _span);
                var builder = new StringBuilder();
                while (true)
                {
                    _context.CheckExecutionBudget(_span);
                    if (!Advance(builder, charge, out var chunkReady))
                    {
                        DiscardScratch();
                        _finished = true;
                        value = PyNone.Instance;
                        return false;
                    }

                    if (!chunkReady || builder.Length == 0)
                    {
                        continue;
                    }

                    // Fund the UTF-16 copy below like the eager path, then
                    // transfer ownership to the retained chunk itself.
                    charge.Grow(builder.Length);
                    var chunk = LythonRuntime.CreateString(builder.ToString(), _context, _span);
                    _context.Services.State.CallTemporaries.TrackFreshString(chunk, _span);
                    value = chunk;
                    return true;
                }
            }
            catch (InvalidOperationException ex)
            {
                DiscardScratch();
                _finished = true;
                throw new LythonRuntimeException("TypeError", ex.Message, _span);
            }
            catch
            {
                DiscardScratch();
                _finished = true;
                throw;
            }
        }

        // Sort scratch is funded before growth and tracked in the pool against
        // this iterator, so exhaustion denies cleanly and a dropped iterator
        // reclaims on sweep. Funding before tracking would strand on drop;
        // tracking before funding would over-release when funding denies, so
        // the reservation is held aside until tracking succeeds.
        private void GrowScratch(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            var pool = _context.Services.State.CallTemporaries;
            _context.MemoryGovernor.Reserve(bytes, _span);
            try
            {
                pool.TrackMutable(this, checked(_scratchBytes + bytes), _span);
            }
            catch (LythonRuntimeException)
            {
                _context.MemoryGovernor.ReleaseReserved(bytes);
                throw;
            }

            _context.MemoryGovernor.Commit(bytes);
            _scratchBytes += bytes;
            ChargeReclamationPool.NotifyStorageReplaced(this, _scratchBytes);
        }

        private void ReleaseScratch(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            _context.MemoryGovernor.Release(bytes);
            _scratchBytes -= bytes;
            if (_scratchBytes < 0)
            {
                _scratchBytes = 0;
            }

            ChargeReclamationPool.NotifyStorageReplaced(this, _scratchBytes);
        }

        private void DiscardScratch()
        {
            if (_scratchBytes > 0)
            {
                _context.MemoryGovernor.Release(_scratchBytes);
                _scratchBytes = 0;
                ChargeReclamationPool.NotifyStorageReplaced(this, 0);
            }
        }

        // Advances the machine; true means more work remains (with a complete
        // chunk when chunkReady). Structural order mirrors the eager renderer
        // exactly: separators, indent prefixes, keys and values concatenate to
        // the same text encode() produces.
        private bool Advance(StringBuilder builder, JsonModule.JsonGrowthCharge charge, out bool chunkReady)
        {
            chunkReady = false;
            if (_hasRoot)
            {
                _hasRoot = false;
                return AdvanceValue(_rootValue!, 0, builder, charge, out chunkReady);
            }

            while (_stack.Count > 0)
            {
                var frame = _stack.Peek();
                if (!frame.Opened)
                {
                    frame.Opened = true;
                    if (_markers is not null && !_markers.Add(frame.Container))
                    {
                        throw new LythonRuntimeException("ValueError", "Circular reference detected.", _span);
                    }

                    builder.Append(frame.IsList ? '[' : '{');
                    chunkReady = true;
                    return true;
                }

                if (frame.IsList)
                {
                    if (frame.ListItems is null || !frame.ListItems.MoveNext())
                    {
                        CloseFrame(frame);
                        if (frame.Index > 0)
                        {
                            JsonModule.AppendJsonContainerSuffix(builder, _options, frame.Depth, charge, _context, _span);
                        }

                        builder.Append(']');
                        chunkReady = true;
                        return true;
                    }

                    var element = frame.ListItems.Current;
                    if (frame.Index > 0)
                    {
                        JsonModule.AppendSeparator(builder, _options.ItemSeparator, charge);
                    }

                    AppendPrettyPrefix(builder, frame.Depth + 1, charge);
                    frame.Index++;
                    return AdvanceValue(element, frame.Depth + 1, builder, charge, out chunkReady);
                }

                if (!TryTakePair(frame, out var key, out var pairValue))
                {
                    CloseFrame(frame);
                    if (frame.Index > 0)
                    {
                        JsonModule.AppendJsonContainerSuffix(builder, _options, frame.Depth, charge, _context, _span);
                    }

                    builder.Append('}');
                    chunkReady = true;
                    return true;
                }

                if (frame.Index > 0)
                {
                    JsonModule.AppendSeparator(builder, _options.ItemSeparator, charge);
                }

                AppendPrettyPrefix(builder, frame.Depth + 1, charge);
                if (!JsonModule.TryConvertJsonObjectKey(key, skipKeys: false, out var keyText))
                {
                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                }

                JsonModule.AppendJsonString(builder, keyText, _options.EnsureAscii, charge, _context, _span);
                JsonModule.AppendSeparator(builder, _options.KeySeparator, charge);
                frame.Index++;
                return AdvanceValue(pairValue, frame.Depth + 1, builder, charge, out chunkReady);
            }

            return false;
        }

        private bool AdvanceValue(object value, int depth, StringBuilder builder, JsonModule.JsonGrowthCharge charge, out bool chunkReady)
        {
            chunkReady = false;
            _context.CheckExecutionBudget(_span);
            if (depth >= ExecutionLimits.MaxInterpreterDepth || _defaultDepth >= ExecutionLimits.MaxInterpreterDepth)
            {
                throw new LythonRuntimeException(
                    "RecursionError",
                    "maximum recursion depth exceeded while encoding a JSON object",
                    _span);
            }

            switch (value)
            {
                case PyNone:
                    builder.Append("null");
                    chunkReady = true;
                    return true;
                case PyString text:
                    JsonModule.AppendJsonString(builder, text.AsString(), _options.EnsureAscii, charge, _context, _span);
                    chunkReady = true;
                    return true;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    chunkReady = true;
                    return true;
                case BigInteger integer:
                    _context.CheckExecutionBudget(_span);
                    JsonModule.AppendJsonNumber(builder, integer.ToString(CultureInfo.InvariantCulture), charge, _context, _span);
                    chunkReady = true;
                    return true;
                case int integer:
                    JsonModule.AppendJsonNumber(builder, integer.ToString(CultureInfo.InvariantCulture), charge, _context, _span);
                    chunkReady = true;
                    return true;
                case double floating:
                    JsonModule.AppendJsonDouble(builder, floating, _options, charge, _context, _span);
                    chunkReady = true;
                    return true;
                case PyDecimal decimalValue:
                    JsonModule.AppendJsonNumber(builder, PyDecimalOps.Format(decimalValue), charge, _context, _span);
                    chunkReady = true;
                    return true;
                case PyList list:
                    PushListFrame(list, list.GetEnumerator(), depth);
                    return true;
                case PyTuple tuple:
                    PushListFrame(tuple, tuple.GetEnumerator(), depth);
                    return true;
                case PyDict dict:
                    PushDictFrame(dict, depth);
                    return true;
                default:
                    if (_options.DefaultCallable is null)
                    {
                        throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonModule.JsonValueTypeName(value)}");
                    }

                    var replacement = JsonModule.InvokeJsonCallback(_options.DefaultCallable, value, _context, _span);
                    if (ReferenceEquals(replacement, value))
                    {
                        throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", _span);
                    }

                    // Replacements that keep producing fresh unsupported
                    // objects carry their own depth budget like the eager
                    // path instead of recursing past the container guard.
                    _defaultDepth++;
                    return AdvanceValue(replacement, depth, builder, charge, out chunkReady);
            }
        }

        private void AppendPrettyPrefix(StringBuilder builder, int depth, JsonModule.JsonGrowthCharge charge)
        {
            if (_options.IndentUnit is null)
            {
                return;
            }

            JsonModule.AppendJsonValuePrefix(builder, _options, depth, 0, charge, _context, _span);
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

        private void PushDictFrame(PyDict dict, int depth)
        {
            var frame = new EmitFrame
            {
                IsList = false,
                Container = dict,
                Depth = depth,
            };

            if (!_options.SortKeys)
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
                var entries = new List<(object OriginalKey, object Value)>();
                foreach (var pair in dict)
                {
                    _context.CheckExecutionBudget(_span);
                    if (JsonModule.IsSupportedJsonObjectKey(pair.Key))
                    {
                        entries.Add((pair.Key, pair.Value));
                    }
                    else if (!_options.SkipKeys)
                    {
                        throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                    }
                }

                entries.Sort((left, right) => PyComparison.Compare(left.OriginalKey, right.OriginalKey, _span));
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

        private bool TryTakePair(EmitFrame frame, out object key, out object value)
        {
            while (true)
            {
                if (frame.SortedEntries is not null)
                {
                    if (frame.SortedIndex >= frame.SortedEntries.Count)
                    {
                        key = PyNone.Instance;
                        value = PyNone.Instance;
                        return false;
                    }

                    var entry = frame.SortedEntries[frame.SortedIndex++];
                    key = entry.OriginalKey;
                    value = entry.Value;
                    return true;
                }

                if (frame.DictPairs is null || !frame.DictPairs.MoveNext())
                {
                    key = PyNone.Instance;
                    value = PyNone.Instance;
                    return false;
                }

                var pair = frame.DictPairs.Current;
                if (!JsonModule.IsSupportedJsonObjectKey(pair.Key))
                {
                    if (_options.SkipKeys)
                    {
                        continue;
                    }

                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                }

                key = pair.Key;
                value = pair.Value;
                return true;
            }
        }

        private void CloseFrame(EmitFrame frame)
        {
            _ = _stack.Pop();
            if (_markers is not null)
            {
                _ = _markers.Remove(frame.Container);
            }

            if (frame.ScratchBytes > 0)
            {
                ReleaseScratch(frame.ScratchBytes);
                frame.ScratchBytes = 0;
            }

            frame.ListItems?.Dispose();
            frame.DictPairs?.Dispose();
        }
    }
}
