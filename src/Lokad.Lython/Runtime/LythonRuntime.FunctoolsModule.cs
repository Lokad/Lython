using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
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
                "update_wrapper" => UpdateWrapperCallable.Instance,
                "wraps" => WrapsCallable.Instance,
                "total_ordering" => new BuiltinCallable("functools.total_ordering", TotalOrdering, ["cls"]),
                "reduce" => new BuiltinCallable("functools.reduce", Reduce, ["function", "iterable", "initializer"], requiredCount: 2),
                "partial" => PartialFactory.Instance,
                "cmp_to_key" => new BuiltinCallable("functools.cmp_to_key", CmpToKey, ["mycmp"]),
                _ => null!,
            };

            return value is not null;
        }
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

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.partial>");

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

    private sealed class PartialFactory : ICallable
    {
        public static readonly PartialFactory Instance = new();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.partial(func, ...) expects the first argument to be callable.", span);
            }

            return new PyPartial(callable, arguments[1..]);
        }
    }

    private sealed class UpdateWrapperCallable : ICallable
    {
        public static readonly UpdateWrapperCallable Instance = new();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var wrapper = default(object);
            var wrapped = default(object);
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (wrapper is null)
                    {
                        wrapper = argument.Value;
                    }
                    else if (wrapped is null)
                    {
                        wrapped = argument.Value;
                    }
                    else
                    {
                        throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped[, ...]) expects at most two positional arguments.", span);
                    }

                    continue;
                }

                if (argument.Name == "wrapped")
                {
                    wrapped = argument.Value;
                    continue;
                }

                if (argument.Name is "assigned" or "updated")
                {
                    continue;
                }

                throw CallErrors.UnexpectedKeyword("Builtin", "functools.update_wrapper", argument.Name, span);
            }

            if (wrapper is not IPyDynamicAttributes mutableWrapper || wrapped is null)
            {
                throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped) expects a mutable callable wrapper and a wrapped object.", span);
            }

            if (TryReadWrapperMetadata(wrapped, "__name__", out var name))
            {
                mutableWrapper.TrySetMember("__name__", name);
            }

            if (TryReadWrapperMetadata(wrapped, "__qualname__", out var qualname))
            {
                mutableWrapper.TrySetMember("__qualname__", qualname);
            }

            mutableWrapper.TrySetMember("__wrapped__", wrapped);
            return wrapper;
        }
    }

    private sealed class WrapsCallable : ICallable
    {
        public static readonly WrapsCallable Instance = new();

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

    private static bool TryReadWrapperMetadata(object target, string memberName, out object value)
    {
        return target switch
        {
            IPyDynamicAttributes dynamicAttributes when dynamicAttributes.TryGetMember(memberName, out value) => true,
            PyBuiltinRuntimeType builtinType when builtinType.TryGetMember(memberName, out value) => true,
            PyType type when type.TryGetMember(memberName, out value) => true,
            _ => (value = PyNone.Instance) is not null && false
        };
    }
}
