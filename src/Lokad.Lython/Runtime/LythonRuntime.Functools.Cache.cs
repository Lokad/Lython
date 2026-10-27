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
        public object TypedValue { get; init; } = false;
        // Key protocols use the current context; clear/eviction may reenter.
        private ContextualKeyTable<CacheEntry> _cache = new();
        private readonly LinkedList<CacheEntry> _recency = [];
        private PyDict? _metadata;
        private readonly MemoryGovernor _memoryGovernor;
        private readonly ExecutionState _ownerState;
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

        public PyLruCacheWrapper(ICallable callable, int? maxSize, CacheKeyMode keyMode, ExecutionState ownerState)
        {
            _callable = callable;
            _maxSize = maxSize;
            _keyMode = keyMode;
            _ownerState = ownerState;
            _memoryGovernor = ownerState.MemoryGovernor;
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

        private PyNamedTupleObject BuildCacheInfo(LythonSourceSpan span, ExecutionContext context)
        {
            object maxSizeValue = _maxSize is int limit ? new BigInteger(limit) : PyNone.Instance;
            var type = _ownerState.FunctoolsCacheInfoType ??= new PyNamedTupleType(
                "CacheInfo", ["hits", "misses", "maxsize", "currsize"], null, _memoryGovernor, span);
            return type.CreateFromValues(
                [_hits, _misses, maxSizeValue, new BigInteger(_cache.Count)],
                context.MemoryGovernor, span);
        }

        private PyDict BuildCacheParameters(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            dict.SetItem(PyString.FromString("maxsize"), _maxSize is int limit ? new BigInteger(limit) : PyNone.Instance);
            dict.SetItem(PyString.FromString("typed"), TypedValue);
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

                return _owner.BuildCacheInfo(span, context);
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
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
                throw RuntimeErrors.Type("functools.cached_property(func) expects one argument.", span);
            var property = new PyCachedProperty(context.MemoryGovernor, span);
            property.TrySetMember("func", arguments[0].Value);
            property.TrySetMember("attrname", PyNone.Instance);
            foreach (var name in new[] { "__doc__", "__module__" })
                property.TrySetMember(name, await ReadWrapperAttributeAsync(arguments[0].Value, name, context, span, asynchronous).ConfigureAwait(false));
            return property;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class 'functools.cached_property'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCachedProperty(MemoryGovernor governor, LythonSourceSpan span) : IPyDescriptor, IClassNamedMember,
        IClassOwnedMember, IPyMutableDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue
    {
        private PyDict? _metadata;
        public void BindName(string name)
        {
            if (!TryGetMember("attrname", out var previous) || previous is PyNone)
                TrySetMember("attrname", PyString.FromString(name, governor, span));
            else
            {
                if (previous is not PyString text) throw RuntimeErrors.Type("cached_property attrname must be a string for name initialization", span);
                using var decoding = governor.ReserveTemporary(24L + 2L * text.GetUtf16CodeUnitCount(), span);
                if (text.AsString() != name) throw RuntimeErrors.Type("Cannot assign the same cached_property to two different names.", span);
            }
        }
        public void BindOwner(PyType owner)
        {
            if (TryGetMember("func", out var function) && function is IClassOwnedMember owned) owned.BindOwner(owner);
        }
        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? sourceSpan)
            => GetCoreAsync(instance, context, sourceSpan, false).GetAwaiter().GetResult();
        public ValueTask<object> GetAsync(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? sourceSpan)
            => GetCoreAsync(instance, context, sourceSpan, true);
        private async ValueTask<object> GetCoreAsync(object? instance, ExecutionContext? context, LythonSourceSpan? sourceSpan, bool asynchronous)
        {
            if (instance is null) return this;
            if (context is null || sourceSpan is null) throw new InvalidOperationException("cached_property access requires runtime context.");
            context.CheckExecution(sourceSpan);
            var name = await ReadWrapperAttributeAsync(this, "attrname", context, sourceSpan, asynchronous).ConfigureAwait(false);
            if (name is PyNone) throw RuntimeErrors.Type("cached_property has no bound attribute name.", sourceSpan);
            var cache = await ReadWrapperAttributeAsync(instance, "__dict__", context, sourceSpan, asynchronous).ConfigureAwait(false);
            object value;
            if (cache is PyDict dict)
            {
                if (dict.TryGetValue(name, out var cached, context, sourceSpan)) return cached;
            }
            else
            {
                var get = await ReadWrapperAttributeAsync(cache, "get", context, sourceSpan, asynchronous).ConfigureAwait(false);
                value = asynchronous ? await CallableInvocation.InvokeBinaryAsync(get, name, MissingMemberValue.Instance, sourceSpan, context).ConfigureAwait(false)
                    : CallableInvocation.InvokeBinary(get, name, MissingMemberValue.Instance, sourceSpan, context);
                if (!ReferenceEquals(value, MissingMemberValue.Instance)) return value;
            }
            var function = await ReadWrapperAttributeAsync(this, "func", context, sourceSpan, asynchronous).ConfigureAwait(false);
            value = asynchronous ? await CallableInvocation.InvokeUnaryAsync(function, instance, sourceSpan, context).ConfigureAwait(false)
                : CallableInvocation.InvokeUnary(function, instance, sourceSpan, context);
            if (asynchronous) await SetHeaderSubscriptAsync(cache, name, value, sourceSpan, context).ConfigureAwait(false);
            else SetSubscriptValue(cache, name, value, sourceSpan, context);
            return value;
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, null, span, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan sourceSpan, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, context, sourceSpan, out value);
        private bool TryGetMemberCore(string name, ExecutionContext? context, LythonSourceSpan sourceSpan, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__") { value = _metadata ??= new PyDict(governor, sourceSpan); return true; }
            if (TryReadFunctoolsMetadata(_metadata, name, governor, sourceSpan, context, out value)) return true;
            if (name != "__set_name__") return false;
            value = BuiltinCallable.Create("cached_property.__set_name__", SetName, ["owner", "name"], requiredCount: 2);
            return true;
        }
        private object SetName(object[] arguments, LythonSourceSpan sourceSpan, ExecutionContext context)
        {
            if (arguments[1] is not PyString name) throw RuntimeErrors.Type("cached_property name must be a string", sourceSpan);
            using var decoding = context.MemoryGovernor.ReserveTemporary(24L + 2L * name.GetUtf16CodeUnitCount(), sourceSpan);
            BindName(name.AsString());
            return PyNone.Instance;
        }
        public bool TrySetMember(string name, object value) => SetFunctoolsMetadata(ref _metadata, name, value, governor, span);
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.cached_property>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static async ValueTask<object> CreateCacheWrapperAsync(ICallable callable, int? maxSize, object typed,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var truth = asynchronous ? await IsTruthyAsync(typed, context, span).ConfigureAwait(false) : IsTruthy(typed, context, span);
        var wrapper = new PyLruCacheWrapper(callable, maxSize, truth ? CacheKeyMode.ValuesAndTypes : CacheKeyMode.ValuesOnly, context.State) { TypedValue = typed };
        await ApplyUpdateWrapperAsync(wrapper, callable, FunctoolsWrapperAssignments, FunctoolsWrapperUpdates, context, span, asynchronous).ConfigureAwait(false);
        return wrapper;
    }

    private static UpdateWrapperArguments ParseUpdateWrapperArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        object? wrapper = null;
        object? wrapped = null;
        object assigned = FunctoolsWrapperAssignments;
        object updated = FunctoolsWrapperUpdates;
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
            long partCount = keyMode == CacheKeyMode.ValuesAndTypes ? 1L + arguments.Length : 0;
            foreach (var argument in arguments)
            {
                context.CheckExecution(span);
                partCount = checked(partCount + (argument.IsKeyword ? 3 : 1));
            }
            if (partCount > int.MaxValue) throw RuntimeErrors.Memory("cache key is too large", span);
            using var working = context.MemoryGovernor.ReserveTemporary(checked(32L + 8L * partCount), span);
            var parts = new List<object>((int)partCount);
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
    private static object BuildCacheTypePart(object value, ExecutionContext context, LythonSourceSpan span, ref long payloadCharge)
        => OwnCacheTypeToken(value is PyInstance instance ? instance.Type
            : TryGetValueClass(value, context, out var type) ? type : value.GetType(), context, span, ref payloadCharge);

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
