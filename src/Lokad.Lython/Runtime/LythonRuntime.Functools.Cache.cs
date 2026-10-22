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
        // Key protocols use the current context; clear/eviction may reenter.
        private ContextualKeyTable<CacheEntry> _cache = new();
        private readonly LinkedList<CacheEntry> _recency = [];
        private PyDict? _metadata;
        private readonly MemoryGovernor _memoryGovernor;
        private long _committedEntryBytes;
        private long _committedKeyBytes;
        private int _activeLookups;
        private long _deferredRefundBytes;
        private BigInteger _hits;
        private BigInteger _misses;

        // Conservative table/bucket capacity, records, recency nodes and
        // replacement headroom. Fresh key storage is separate; argument/result
        // payloads retain their existing execution owners.
        private const long EntryInfrastructureBytes = 384;

        public PyLruCacheWrapper(ICallable callable, int? maxSize, CacheKeyMode keyMode, MemoryGovernor memoryGovernor)
        {
            _callable = callable;
            _maxSize = maxSize;
            _keyMode = keyMode;
            _memoryGovernor = memoryGovernor;
        }


        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (_maxSize == 0)
            {
                _misses++;
                return asynchronous
                    ? await _callable.InvokeAsync(arguments, span, context).ConfigureAwait(false)
                    : _callable.Invoke(arguments, span, context);
            }
            var key = BuildCacheKey(arguments, _keyMode, context, span, out var keyCharge);
            try
            {
                // Like Python's known-hash dictionary calls, hashing happens once.
                // Key protocols remain synchronous; target invocation may suspend.
                var hash = PyHashProtocols.BucketHash(key, context, span);
                if (TryLookup(key, hash, context, span, out var cached))
                {
                    _hits++;
                    Touch(cached);
                    return cached.Value;
                }
                _misses++;
                var result = asynchronous
                    ? await _callable.InvokeAsync(arguments, span, context).ConfigureAwait(false)
                    : _callable.Invoke(arguments, span, context);
                var transferred = keyCharge;
                keyCharge = 0;
                Store(key, transferred, result, span, context, hash);
                return result;
            }
            finally { context.MemoryGovernor.Release(keyCharge); }
        }

        private bool TryLookup(object key, int hash, ExecutionContext context, LythonSourceSpan span,
            [MaybeNullWhen(false)] out CacheEntry value)
        {
            var table = _cache;
            _activeLookups++;
            try
            {
                // Equality can clear/evict/reenter. A detached snapshot is a miss.
                return table.TryGetValue(key, hash, context, span, out value)
                    && ReferenceEquals(table, _cache)
                    && (_maxSize is null || ReferenceEquals(value.Node?.List, _recency));
            }
            finally
            {
                GC.KeepAlive(table);
                if (--_activeLookups == 0)
                {
                    _memoryGovernor.Release(_deferredRefundBytes);
                    _deferredRefundBytes = 0;
                }
            }
        }

        // Clear/eviction relinquishes storage only after all contextual lookups
        // finish using their snapshots. No object ledger or reclamation scan.
        private void RefundEntries(long bytes)
        {
            if (_activeLookups == 0) _memoryGovernor.Release(bytes);
            else _deferredRefundBytes = checked(_deferredRefundBytes + bytes);
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
            => TryGetMemberCore(name, null, null, out value);

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, context, span, out value);

        private bool TryGetMemberCore(string name, ExecutionContext? context, LythonSourceSpan? span,
            [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__") { value = _metadata ??= new PyDict(_memoryGovernor, span); return true; }
            if (TryReadFunctoolsMetadata(_metadata, name, _memoryGovernor, span, context, out value)) return true;
            value = name switch
            {
                "cache_info" => new CacheInfoMethod(this),
                "cache_clear" => new CacheClearMethod(this),
                "cache_parameters" => new CacheParametersMethod(this),
                "__wrapped__" => _callable,
                "__name__" => PyString.FromString("lru_cache_wrapper"),
                "__qualname__" => PyString.FromString("lru_cache_wrapper"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TrySetMember(string name, object value)
            => SetFunctoolsMetadata(ref _metadata, name, value, _memoryGovernor, null);

        public PyString RenderPython(PyRenderingContext context)
        {
            var name = TryReadWrapperMetadata(_callable, "__name__", context.Context, null, out var value) &&
                PyStringOps.TryAsString(value, out var text)
                    ? text.AsString()
                    : "lru_cache_wrapper";
            return PyString.FromString($"<functools._lru_cache_wrapper {name}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private void Store(object key, long keyCharge, object value, LythonSourceSpan span, ExecutionContext context, int hash)
        {
            var published = false;
            try
            {
                // A recursive call may already have stored a result. Preserve it.
                if (TryLookup(key, hash, context, span, out var existing))
                {
                    Touch(existing);
                    return;
                }
                using var storage = _memoryGovernor.ReserveTemporary(EntryInfrastructureBytes, span);
                var entry = new CacheEntry(value, keyCharge, hash);
                if (_maxSize is not null) entry.Node = new LinkedListNode<CacheEntry>(entry);
                entry.Location = _cache.Add(key, entry, hash);
                if (entry.Node is { } node) _recency.AddLast(node);
                storage.Commit(EntryInfrastructureBytes);
                _committedEntryBytes += EntryInfrastructureBytes;
                _committedKeyBytes += keyCharge;
                published = true;

                if (_maxSize is { } limit && _cache.Count > limit)
                {
                    var oldest = _recency.First!.Value;
                    _cache.Remove(oldest.Location!, oldest.Hash);
                    _recency.RemoveFirst();
                    _committedEntryBytes -= EntryInfrastructureBytes;
                    _committedKeyBytes -= oldest.KeyCharge;
                    RefundEntries(EntryInfrastructureBytes + oldest.KeyCharge);
                }
            }
            finally { if (!published) _memoryGovernor.Release(keyCharge); }
        }

        private void Touch(CacheEntry entry)
        {
            if (entry.Node is not { } node || !ReferenceEquals(node.List, _recency) || ReferenceEquals(node, _recency.Last)) return;
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
            RefundEntries(_committedEntryBytes + _committedKeyBytes);
            _cache = new();
            _recency.Clear();
            _committedEntryBytes = 0;
            _committedKeyBytes = 0;
            _hits = BigInteger.Zero;
            _misses = BigInteger.Zero;
        }

        private sealed class CacheEntry(object value, long keyCharge, int hash)
        {
            public object Value { get; } = value;
            public long KeyCharge { get; } = keyCharge;
            public int Hash { get; } = hash;
            public LinkedListNode<CacheEntry>? Node { get; set; }
            public ContextualKeyTable<CacheEntry>.Entry? Location { get; set; }
        }

        private sealed class CacheInfoMethod : ICallable, IPyRenderableValue
        {
            private readonly PyLruCacheWrapper _owner;

            public CacheInfoMethod(PyLruCacheWrapper owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
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
                context.CheckExecution(span);
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
                context.CheckExecution(span);
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
            context.CheckExecution(span);
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

        public async ValueTask<object> GetAsync(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
        {
            if (instance is null) return this;
            if (context is null || span is null)
                throw new InvalidOperationException("cached_property access requires runtime context.");
            if (instance is not PyInstance pyInstance)
                throw new LythonRuntimeException("TypeError", "cached_property can only be accessed on user class instances.", span);
            if (Name is null)
                throw new LythonRuntimeException("TypeError", "cached_property has no bound attribute name.", span);
            if (pyInstance.TryGetOwnAttribute(Name, out var cached)) return cached;

            var resolved = _callable is IPyDescriptor descriptor
                ? await descriptor.GetAsync(instance, owner, context, span).ConfigureAwait(false)
                : new PyBoundMethod(instance, _callable);
            if (resolved is not ICallable callable)
                throw new LythonRuntimeException("TypeError", "Descriptor target must resolve to a callable.", span);
            var value = await callable.InvokeAsync([], span, context).ConfigureAwait(false);
            pyInstance.SetAttribute(Name, value);
            return value;
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
        var wrapper = new PyLruCacheWrapper(callable, maxSize, keyMode, context.MemoryGovernor);
        ApplyUpdateWrapper(wrapper, wrapper, callable, FunctoolsWrapperAssignmentNames, FunctoolsWrapperUpdateNames, context, span);
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
        // Only freshly constructed key storage transfers into the cache;
        // argument payloads retain their original owners.
        long payloadCharge = 0;
        var complete = false;
        try
        {
            var parts = new List<object>();
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
            complete = true;
            return key;
        }
        catch (InvalidOperationException ex)
        {
            throw new LythonRuntimeException("TypeError", ex.Message, span);
        }
        finally { if (!complete) context.MemoryGovernor.Release(payloadCharge); }
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
            case object integer when PyNumberOps.IsInteger(integer):
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
