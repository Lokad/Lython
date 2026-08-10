using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyPartial : ICallable, IPyRenderableValue, IPyMutableDynamicAttributes, IPyContextualDynamicAttributes
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
                if (arguments[i].IsPositional)
                {
                    continue;
                }

                overriddenKeywords ??= new HashSet<string>(StringComparer.Ordinal);
                overriddenKeywords.Add(arguments[i].KeywordName);
            }

            var combined = new CallArgumentValue[_boundArguments.Length + arguments.Length];
            var count = 0;

            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].IsPositional)
                {
                    combined[count++] = _boundArguments[i];
                }
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    combined[count++] = arguments[i];
                }
            }

            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].IsKeyword &&
                    (overriddenKeywords is null || !overriddenKeywords.Contains(_boundArguments[i].KeywordName)))
                {
                    combined[count++] = _boundArguments[i];
                }
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsKeyword)
                {
                    combined[count++] = arguments[i];
                }
            }

            return _callable.Invoke(count == combined.Length ? combined : combined[..count], span, context);
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
                "args" => BuildArgs(),
                "keywords" => BuildKeywords(),
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata),
                "__name__" => PyString.FromString("partial"),
                "__qualname__" => PyString.FromString("partial"),
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
                "func" => _callable,
                "args" => BuildArgs(context.MemoryGovernor, span),
                "keywords" => BuildKeywords(context, span),
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata, context.MemoryGovernor, span),
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
                if (argument.IsPositional)
                {
                    parts.Add(PyRendering.ToPythonString(argument.Value, context));
                }
            }

            foreach (var argument in _boundArguments)
            {
                if (argument.IsKeyword)
                {
                    parts.Add($"{argument.KeywordName}={PyRendering.ToPythonString(argument.Value, context)}");
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
                if (argument.IsKeyword)
                {
                    dict.SetItem(PyString.FromString(argument.KeywordName), argument.Value);
                }
            }

            return dict;
        }

        private PyDict BuildKeywords(ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            foreach (var argument in _boundArguments)
            {
                if (argument.IsKeyword)
                {
                    dict.SetItem(PyString.FromString(argument.KeywordName), argument.Value);
                }
            }

            return dict;
        }

        private object[] BuildPositionalArguments()
        {
            var count = 0;
            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].IsPositional)
                {
                    count++;
                }
            }

            var result = new object[count];
            var index = 0;
            for (var i = 0; i < _boundArguments.Length; i++)
            {
                if (_boundArguments[i].IsPositional)
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
            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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
            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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
            if (parsed.Wrapper is not IPyMutableDynamicAttributes mutableWrapper || parsed.Wrapped is null)
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
            bound[0] = CallArgumentValue.Keyword("wrapped", arguments[0].Value);
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
        IPyMutableDynamicAttributes mutableWrapper,
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

    private static bool TryReadWrapperMetadata(object target, string memberName, ExecutionContext? context, LythonSourceSpan? span, [MaybeNullWhen(false)] out object value)
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

    private static LruCacheParameters ParseLruCacheParameters(
        CallArgumentValue[] arguments,
        int? defaultMaxSize,
        LythonSourceSpan span)
    {
        object maxSizeValue = defaultMaxSize is int maxSize ? new BigInteger(maxSize) : PyNone.Instance;
        object typedValue = false;
        var seenMaxSize = false;
        var seenTyped = false;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                switch (positionalIndex++)
                {
                    case 0:
                        if (seenMaxSize)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "maxsize", span);
                        }

                        maxSizeValue = argument.Value;
                        seenMaxSize = true;
                        break;
                    case 1:
                        if (seenTyped)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "typed", span);
                        }

                        typedValue = argument.Value;
                        seenTyped = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "functools.lru_cache(maxsize=128, typed=False) expects at most two arguments.", span);
                }

                continue;
            }

            switch (argument.KeywordName)
            {
                case "maxsize":
                    if (seenMaxSize)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "maxsize", span);
                    }

                    maxSizeValue = argument.Value;
                    seenMaxSize = true;
                    break;
                case "typed":
                    if (seenTyped)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "typed", span);
                    }

                    typedValue = argument.Value;
                    seenTyped = true;
                    break;
                default:
                    throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "functools.lru_cache", argument.KeywordName, span);
            }
        }

        var keyMode = IsTruthy(typedValue) ? CacheKeyMode.ValuesAndTypes : CacheKeyMode.ValuesOnly;
        return new LruCacheParameters(ParseCacheMaxSize(maxSizeValue, span), keyMode);
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

    private readonly record struct UpdateWrapperArguments(object? Wrapper, object? Wrapped, object Assigned, object Updated);
}
