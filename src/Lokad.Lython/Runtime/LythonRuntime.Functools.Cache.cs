using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

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
        private readonly Dictionary<object, CacheEntry> _cache = new(PyValueComparer.Instance);
        private readonly LinkedList<object> _recency = [];
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
        private BigInteger _hits;
        private BigInteger _misses;

        public PyLruCacheWrapper(ICallable callable, int? maxSize, CacheKeyMode keyMode)
        {
            _callable = callable;
            _maxSize = maxSize;
            _keyMode = keyMode;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var key = BuildCacheKey(arguments, _keyMode, context, span);
            if (_maxSize != 0 && _cache.TryGetValue(key, out var cached))
            {
                _hits++;
                Touch(cached);
                return cached.Value;
            }

            _misses++;
            var result = _callable.Invoke(arguments, span, context);
            if (_maxSize != 0)
            {
                Store(key, result);
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

        private void Store(object key, object value)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                existing.Value = value;
                Touch(existing);
                return;
            }

            if (_maxSize is null)
            {
                _cache.Add(key, new CacheEntry(value, node: null));
                return;
            }

            var node = _recency.AddLast(key);
            _cache.Add(key, new CacheEntry(value, node));
            if (_cache.Count > _maxSize.Value)
            {
                var oldest = _recency.First.RequireNotNull();
                _recency.RemoveFirst();
                _cache.Remove(oldest.Value);
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
            _cache.Clear();
            _recency.Clear();
            _hits = BigInteger.Zero;
            _misses = BigInteger.Zero;
        }

        private sealed class CacheEntry
        {
            public CacheEntry(object value, LinkedListNode<object>? node)
            {
                Value = value;
                Node = node;
            }

            public object Value { get; set; }

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

            return new PyCachedProperty(callable);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.cached_property");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCachedProperty : IPyDescriptor, IClassNamedMember, IClassOwnedMember, IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly ICallable _callable;
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

        public PyCachedProperty(ICallable callable)
        {
            _callable = callable;
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
        var wrapper = new PyLruCacheWrapper(callable, maxSize, keyMode);
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
        LythonSourceSpan span)
    {
        try
        {
            var parts = new List<object>();
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    parts.Add(ValidateDictionaryKey(argument.Value, span, context.MemoryGovernor));
                    continue;
                }

                parts.Add(CacheKeyMarker.Keyword);
                parts.Add(PyString.FromString(argument.KeywordName));
                parts.Add(ValidateDictionaryKey(argument.Value, span, context.MemoryGovernor));
            }

            if (keyMode == CacheKeyMode.ValuesAndTypes)
            {
                parts.Add(CacheKeyMarker.Typed);
                foreach (var argument in arguments)
                {
                    parts.Add(PyString.FromString(GetCacheTypeToken(argument.Value, context, span)));
                }
            }

            return new PyTuple(parts, context.MemoryGovernor, span);
        }
        catch (InvalidOperationException ex)
        {
            throw new LythonRuntimeException("TypeError", ex.Message, span);
        }
    }

    private static string GetCacheTypeToken(object value, ExecutionContext context, LythonSourceSpan span)
    {
        return value switch
        {
            PyNone => "NoneType",
            bool => "bool",
            BigInteger or int => "int",
            double => "float",
            PyString => "str",
            PyBytes => "bytes",
            PyList => "list",
            PyTuple => "tuple",
            PyDict => "dict",
            PySet => "set",
            PyInstance instance => instance.Type.Name,
            PyType type => type.Name,
            _ => value.GetType().Name
        };
    }

    private static ICallable BindCallableForInstance(ICallable callable, object instance, PyType owner, ExecutionContext context, LythonSourceSpan span)
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
