using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly string[] FunctoolsWrapperAssignmentNames =
    [
        "__module__",
        "__name__",
        "__qualname__",
        "__doc__",
        "__annotations__"
    ];

    private static readonly string[] FunctoolsWrapperUpdateNames = ["__dict__"];

    private static readonly PyNamedTupleType FunctoolsCacheInfoType =
        new("CacheInfo", ["hits", "misses", "maxsize", "currsize"]);

    private sealed class FunctoolsModule : PyModule
    {
        public static readonly FunctoolsModule Instance = new();

        private FunctoolsModule() : base("functools")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "WRAPPER_ASSIGNMENTS" => CreateStringTuple(FunctoolsWrapperAssignmentNames),
                "WRAPPER_UPDATES" => CreateStringTuple(FunctoolsWrapperUpdateNames),
                "Placeholder" => UnsupportedPartialPlaceholder.Instance,
                "update_wrapper" => UpdateWrapperCallable.Instance,
                "wraps" => WrapsCallable.Instance,
                "total_ordering" => new BuiltinCallable(LythonKnownCallableSignatures.FunctoolsTotalOrdering, TotalOrdering),
                "reduce" => new BuiltinCallable(LythonKnownCallableSignatures.FunctoolsReduce, Reduce),
                "partial" => PartialFactory.Instance,
                "partialmethod" => PartialMethodFactory.Instance,
                "cmp_to_key" => new BuiltinCallable(LythonKnownCallableSignatures.FunctoolsCmpToKey, CmpToKey),
                "lru_cache" => LruCacheFactory.Instance,
                "cache" => CacheFactory.Instance,
                "cached_property" => CachedPropertyFactory.Instance,
                "singledispatch" => SingleDispatchFactory.Instance,
                "singledispatchmethod" => SingleDispatchMethodFactory.Instance,
                "recursive_repr" => RecursiveReprFactory.Instance,
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed class UnsupportedPartialPlaceholder : IPyRenderableValue, IPyHashableValue
    {
        public static readonly UnsupportedPartialPlaceholder Instance = new();

        public int GetPyHashCode() => 0x5F3759DF;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.Placeholder");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyPartial : ICallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        private readonly ICallable _callable;
        private readonly CallArgumentValue[] _boundArguments;
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

        public PyPartial(ICallable callable, CallArgumentValue[] boundArguments)
        {
            _callable = callable;
            _boundArguments = boundArguments;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            HashSet<string>? overriddenKeywords = null;
            for (var i = 0; i < arguments.Length; i++)
            {
                var name = arguments[i].Name;
                if (name is null)
                {
                    continue;
                }

                overriddenKeywords ??= new HashSet<string>(StringComparer.Ordinal);
                overriddenKeywords.Add(name);
            }

            var combined = new CallArgumentValue[_boundArguments.Length + arguments.Length];
            var count = 0;

            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].Name is null)
                {
                    combined[count++] = _boundArguments[i];
                }
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is null)
                {
                    combined[count++] = arguments[i];
                }
            }

            for (var i = 0; i < _boundArguments.Length; i++)
            {
                var name = _boundArguments[i].Name;
                if (name is not null && (overriddenKeywords is null || !overriddenKeywords.Contains(name)))
                {
                    combined[count++] = _boundArguments[i];
                }
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is not null)
                {
                    combined[count++] = arguments[i];
                }
            }

            return _callable.Invoke(count == combined.Length ? combined : combined[..count], span, context);
        }

        public bool TryGetMember(string name, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "func" => _callable,
                "args" => BuildArgs(),
                "keywords" => BuildKeywords(),
                "__dict__" => BuildMetadataDict(),
                "__name__" => PyString.FromString("partial"),
                "__qualname__" => PyString.FromString("partial"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "func" => _callable,
                "args" => BuildArgs(context.MemoryGovernor, span),
                "keywords" => BuildKeywords(context, span),
                "__dict__" => BuildMetadataDict(context.MemoryGovernor, span),
                "__name__" => PyString.FromString("partial"),
                "__qualname__" => PyString.FromString("partial"),
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
            var parts = new List<string>
            {
                PyRendering.ToPythonString(_callable, context)
            };

            foreach (var argument in _boundArguments)
            {
                if (argument.Name is null)
                {
                    parts.Add(PyRendering.ToPythonString(argument.Value, context));
                }
            }

            foreach (var argument in _boundArguments)
            {
                if (argument.Name is not null)
                {
                    parts.Add($"{argument.Name}={PyRendering.ToPythonString(argument.Value, context)}");
                }
            }

            return PyString.FromString($"functools.partial({string.Join(", ", parts)})");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private PyTuple BuildArgs()
        {
            return new PyTuple(BuildPositionalArguments());
        }

        private PyTuple BuildArgs(MemoryGovernor governor, LythonSourceSpan span)
        {
            return new PyTuple(BuildPositionalArguments(), governor, span);
        }

        private PyDict BuildKeywords()
        {
            var dict = new PyDict();
            foreach (var argument in _boundArguments)
            {
                if (argument.Name is not null)
                {
                    dict.SetItem(PyString.FromString(argument.Name), argument.Value);
                }
            }

            return dict;
        }

        private PyDict BuildKeywords(ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            foreach (var argument in _boundArguments)
            {
                if (argument.Name is not null)
                {
                    dict.SetItem(PyString.FromString(argument.Name), argument.Value);
                }
            }

            return dict;
        }

        private PyDict BuildMetadataDict()
        {
            var dict = new PyDict();
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        private PyDict BuildMetadataDict(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        private object[] BuildPositionalArguments()
        {
            var count = 0;
            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].Name is null)
                {
                    count++;
                }
            }

            var result = new object[count];
            var index = 0;
            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].Name is null)
                {
                    result[index++] = _boundArguments[i].Value;
                }
            }

            return result;
        }
    }

    private sealed class PartialFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly PartialFactory Instance = new();

        public string Name => "functools.partial";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.partial(func, ...) expects the first argument to be callable.", span);
            }

            EnsureNoUnsupportedPlaceholder(arguments.AsSpan(1), span);
            return new PyPartial(callable, arguments[1..]);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.partial");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyPartialMethod : IPyDescriptor, IPyRenderableValue, IClassOwnedMember
    {
        private readonly ICallable _callable;
        private readonly CallArgumentValue[] _boundArguments;

        public PyPartialMethod(ICallable callable, CallArgumentValue[] boundArguments)
        {
            _callable = callable;
            _boundArguments = boundArguments;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
        {
            if (context is null || span is null)
            {
                return this;
            }

            if (instance is null)
            {
                return this;
            }

            var resolved = _callable is IPyDescriptor descriptor
                ? descriptor.Get(instance, owner, context, span)
                : new PyBoundMethod(instance, _callable);
            if (resolved is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.partialmethod target must resolve to a callable.", span);
            }

            return new PyPartial(callable, _boundArguments);
        }

        public void BindOwner(PyType owner)
        {
            if (_callable is IClassOwnedMember owned)
            {
                owned.BindOwner(owner);
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.partialmethod(...)");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PartialMethodFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly PartialMethodFactory Instance = new();

        public string Name => "functools.partialmethod";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.partialmethod(func, ...) expects the first argument to be callable.", span);
            }

            EnsureNoUnsupportedPlaceholder(arguments.AsSpan(1), span);
            return new PyPartialMethod(callable, arguments[1..]);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.partialmethod");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class UpdateWrapperCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly UpdateWrapperCallable Instance = new();

        public string Name => "functools.update_wrapper";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var parsed = ParseUpdateWrapperArguments(arguments, span);
            if (parsed.Wrapper is not IPyDynamicAttributes mutableWrapper || parsed.Wrapped is null)
            {
                throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped) expects a mutable callable wrapper and a wrapped object.", span);
            }

            var assigned = ReadWrapperMemberNames(parsed.Assigned, "assigned", span);
            var updated = ReadWrapperMemberNames(parsed.Updated, "updated", span);
            ApplyUpdateWrapper(mutableWrapper, parsed.Wrapper, parsed.Wrapped, assigned, updated, context, span);
            return parsed.Wrapper;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.update_wrapper");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class WrapsCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly WrapsCallable Instance = new();

        public string Name => "functools.wraps";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "functools.wraps(wrapped[, ...]) expects at least the wrapped callable.", span);
            }

            var bound = new CallArgumentValue[arguments.Length];
            bound[0] = new CallArgumentValue("wrapped", arguments[0].Value);
            for (var i = 1; i < arguments.Length; i++)
            {
                bound[i] = arguments[i];
            }

            return new PyPartial(UpdateWrapperCallable.Instance, bound);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.wraps");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class LruCacheFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly LruCacheFactory Instance = new();

        public string Name => "functools.lru_cache";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 1 && arguments[0].Name is null && arguments[0].Value is ICallable callable)
            {
                return CreateCacheWrapper(callable, maxSize: 128, typed: false, context, span);
            }

            var parameters = ParseLruCacheParameters(arguments, defaultMaxSize: 128, span);
            return new LruCacheDecorator(parameters.MaxSize, parameters.Typed);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.lru_cache");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CacheFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CacheFactory Instance = new();

        public string Name => "functools.cache";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.cache(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, maxSize: null, typed: false, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.cache");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class LruCacheDecorator : ICallable, IPyRenderableValue
    {
        private readonly int? _maxSize;
        private readonly bool _typed;

        public LruCacheDecorator(int? maxSize, bool typed)
        {
            _maxSize = maxSize;
            _typed = typed;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.lru_cache(...)(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, _maxSize, _typed, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.lru_cache decorator>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyLruCacheWrapper :
        ICallable,
        IPyBindableCallable,
        IPyDynamicAttributes,
        IPyContextualDynamicAttributes,
        IPyRenderableValue,
        IClassOwnedMember
    {
        private readonly ICallable _callable;
        private readonly int? _maxSize;
        private readonly bool _typed;
        private readonly Dictionary<object, object> _cache = new(PyValueComparer.Instance);
        private readonly List<object> _recency = [];
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
        private BigInteger _hits;
        private BigInteger _misses;

        public PyLruCacheWrapper(ICallable callable, int? maxSize, bool typed)
        {
            _callable = callable;
            _maxSize = maxSize;
            _typed = typed;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var key = BuildCacheKey(arguments, _typed, context, span);
            if (_maxSize != 0 && _cache.TryGetValue(key, out var cached))
            {
                _hits++;
                TouchKey(key);
                return cached;
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

        public bool TryGetMember(string name, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "cache_info" => new CacheInfoMethod(this),
                "cache_clear" => new CacheClearMethod(this),
                "cache_parameters" => new CacheParametersMethod(this),
                "__wrapped__" => _callable,
                "__dict__" => BuildMetadataDict(),
                "__name__" => PyString.FromString("lru_cache_wrapper"),
                "__qualname__" => PyString.FromString("lru_cache_wrapper"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "cache_info" => new CacheInfoMethod(this),
                "cache_clear" => new CacheClearMethod(this),
                "cache_parameters" => new CacheParametersMethod(this),
                "__wrapped__" => _callable,
                "__dict__" => BuildMetadataDict(context.MemoryGovernor, span),
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
            _cache[key] = value;
            _recency.Add(key);
            if (_maxSize is int limit && _cache.Count > limit)
            {
                var evicted = _recency[0];
                _recency.RemoveAt(0);
                _cache.Remove(evicted);
            }
        }

        private void TouchKey(object key)
        {
            for (var i = 0; i < _recency.Count; i++)
            {
                if (PyValueComparer.Instance.Equals(_recency[i], key))
                {
                    var existing = _recency[i];
                    _recency.RemoveAt(i);
                    _recency.Add(existing);
                    return;
                }
            }
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
            dict.SetItem(PyString.FromString("typed"), _typed);
            return dict;
        }

        private void Clear()
        {
            _cache.Clear();
            _recency.Clear();
            _hits = BigInteger.Zero;
            _misses = BigInteger.Zero;
        }

        private PyDict BuildMetadataDict()
        {
            var dict = new PyDict();
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        private PyDict BuildMetadataDict(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
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
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
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

    private sealed class PyCachedProperty : IPyDescriptor, IClassNamedMember, IClassOwnedMember, IPyDynamicAttributes, IPyRenderableValue
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

        public bool TryGetMember(string name, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
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

    private sealed class SingleDispatchFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly SingleDispatchFactory Instance = new();

        public string Name => "functools.singledispatch";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.singledispatch(func) expects one callable argument.", span);
            }

            var dispatcher = new PySingleDispatchDispatcher(callable, methodMode: false);
            ApplyUpdateWrapper(dispatcher, dispatcher, callable, FunctoolsWrapperAssignmentNames, FunctoolsWrapperUpdateNames, context, span);
            return dispatcher;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.singledispatch");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SingleDispatchMethodFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly SingleDispatchMethodFactory Instance = new();

        public string Name => "functools.singledispatchmethod";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.singledispatchmethod(func) expects one callable argument.", span);
            }

            var dispatcher = new PySingleDispatchDispatcher(callable, methodMode: true);
            ApplyUpdateWrapper(dispatcher, dispatcher, callable, FunctoolsWrapperAssignmentNames, FunctoolsWrapperUpdateNames, context, span);
            return new PySingleDispatchMethod(dispatcher);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.singledispatchmethod");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PySingleDispatchDispatcher :
        ICallable,
        IPyBindableCallable,
        IPyDynamicAttributes,
        IPyContextualDynamicAttributes,
        IPyRenderableValue,
        IClassOwnedMember
    {
        private readonly ICallable _defaultCallable;
        private readonly bool _methodMode;
        private readonly List<SingleDispatchRegistration> _registrations = [];
        private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

        public PySingleDispatchDispatcher(ICallable defaultCallable, bool methodMode)
        {
            _defaultCallable = defaultCallable;
            _methodMode = methodMode;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var dispatchIndex = _methodMode ? 1 : 0;
            if (arguments.Length <= dispatchIndex)
            {
                throw new LythonRuntimeException("TypeError", "singledispatch function requires at least one dispatch argument.", span);
            }

            var callable = ResolveForValue(arguments[dispatchIndex].Value);
            return callable.Invoke(arguments, span, context);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public void BindOwner(PyType owner)
        {
            if (_defaultCallable is IClassOwnedMember owned)
            {
                owned.BindOwner(owner);
            }

            foreach (var registration in _registrations)
            {
                if (registration.Callable is IClassOwnedMember registeredOwned)
                {
                    registeredOwned.BindOwner(owner);
                }
            }
        }

        public bool TryGetMember(string name, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "register" => new SingleDispatchRegisterMethod(this),
                "dispatch" => new SingleDispatchDispatchMethod(this),
                "registry" => BuildRegistry(),
                "__wrapped__" => _defaultCallable,
                "__dict__" => BuildMetadataDict(),
                "__name__" => PyString.FromString("singledispatch"),
                "__qualname__" => PyString.FromString("singledispatch"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            if (_metadata.TryGetValue(name, out value!))
            {
                return true;
            }

            value = name switch
            {
                "register" => new SingleDispatchRegisterMethod(this),
                "dispatch" => new SingleDispatchDispatchMethod(this),
                "registry" => BuildRegistry(context.MemoryGovernor, span),
                "__wrapped__" => _defaultCallable,
                "__dict__" => BuildMetadataDict(context.MemoryGovernor, span),
                "__name__" => PyString.FromString("singledispatch"),
                "__qualname__" => PyString.FromString("singledispatch"),
                _ => PyNone.Instance
            };
            return !ReferenceEquals(value, PyNone.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _metadata[name] = value;
            return true;
        }

        public ICallable ResolveForValue(object value)
        {
            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                if (IsInstanceAgainstSingleType(value, _registrations[i].TypeSpec))
                {
                    return _registrations[i].Callable;
                }
            }

            return _defaultCallable;
        }

        public ICallable ResolveForType(object typeSpec, LythonSourceSpan span)
        {
            if (!IsSupportedTypeSpecifier(typeSpec))
            {
                throw new LythonRuntimeException("TypeError", "singledispatch.dispatch(cls) expects a supported class/type argument.", span);
            }

            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                if (DoesDispatchTypeMatch(typeSpec, _registrations[i].TypeSpec))
                {
                    return _registrations[i].Callable;
                }
            }

            return _defaultCallable;
        }

        public void Register(object typeSpec, ICallable callable, LythonSourceSpan span)
        {
            if (!IsSupportedTypeSpecifier(typeSpec))
            {
                throw new LythonRuntimeException("TypeError", "singledispatch.register(cls, func) expects cls to be a supported class/type.", span);
            }

            _registrations.RemoveAll(registration => DispatchTypeIdentityEquals(registration.TypeSpec, typeSpec));
            _registrations.Add(new SingleDispatchRegistration(typeSpec, callable));
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var name = TryReadWrapperMetadata(this, "__name__", context.Context, null, out var value) &&
                PyStringOps.TryAsString(value, out var text)
                    ? text.AsString()
                    : "singledispatch";
            return PyString.FromString($"<function {name}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private PyDict BuildRegistry()
        {
            var dict = new PyDict();
            foreach (var registration in _registrations)
            {
                dict.SetItem(registration.TypeSpec, registration.Callable);
            }

            return dict;
        }

        private PyDict BuildRegistry(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            foreach (var registration in _registrations)
            {
                dict.SetItem(registration.TypeSpec, registration.Callable);
            }

            return dict;
        }

        private PyDict BuildMetadataDict()
        {
            var dict = new PyDict();
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        private PyDict BuildMetadataDict(MemoryGovernor governor, LythonSourceSpan span)
        {
            var dict = new PyDict(governor, span);
            foreach (var pair in _metadata)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        private readonly record struct SingleDispatchRegistration(object TypeSpec, ICallable Callable);

        private sealed class SingleDispatchRegisterMethod : ICallable, IPyRenderableValue
        {
            private readonly PySingleDispatchDispatcher _owner;

            public SingleDispatchRegisterMethod(PySingleDispatchDispatcher owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length == 1 && arguments[0].Name is null)
                {
                    if (!IsSupportedTypeSpecifier(arguments[0].Value))
                    {
                        if (arguments[0].Value is ICallable)
                        {
                            throw new LythonRuntimeException("TypeError", "singledispatch.register(func) without an explicit type is unsupported; pass register(cls).", span);
                        }

                        throw new LythonRuntimeException("TypeError", "singledispatch.register(cls) expects a supported class/type.", span);
                    }

                    return new SingleDispatchRegistrationDecorator(_owner, arguments[0].Value);
                }

                if (arguments.Length == 2 &&
                    arguments[0].Name is null &&
                    arguments[1].Name is null &&
                    arguments[1].Value is ICallable callable)
                {
                    _owner.Register(arguments[0].Value, callable, span);
                    return callable;
                }

                throw new LythonRuntimeException("TypeError", "singledispatch.register(cls[, func]) expects a class/type and an optional callable.", span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<singledispatch.register>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private sealed class SingleDispatchRegistrationDecorator : ICallable, IPyRenderableValue
        {
            private readonly PySingleDispatchDispatcher _owner;
            private readonly object _typeSpec;

            public SingleDispatchRegistrationDecorator(PySingleDispatchDispatcher owner, object typeSpec)
            {
                _owner = owner;
                _typeSpec = typeSpec;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
                {
                    throw new LythonRuntimeException("TypeError", "singledispatch.register(cls)(func) expects one callable argument.", span);
                }

                _owner.Register(_typeSpec, callable, span);
                return callable;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<singledispatch.register decorator>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private sealed class SingleDispatchDispatchMethod : ICallable, IPyRenderableValue
        {
            private readonly PySingleDispatchDispatcher _owner;

            public SingleDispatchDispatchMethod(PySingleDispatchDispatcher owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length != 1 || arguments[0].Name is not null)
                {
                    throw new LythonRuntimeException("TypeError", "singledispatch.dispatch(cls) expects one class/type argument.", span);
                }

                return _owner.ResolveForType(arguments[0].Value, span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<singledispatch.dispatch>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
    }

    private sealed class PySingleDispatchMethod : IPyDescriptor, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, IClassOwnedMember
    {
        private readonly PySingleDispatchDispatcher _dispatcher;

        public PySingleDispatchMethod(PySingleDispatchDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : new BoundSingleDispatchMethod(instance, _dispatcher);

        public void BindOwner(PyType owner) => _dispatcher.BindOwner(owner);

        public bool TryGetMember(string name, out object value) => _dispatcher.TryGetMember(name, out value);

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, out object value)
            => _dispatcher.TryGetMember(name, context, span, out value);

        public bool TrySetMember(string name, object value) => _dispatcher.TrySetMember(name, value);

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.singledispatchmethod>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class BoundSingleDispatchMethod : ICallable, IPyRenderableValue
    {
        private readonly object _self;
        private readonly PySingleDispatchDispatcher _dispatcher;

        public BoundSingleDispatchMethod(object self, PySingleDispatchDispatcher dispatcher)
        {
            _self = self;
            _dispatcher = dispatcher;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "singledispatchmethod call requires a dispatch argument.", span);
            }

            var callable = _dispatcher.ResolveForValue(arguments[0].Value);
            var forwarded = new CallArgumentValue[arguments.Length + 1];
            forwarded[0] = new CallArgumentValue(null, _self);
            Array.Copy(arguments, 0, forwarded, 1, arguments.Length);
            return callable.Invoke(forwarded, span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<bound singledispatchmethod>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class RecursiveReprFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly RecursiveReprFactory Instance = new();

        public string Name => "functools.recursive_repr";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var fillValue = PyString.FromString("...");
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "functools.recursive_repr(fillvalue='...') expects zero or one argument.", span);
            }

            if (arguments.Length == 1)
            {
                var keywordName = arguments[0].Name;
                if (keywordName is not null && keywordName != "fillvalue")
                {
                    throw CallErrors.UnexpectedKeyword("Builtin", "functools.recursive_repr", keywordName, span);
                }

                if (!PyStringOps.TryAsString(arguments[0].Value, out fillValue))
                {
                    throw new LythonRuntimeException("TypeError", "functools.recursive_repr(fillvalue=...) expects a string fill value.", span);
                }
            }

            return new RecursiveReprDecorator(fillValue);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.recursive_repr");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class RecursiveReprDecorator : ICallable, IPyRenderableValue
    {
        private readonly PyString _fillValue;

        public RecursiveReprDecorator(PyString fillValue)
        {
            _fillValue = fillValue;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.recursive_repr(...)(func) expects one callable argument.", span);
            }

            return new PyRecursiveReprWrapper(callable, _fillValue);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.recursive_repr decorator>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyRecursiveReprWrapper : ICallable, IPyBindableCallable, IPyRenderableValue, IClassOwnedMember
    {
        private readonly ICallable _callable;
        private readonly PyString _fillValue;
        private readonly HashSet<object> _active = new(ReferenceEqualityComparer.Instance);

        public PyRecursiveReprWrapper(ICallable callable, PyString fillValue)
        {
            _callable = callable;
            _fillValue = fillValue;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var key = arguments.Length == 0 ? this : arguments[0].Value;
            if (!_active.Add(key))
            {
                return _fillValue;
            }

            try
            {
                return _callable.Invoke(arguments, span, context);
            }
            finally
            {
                _active.Remove(key);
            }
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

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<recursive_repr wrapper>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCmpKeyFactory : ICallable, IPyRenderableValue
    {
        private readonly ICallable _comparer;

        public PyCmpKeyFactory(ICallable comparer)
        {
            _comparer = comparer;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "functools.cmp_to_key(cmp)(value) expects one positional argument.", span);
            }

            return new PyCmpKey(_comparer, arguments[0].Value);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.KeyWrapper>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCmpKey : IPyRenderableValue
    {
        public PyCmpKey(ICallable comparer, object value)
        {
            Comparer = comparer;
            Value = value;
        }

        public ICallable Comparer { get; }

        public object Value { get; }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.KeyWrapper>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private enum OrderingMethod
    {
        Lt,
        Le,
        Gt,
        Ge,
    }

    private sealed class TotalOrderingMethod : IPyBindableCallable
    {
        private readonly OrderingMethod _rootMethod;
        private readonly OrderingMethod _generatedMethod;

        public TotalOrderingMethod(OrderingMethod rootMethod, OrderingMethod generatedMethod)
        {
            _rootMethod = rootMethod;
            _generatedMethod = generatedMethod;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0].Name is not null || arguments[1].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "generated ordering method expects self and other.", span);
            }

            var self = arguments[0].Value;
            var other = arguments[1].Value;
            var equal = InvokeEq(self, other, context, span);
            return _generatedMethod switch
            {
                OrderingMethod.Lt => ComputeLt(self, other, equal, context, span),
                OrderingMethod.Le => ComputeLt(self, other, equal, context, span) || equal,
                OrderingMethod.Gt => ComputeGt(self, other, equal, context, span),
                OrderingMethod.Ge => ComputeGt(self, other, equal, context, span) || equal,
                _ => throw new InvalidOperationException("Unknown generated ordering method.")
            };
        }

        private bool ComputeLt(object self, object other, bool equal, ExecutionContext context, LythonSourceSpan span)
        {
            return _rootMethod switch
            {
                OrderingMethod.Lt => InvokeBool(self, OrderingMethod.Lt, other, context, span),
                OrderingMethod.Le => InvokeBool(self, OrderingMethod.Le, other, context, span) && !equal,
                OrderingMethod.Gt => InvokeBool(other, OrderingMethod.Gt, self, context, span),
                OrderingMethod.Ge => InvokeBool(other, OrderingMethod.Ge, self, context, span) && !equal,
                _ => false
            };
        }

        private bool ComputeGt(object self, object other, bool equal, ExecutionContext context, LythonSourceSpan span)
        {
            return _rootMethod switch
            {
                OrderingMethod.Lt => InvokeBool(other, OrderingMethod.Lt, self, context, span),
                OrderingMethod.Le => InvokeBool(other, OrderingMethod.Le, self, context, span) && !equal,
                OrderingMethod.Gt => InvokeBool(self, OrderingMethod.Gt, other, context, span),
                OrderingMethod.Ge => InvokeBool(self, OrderingMethod.Ge, other, context, span) && !equal,
                _ => false
            };
        }

        private static bool InvokeEq(object self, object other, ExecutionContext context, LythonSourceSpan span)
            => InvokeNamedBool(self, "__eq__", other, context, span);

        private static bool InvokeBool(object self, OrderingMethod method, object other, ExecutionContext context, LythonSourceSpan span)
            => InvokeNamedBool(self, MethodName(method), other, context, span);

        private static bool InvokeNamedBool(object self, string methodName, object other, ExecutionContext context, LythonSourceSpan span)
        {
            if (!PyMemberAccess.TryResolve(self, methodName, context, span, out var member) || member is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", $"Object has no callable '{methodName}' method.", span);
            }

            var result = callable.Invoke([new CallArgumentValue(null, other)], span, context);
            return result switch
            {
                bool boolean => boolean,
                _ => throw new LythonRuntimeException("TypeError", $"'{methodName}' must return bool.", span)
            };
        }

        private static string MethodName(OrderingMethod method)
        {
            return method switch
            {
                OrderingMethod.Lt => "__lt__",
                OrderingMethod.Le => "__le__",
                OrderingMethod.Gt => "__gt__",
                OrderingMethod.Ge => "__ge__",
                _ => throw new InvalidOperationException("Unknown ordering method.")
            };
        }
    }

    private static object TotalOrdering(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "functools.total_ordering(cls) expects one class argument.", span);
        }

        if (!type.TryGetOwnMember("__eq__", out _))
        {
            throw new LythonRuntimeException("TypeError", "functools.total_ordering requires __eq__ to be defined.", span);
        }

        var root = GetRootOrderingMethod(type, span);
        foreach (var method in new[] { OrderingMethod.Lt, OrderingMethod.Le, OrderingMethod.Gt, OrderingMethod.Ge })
        {
            var methodName = OrderingMethodName(method);
            if (method == root || type.TryGetOwnMember(methodName, out _))
            {
                continue;
            }

            type.TrySetMember(methodName, new TotalOrderingMethod(root, method));
        }

        return type;
    }

    private static OrderingMethod GetRootOrderingMethod(PyType type, LythonSourceSpan span)
    {
        if (type.TryGetOwnMember("__lt__", out _))
        {
            return OrderingMethod.Lt;
        }

        if (type.TryGetOwnMember("__le__", out _))
        {
            return OrderingMethod.Le;
        }

        if (type.TryGetOwnMember("__gt__", out _))
        {
            return OrderingMethod.Gt;
        }

        if (type.TryGetOwnMember("__ge__", out _))
        {
            return OrderingMethod.Ge;
        }

        throw new LythonRuntimeException("TypeError", "functools.total_ordering requires one of __lt__, __le__, __gt__, or __ge__.", span);
    }

    private static object Reduce(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.reduce(function, iterable[, initializer]) expects a callable, an iterable, and an optional initializer.", span);
        }

        using var enumerator = ToSequence(arguments[1], span).GetEnumerator();
        object accumulator;
        if (arguments.Length == 3)
        {
            accumulator = arguments[2];
        }
        else
        {
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "functools.reduce() of empty sequence with no initial value.", span);
            }

            accumulator = RuntimeValue(enumerator.Current);
        }

        while (enumerator.MoveNext())
        {
            accumulator = callable.Invoke(
                [new CallArgumentValue(null, accumulator), new CallArgumentValue(null, RuntimeValue(enumerator.Current))],
                span,
                context);
        }

        return accumulator;
    }

    private static object CmpToKey(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.cmp_to_key(mycmp) expects one callable argument.", span);
        }

        return new PyCmpKeyFactory(callable);
    }

    private static string OrderingMethodName(OrderingMethod method)
    {
        return method switch
        {
            OrderingMethod.Lt => "__lt__",
            OrderingMethod.Le => "__le__",
            OrderingMethod.Gt => "__gt__",
            OrderingMethod.Ge => "__ge__",
            _ => throw new InvalidOperationException("Unknown ordering method.")
        };
    }

    private static PyTuple CreateStringTuple(IReadOnlyList<string> values)
        => new(values.Select(PyString.FromString).Cast<object>());

    private static PyLruCacheWrapper CreateCacheWrapper(ICallable callable, int? maxSize, bool typed, ExecutionContext context, LythonSourceSpan span)
    {
        var wrapper = new PyLruCacheWrapper(callable, maxSize, typed);
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
            if (argument.Name is null)
            {
                switch (positionalIndex++)
                {
                    case 0:
                        if (seenWrapper)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "wrapper", span);
                        }

                        wrapper = argument.Value;
                        seenWrapper = true;
                        break;
                    case 1:
                        if (seenWrapped)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "wrapped", span);
                        }

                        wrapped = argument.Value;
                        seenWrapped = true;
                        break;
                    case 2:
                        if (seenAssigned)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "assigned", span);
                        }

                        assigned = argument.Value;
                        seenAssigned = true;
                        break;
                    case 3:
                        if (seenUpdated)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "updated", span);
                        }

                        updated = argument.Value;
                        seenUpdated = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped[, assigned][, updated]) expects at most four positional arguments.", span);
                }

                continue;
            }

            switch (argument.Name)
            {
                case "wrapper":
                    if (seenWrapper)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "wrapper", span);
                    }

                    wrapper = argument.Value;
                    seenWrapper = true;
                    break;
                case "wrapped":
                    if (seenWrapped)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "wrapped", span);
                    }

                    wrapped = argument.Value;
                    seenWrapped = true;
                    break;
                case "assigned":
                    if (seenAssigned)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "assigned", span);
                    }

                    assigned = argument.Value;
                    seenAssigned = true;
                    break;
                case "updated":
                    if (seenUpdated)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.update_wrapper", "updated", span);
                    }

                    updated = argument.Value;
                    seenUpdated = true;
                    break;
                default:
                    throw CallErrors.UnexpectedKeyword("Builtin", "functools.update_wrapper", argument.Name, span);
            }
        }

        return new UpdateWrapperArguments(wrapper, wrapped, assigned, updated);
    }

    private static IReadOnlyList<string> ReadWrapperMemberNames(object value, string parameterName, LythonSourceSpan span)
    {
        var result = new List<string>();
        foreach (var item in ToSequence(value, span))
        {
            if (!PyStringOps.TryAsString(item, out var name))
            {
                throw new LythonRuntimeException("TypeError", $"functools.update_wrapper(..., {parameterName}=...) expects an iterable of strings.", span);
            }

            result.Add(name.AsString());
        }

        return result;
    }

    private static void ApplyUpdateWrapper(
        IPyDynamicAttributes mutableWrapper,
        object wrapper,
        object wrapped,
        IReadOnlyList<string> assigned,
        IReadOnlyList<string> updated,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        foreach (var name in assigned)
        {
            if (TryReadWrapperMetadata(wrapped, name, context, span, out var value))
            {
                mutableWrapper.TrySetMember(name, value);
            }
        }

        foreach (var name in updated)
        {
            if (TryReadWrapperMetadata(wrapper, name, context, span, out var wrapperValue) &&
                TryReadWrapperMetadata(wrapped, name, context, span, out var wrappedValue) &&
                wrapperValue is PyDict wrapperDict &&
                wrappedValue is PyDict wrappedDict)
            {
                foreach (var pair in wrappedDict)
                {
                    wrapperDict.SetItem(pair.Key, pair.Value);
                }

                mutableWrapper.TrySetMember(name, wrapperDict);
            }
        }

        mutableWrapper.TrySetMember("__wrapped__", wrapped);
    }

    private static bool TryReadWrapperMetadata(object target, string memberName, ExecutionContext? context, LythonSourceSpan? span, out object value)
    {
        if (context is not null && span is not null && PyMemberAccess.TryResolve(target, memberName, context, span, out value))
        {
            return true;
        }

        return target switch
        {
            IPyDynamicAttributes dynamicAttributes when dynamicAttributes.TryGetMember(memberName, out value) => true,
            PyBuiltinRuntimeType builtinType when builtinType.TryGetMember(memberName, out value) => true,
            PyType type when type.TryGetMember(memberName, out value) => true,
            _ => (value = PyNone.Instance) is not null && false
        };
    }

    private static (int? MaxSize, bool Typed) ParseLruCacheParameters(CallArgumentValue[] arguments, int? defaultMaxSize, LythonSourceSpan span)
    {
        object maxSizeValue = defaultMaxSize is int maxSize ? new BigInteger(maxSize) : PyNone.Instance;
        object typedValue = false;
        var seenMaxSize = false;
        var seenTyped = false;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                switch (positionalIndex++)
                {
                    case 0:
                        if (seenMaxSize)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.lru_cache", "maxsize", span);
                        }

                        maxSizeValue = argument.Value;
                        seenMaxSize = true;
                        break;
                    case 1:
                        if (seenTyped)
                        {
                            throw CallErrors.MultipleValues("Builtin", "functools.lru_cache", "typed", span);
                        }

                        typedValue = argument.Value;
                        seenTyped = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "functools.lru_cache(maxsize=128, typed=False) expects at most two arguments.", span);
                }

                continue;
            }

            switch (argument.Name)
            {
                case "maxsize":
                    if (seenMaxSize)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.lru_cache", "maxsize", span);
                    }

                    maxSizeValue = argument.Value;
                    seenMaxSize = true;
                    break;
                case "typed":
                    if (seenTyped)
                    {
                        throw CallErrors.MultipleValues("Builtin", "functools.lru_cache", "typed", span);
                    }

                    typedValue = argument.Value;
                    seenTyped = true;
                    break;
                default:
                    throw CallErrors.UnexpectedKeyword("Builtin", "functools.lru_cache", argument.Name, span);
            }
        }

        return (ParseCacheMaxSize(maxSizeValue, span), IsTruthy(typedValue));
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

    private static object BuildCacheKey(CallArgumentValue[] arguments, bool typed, ExecutionContext context, LythonSourceSpan span)
    {
        try
        {
            var parts = new List<object>();
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    parts.Add(ValidateDictionaryKey(argument.Value, span, context.MemoryGovernor));
                    continue;
                }

                parts.Add(CacheKeyMarker.Keyword);
                parts.Add(PyString.FromString(argument.Name));
                parts.Add(ValidateDictionaryKey(argument.Value, span, context.MemoryGovernor));
            }

            if (typed)
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
            PyString or string => "str",
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

    private static bool DoesDispatchTypeMatch(object requestedType, object registeredType)
    {
        if (DispatchTypeIdentityEquals(requestedType, registeredType))
        {
            return true;
        }

        if (requestedType is PyType type && registeredType is PyType baseType)
        {
            return type.IsSubtypeOf(baseType);
        }

        return false;
    }

    private static bool DispatchTypeIdentityEquals(object left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        var leftName = GetDispatchTypeName(left);
        var rightName = GetDispatchTypeName(right);
        return leftName is not null &&
               rightName is not null &&
               string.Equals(leftName, rightName, StringComparison.Ordinal);
    }

    private static string? GetDispatchTypeName(object value)
    {
        return value switch
        {
            PyType type => type.Name,
            PyBuiltinRuntimeType builtinType => builtinType.Name,
            BuiltinCallable builtin => builtin.Name,
            INamedRuntimeCallable named => named.Name,
            _ => null
        };
    }

    private static void EnsureNoUnsupportedPlaceholder(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (ReferenceEquals(argument.Value, UnsupportedPartialPlaceholder.Instance))
            {
                throw new LythonRuntimeException("NotImplementedError", "functools.Placeholder is not supported by Lython partial objects.", span);
            }
        }
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

    private readonly record struct UpdateWrapperArguments(object? Wrapper, object? Wrapped, object Assigned, object Updated);
}
