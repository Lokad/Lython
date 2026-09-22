using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyLruCacheWrapper :
        ICallable,
        IPyBindableCallable,
        IPyMutableDynamicAttributes,
        IPyContextualDynamicAttributes,
        IPyRenderableValue,
        IClassOwnedMember
    {
        private readonly ICallable _callable;
        private readonly int? _maxSize;
        private readonly CacheKeyMode _keyMode;
        // N08/N12: contextual key storage (guest __hash__/__eq__) instead of the
        // structural comparer, so equal-but-distinct keys hit like CPython.
        private ContextualKeyTable<CacheEntry> _cache = new();
        private readonly LinkedList<object> _recency = [];
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
        private readonly MemoryGovernor _memoryGovernor;
        private readonly ChargeReclamationPool _pool;
        private long _committedEntryBytes;
        private long _committedKeyBytes;
        private long _committedMetadataBytes;
        private BigInteger _hits;
        private BigInteger _misses;

        // Per-entry infrastructure (table slot, recency node and entry
        // record) is charged here; retained key graphs join the wrapper
        // coupon below, while results arrive with their own ownership from
        // invocation.
        private const long EntryInfrastructureBytes = 128;

        // Wrapper attribute slots ride the instance-attribute rate.
        private const long MetadataSlotBytes = 64;

        public PyLruCacheWrapper(ICallable callable, int? maxSize, CacheKeyMode keyMode, MemoryGovernor memoryGovernor, ChargeReclamationPool pool)
        {
            _callable = callable;
            _maxSize = maxSize;
            _keyMode = keyMode;
            _memoryGovernor = memoryGovernor;
            _pool = pool;
        }

        // Wrapper coupon for drop reclamation: infrastructure plus retained key
        // graphs plus metadata slots. Keys commit at construction and transfer
        // here; eviction/clear release exactly, and abandoned wrappers sweep.
        internal long CommittedStorageBytes =>
            _committedEntryBytes + _committedKeyBytes + _committedMetadataBytes;

        private void NoteGrowth(long beforeCharges)
        {
            var current = CommittedStorageBytes;
            if (current != beforeCharges)
            {
                // N08: first growth registers the wrapper (creation coupons are
                // zero, hence untracked); later moves re-snapshot like other pools.
                if (current > 0 && !_pool.IsTracked(this))
                {
                    _pool.TrackFreshMutable(this, current, null);
                }
                else
                {
                    ChargeReclamationPool.NotifyStorageReplaced(this, current);
                }
            }
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            // N08: explicit key ownership (no global-delta inference, which an
            // exhaustion sweep can perturb through unrelated commitments). The
            // tuple commits at construction; hits and disabled caches release
            // it, misses transfer it into the wrapper coupon.
            var key = BuildCacheKey(arguments, _keyMode, context, span, out var keyCharge);
            if (_maxSize != 0 && _cache.TryGetValue(key, context, span, out var cached))
            {
                _hits++;
                Touch(cached);
                context.MemoryGovernor.Release(keyCharge);
                return cached.Value;
            }

            _misses++;
            object result;
            try
            {
                result = _callable.Invoke(arguments, span, context);
            }
            catch
            {
                context.MemoryGovernor.Release(keyCharge);
                throw;
            }

            if (_maxSize != 0)
            {
                Store(key, keyCharge, result, span, context);
            }
            else
            {
                context.MemoryGovernor.Release(keyCharge);
            }

            return result;
        }

        // N15: awaited twin of Invoke for RunAsync paths. Key building, lookup,
        // hit/miss accounting and failure refunds are identical; only the target
        // invocation suspends. Key lookup itself stays synchronous (contextual
        // dispatch has no async twin, like the N12 module tables).
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var key = BuildCacheKey(arguments, _keyMode, context, span, out var keyCharge);
            if (_maxSize != 0 && _cache.TryGetValue(key, context, span, out var cached))
            {
                _hits++;
                Touch(cached);
                context.MemoryGovernor.Release(keyCharge);
                return cached.Value;
            }

            _misses++;
            object result;
            try
            {
                result = await _callable.InvokeAsync(arguments, span, context).ConfigureAwait(false);
            }
            catch
            {
                context.MemoryGovernor.Release(keyCharge);
                throw;
            }

            if (_maxSize != 0)
            {
                Store(key, keyCharge, result, span, context);
            }
            else
            {
                context.MemoryGovernor.Release(keyCharge);
            }

            return result;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public void BindOwner(PyType owner)
        {
            if (_callable is IClassOwnedMember owned)
            {
                owned.BindOwner(owner);
            }
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_metadata.TryGetValue(name, out value))
            {
                return true;
            }

            value = name switch
            {
                "cache_info" => new CacheInfoMethod(this),
                "cache_clear" => new CacheClearMethod(this),
                "cache_parameters" => new CacheParametersMethod(this),
                "__wrapped__" => _callable,
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata),
                "__name__" => PyString.FromString("lru_cache_wrapper"),
                "__qualname__" => PyString.FromString("lru_cache_wrapper"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (_metadata.TryGetValue(name, out value))
            {
                return true;
            }

            value = name switch
            {
                "cache_info" => new CacheInfoMethod(this),
                "cache_clear" => new CacheClearMethod(this),
                "cache_parameters" => new CacheParametersMethod(this),
                "__wrapped__" => _callable,
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata, context.MemoryGovernor, span),
                "__name__" => PyString.FromString("lru_cache_wrapper"),
                "__qualname__" => PyString.FromString("lru_cache_wrapper"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            // Wrapper attribute slots ride the instance-attribute rate; the
            // creation-time update_wrapper pass routes through here as well.
            // Slots join the wrapper coupon so abandoned wrappers sweep them.
            if (!_metadata.ContainsKey(name))
            {
                _memoryGovernor.Reserve(MetadataSlotBytes, null);
                _memoryGovernor.Commit(MetadataSlotBytes);
                _committedMetadataBytes += MetadataSlotBytes;
                ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            }

            _metadata[name] = value;
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var name = TryReadWrapperMetadata(_callable, "__name__", context.Context, null, out var value) &&
                PyStringOps.TryAsString(value, out var text)
                    ? text.AsString()
                    : "lru_cache_wrapper";
            return PyString.FromString($"<functools._lru_cache_wrapper {name}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private void Store(object key, long keyCharge, object value, LythonSourceSpan span, ExecutionContext context)
        {
            // Equal-but-distinct keys hit the stored entry (N12) instead of
            // duplicating it; the fresh duplicate's charge releases.
            if (_cache.TryGetValue(key, context, span, out var existing))
            {
                existing.Value = value;
                Touch(existing);
                _memoryGovernor.Release(keyCharge);
                return;
            }

            var beforeCharges = CommittedStorageBytes;
            // Preflight infrastructure before mutating, so denial leaves exact
            // charges; the fresh key releases on the way out.
            try
            {
                _memoryGovernor.Reserve(EntryInfrastructureBytes, span);
                _memoryGovernor.Commit(EntryInfrastructureBytes);
            }
            catch
            {
                _memoryGovernor.Release(keyCharge);
                throw;
            }

            _committedEntryBytes += EntryInfrastructureBytes;
            _committedKeyBytes += keyCharge;

            if (_maxSize is null)
            {
                _cache.Add(key, new CacheEntry(value, keyCharge, node: null), context, span);
                NoteGrowth(beforeCharges);
                return;
            }

            var node = _recency.AddLast(key);
            _cache.Add(key, new CacheEntry(value, keyCharge, node), context, span);
            NoteGrowth(beforeCharges);
            if (_cache.Count > _maxSize.Value)
            {
                var oldest = _recency.First.RequireNotNull();
                _recency.RemoveFirst();
                // The stored key object rides the recency node, so the lookup
                // below hits by identity without guest dispatch.
                long evictedKeyCharge = 0;
                if (_cache.TryGetValue(oldest.Value, context, span, out var evicted))
                {
                    evictedKeyCharge = evicted.KeyCharge;
                    _cache.Remove(oldest.Value, context, span);
                }

                _memoryGovernor.Release(EntryInfrastructureBytes);
                _committedEntryBytes -= EntryInfrastructureBytes;
                _memoryGovernor.Release(evictedKeyCharge);
                _committedKeyBytes -= evictedKeyCharge;
                NoteGrowth(beforeCharges);
            }
        }
        private void Touch(CacheEntry entry)
        {
            if (entry.Node is not { } node || ReferenceEquals(node, _recency.Last))
            {
                return;
            }

            _recency.Remove(node);
            _recency.AddLast(node);
        }

        private PyNamedTupleObject BuildCacheInfo(LythonSourceSpan span)
        {
            object maxSizeValue = _maxSize is int limit ? new BigInteger(limit) : PyNone.Instance;
            return FunctoolsCacheInfoType.CreateFromValues(
                [_hits, _misses, maxSizeValue, new BigInteger(_cache.Count)],
                span);
        }

        private PyDict BuildCacheParameters(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            dict.SetItem(PyString.FromString("maxsize"), _maxSize is int limit ? new BigInteger(limit) : PyNone.Instance);
            dict.SetItem(PyString.FromString("typed"), _keyMode == CacheKeyMode.ValuesAndTypes);
            return dict;
        }

        private void Clear()
        {
            // N08: release retained entry infrastructure plus every retained key
            // graph (guest aliases never outlive entries: equal-key stores reuse
            // them), then drop CLR dictionary capacity by rebuilding (it was
            // never separately charged). Hits/misses reset like CPython.
            var beforeCharges = CommittedStorageBytes;
            foreach (var entry in _cache.EntriesInOrder)
            {
                _memoryGovernor.Release(entry.Value.KeyCharge);
            }

            _memoryGovernor.Release(_committedEntryBytes);
            _cache = new();
            _recency.Clear();
            _committedEntryBytes = 0;
            _committedKeyBytes = 0;
            _hits = BigInteger.Zero;
            _misses = BigInteger.Zero;
            NoteGrowth(beforeCharges);
        }

        private sealed class CacheEntry
        {
            public CacheEntry(object value, long keyCharge, LinkedListNode<object>? node)
            {
                Value = value;
                KeyCharge = keyCharge;
                Node = node;
            }

            public object Value { get; set; }

            public long KeyCharge { get; }

            public LinkedListNode<object>? Node { get; }
        }

        private sealed class CacheInfoMethod : ICallable, IPyRenderableValue
        {
            private readonly PyLruCacheWrapper _owner;

            public CacheInfoMethod(PyLruCacheWrapper owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "cache_info() expects no arguments.", span);
                }

                return _owner.BuildCacheInfo(span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<cache_info>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private sealed class CacheClearMethod : ICallable, IPyRenderableValue
        {
            private readonly PyLruCacheWrapper _owner;

            public CacheClearMethod(PyLruCacheWrapper owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "cache_clear() expects no arguments.", span);
                }

                _owner.Clear();
                return PyNone.Instance;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<cache_clear>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private sealed class CacheParametersMethod : ICallable, IPyRenderableValue
        {
            private readonly PyLruCacheWrapper _owner;

            public CacheParametersMethod(PyLruCacheWrapper owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "cache_parameters() expects no arguments.", span);
                }

                return _owner.BuildCacheParameters(context.MemoryGovernor, span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<cache_parameters>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
    }

    private sealed class CachedPropertyFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CachedPropertyFactory Instance = new();

        public string Name => "functools.cached_property";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.cached_property(func) expects one callable argument.", span);
            }

            return new PyCachedProperty(callable, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'functools.cached_property'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCachedProperty : IPyDescriptor, IClassNamedMember, IClassOwnedMember, IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly ICallable _callable;
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
        // Metadata tables grow one CLR entry per guest attribute name;
        // charge each new key so retained attributes accumulate. Descriptors
        // have no attribute delete path, so nothing is released.
        private const long AttributeSlotBytes = 64;
        private MemoryGovernor? _memoryGovernor;
        private LythonSourceSpan? _allocationSpan;

        public PyCachedProperty(ICallable callable, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        {
            _callable = callable;
            _memoryGovernor = governor;
            _allocationSpan = allocationSpan;
        }

        public string? Name { get; private set; }

        public void BindName(string name) => Name ??= name;

        public void BindOwner(PyType owner)
        {
            if (_callable is IClassOwnedMember owned)
            {
                owned.BindOwner(owner);
            }
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
        {
            if (instance is null)
            {
                return this;
            }

            if (context is null || span is null)
            {
                throw new InvalidOperationException("cached_property access requires runtime context.");
            }

            if (instance is not PyInstance pyInstance)
            {
                throw new LythonRuntimeException("TypeError", "cached_property can only be accessed on user class instances.", span);
            }

            if (Name is null)
            {
                throw new LythonRuntimeException("TypeError", "cached_property has no bound attribute name.", span);
            }

            if (pyInstance.TryGetOwnAttribute(Name, out var cached))
            {
                return cached;
            }

            var callable = BindCallableForInstance(_callable, pyInstance, owner, context, span);
            var value = callable.Invoke([], span, context);
            pyInstance.SetAttribute(Name, value);
            return value;

            static ICallable BindCallableForInstance(ICallable callable, object instance, PyType owner, ExecutionContext context, LythonSourceSpan span)
            {
                var resolved = callable is IPyDescriptor descriptor
                    ? descriptor.Get(instance, owner, context, span)
                    : new PyBoundMethod(instance, callable);

                if (resolved is not ICallable bound)
                {
                    throw new LythonRuntimeException("TypeError", "Descriptor target must resolve to a callable.", span);
                }

                return bound;
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_metadata.TryGetValue(name, out value))
            {
                return true;
            }

            value = name switch
            {
                "func" => _callable,
                "attrname" => Name is null ? PyNone.Instance : PyString.FromString(Name),
                "__name__" => Name is null ? PyString.FromString("cached_property") : PyString.FromString(Name),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (_memoryGovernor is not null && !_metadata.ContainsKey(name))
            {
                _memoryGovernor.Reserve(AttributeSlotBytes, _allocationSpan);
                _memoryGovernor.Commit(AttributeSlotBytes);
            }

            _metadata[name] = value;
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.cached_property>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static PyLruCacheWrapper CreateCacheWrapper(
        ICallable callable,
        int? maxSize,
        CacheKeyMode keyMode,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // N08: the wrapper joins the run pool so abandoned wrappers sweep the
        // coupon below instead of stranding infrastructure, keys and metadata.
        var wrapper = new PyLruCacheWrapper(callable, maxSize, keyMode, context.MemoryGovernor, context.Services.State.CallTemporaries);
        ApplyUpdateWrapper(wrapper, wrapper, callable, FunctoolsWrapperAssignmentNames, FunctoolsWrapperUpdateNames, context, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(wrapper, wrapper.CommittedStorageBytes, span);
        return wrapper;
    }

    private static UpdateWrapperArguments ParseUpdateWrapperArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        object? wrapper = null;
        object? wrapped = null;
        object assigned = CreateStringTuple(FunctoolsWrapperAssignmentNames);
        object updated = CreateStringTuple(FunctoolsWrapperUpdateNames);
        var seenWrapper = false;
        var seenWrapped = false;
        var seenAssigned = false;
        var seenUpdated = false;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                switch (positionalIndex++)
                {
                    case 0:
                        if (seenWrapper)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "wrapper", span);
                        }

                        wrapper = argument.Value;
                        seenWrapper = true;
                        break;
                    case 1:
                        if (seenWrapped)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "wrapped", span);
                        }

                        wrapped = argument.Value;
                        seenWrapped = true;
                        break;
                    case 2:
                        if (seenAssigned)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "assigned", span);
                        }

                        assigned = argument.Value;
                        seenAssigned = true;
                        break;
                    case 3:
                        if (seenUpdated)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "updated", span);
                        }

                        updated = argument.Value;
                        seenUpdated = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped[, assigned][, updated]) expects at most four positional arguments.", span);
                }

                continue;
            }

            switch (argument.KeywordName)
            {
                case "wrapper":
                    if (seenWrapper)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "wrapper", span);
                    }

                    wrapper = argument.Value;
                    seenWrapper = true;
                    break;
                case "wrapped":
                    if (seenWrapped)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "wrapped", span);
                    }

                    wrapped = argument.Value;
                    seenWrapped = true;
                    break;
                case "assigned":
                    if (seenAssigned)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "assigned", span);
                    }

                    assigned = argument.Value;
                    seenAssigned = true;
                    break;
                case "updated":
                    if (seenUpdated)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.update_wrapper", "updated", span);
                    }

                    updated = argument.Value;
                    seenUpdated = true;
                    break;
                default:
                    throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "functools.update_wrapper", argument.KeywordName, span);
            }
        }

        return new UpdateWrapperArguments(wrapper, wrapped, assigned, updated);
    }

    private static int? ParseCacheMaxSize(object value, LythonSourceSpan span)
    {
        if (ReferenceEquals(value, PyNone.Instance))
        {
            return null;
        }

        if (!PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "functools.lru_cache(maxsize=...) expects an integer or None.", span);
        }

        if (integer <= BigInteger.Zero)
        {
            return 0;
        }

        if (integer > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "functools.lru_cache(maxsize=...) is too large for Lython.", span);
        }

        return (int)integer;
    }

    private static object BuildCacheKey(
        CallArgumentValue[] arguments,
        CacheKeyMode keyMode,
        ExecutionContext context,
        LythonSourceSpan span,
        out long keyCharge)
    {
        // N08: explicit key ownership. Arity-bounded parts scratch needs no
        // temp; the tuple commits its storage and reports it with every fresh
        // payload (keyword/type strings, identity tokens) summed alongside, so
        // hits, disabled caches and insertion failures release exactly and
        // misses transfer exactly into the wrapper coupon.
        try
        {
            var parts = new List<object>();
            long payloadCharge = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    parts.Add(ValidateDictionaryKey(argument.Value, span));
                    continue;
                }

                parts.Add(CacheKeyMarker.Keyword);
                var keywordName = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                payloadCharge += keywordName.CommittedOwnedBytes;
                parts.Add(keywordName);
                parts.Add(ValidateDictionaryKey(argument.Value, span));
            }

            if (keyMode == CacheKeyMode.ValuesAndTypes)
            {
                parts.Add(CacheKeyMarker.Typed);
                foreach (var argument in arguments)
                {
                    var part = BuildCacheTypePart(argument.Value, context, span, ref payloadCharge);
                    parts.Add(part);
                }
            }

            var key = new PyTuple(parts, context.MemoryGovernor, span);
            keyCharge = checked(key.CommittedStorageBytes + payloadCharge);
            return key;
        }
        catch (InvalidOperationException ex)
        {
            throw new LythonRuntimeException("TypeError", ex.Message, span);
        }
    }
    // N16: typed-key identity uses runtime type references instead of display
    // names (distinct classes may share a name). Strings stay structural for
    // builtin kinds, which are pinned by exact-charge tests; every other host kind
    // keys by its runtime type with the same ownership. Identity tokens keep their
    // own hash/equality pair below.
    private static object BuildCacheTypePart(object value, ExecutionContext context, LythonSourceSpan span, ref long payloadCharge)
    {
        PyString text;
        switch (value)
        {
            case PyNone:
                text = PyString.FromString("NoneType", context.MemoryGovernor, span);
                break;
            case bool:
                text = PyString.FromString("bool", context.MemoryGovernor, span);
                break;
            case BigInteger or int:
                text = PyString.FromString("int", context.MemoryGovernor, span);
                break;
            case double:
                text = PyString.FromString("float", context.MemoryGovernor, span);
                break;
            case PyString:
                text = PyString.FromString("str", context.MemoryGovernor, span);
                break;
            case PyBytes:
                text = PyString.FromString("bytes", context.MemoryGovernor, span);
                break;
            case PyList:
                text = PyString.FromString("list", context.MemoryGovernor, span);
                break;
            case PyTuple:
                text = PyString.FromString("tuple", context.MemoryGovernor, span);
                break;
            case PyDict:
                text = PyString.FromString("dict", context.MemoryGovernor, span);
                break;
            case PySet:
                text = PyString.FromString("set", context.MemoryGovernor, span);
                break;
            case PyInstance instance:
                return OwnCacheTypeToken(instance.Type, context, span, ref payloadCharge);
            case PyType type:
                return OwnCacheTypeToken(type, context, span, ref payloadCharge);
            default:
                return OwnCacheTypeToken(value.GetType(), context, span, ref payloadCharge);
        }

        payloadCharge = checked(payloadCharge + text.CommittedOwnedBytes);
        return text;
    }

    private static CacheTypeToken OwnCacheTypeToken(object identity, ExecutionContext context, LythonSourceSpan span, ref long payloadCharge)
    {
        const long TokenBytes = 64;
        context.MemoryGovernor.Reserve(TokenBytes, span);
        context.MemoryGovernor.Commit(TokenBytes);
        payloadCharge = checked(payloadCharge + TokenBytes);
        return new CacheTypeToken(identity);
    }

    // N16: typed-cache identity token (see BuildCacheTypePart). structural
    // CLR equality/hash by runtime reference, plus the governed hash hook.
    private sealed class CacheTypeToken : IPyHashableValue
    {
        public CacheTypeToken(object identity) => Identity = identity;

        public object Identity { get; }

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(Identity);

        public override bool Equals(object? obj)
            => obj is CacheTypeToken other && ReferenceEquals(Identity, other.Identity);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(Identity);
    }

    private sealed class CacheKeyMarker : IPyHashableValue
    {
        public static readonly CacheKeyMarker Keyword = new("kw");
        public static readonly CacheKeyMarker Typed = new("typed");

        private readonly string _name;

        private CacheKeyMarker(string name)
        {
            _name = name;
        }

        public int GetPyHashCode() => StringComparer.Ordinal.GetHashCode(_name);
    }

}
