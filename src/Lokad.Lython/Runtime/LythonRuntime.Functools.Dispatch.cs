using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class SingleDispatchFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly SingleDispatchFactory Instance = new();

        public string Name => "functools.singledispatch";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_metadata.TryGetValue(name, out value))
            {
                return true;
            }

            value = name switch
            {
                "register" => new SingleDispatchRegisterMethod(this),
                "dispatch" => new SingleDispatchDispatchMethod(this),
                "registry" => BuildRegistry(),
                "__wrapped__" => _defaultCallable,
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata),
                "__name__" => PyString.FromString("singledispatch"),
                "__qualname__" => PyString.FromString("singledispatch"),
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
                "register" => new SingleDispatchRegisterMethod(this),
                "dispatch" => new SingleDispatchDispatchMethod(this),
                "registry" => BuildRegistry(context.MemoryGovernor, span),
                "__wrapped__" => _defaultCallable,
                "__dict__" => BuildFunctoolsMetadataDictionary(_metadata, context.MemoryGovernor, span),
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
            SingleDispatchRegistration? best = null;
            var bestDistance = int.MaxValue;
            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                var registration = _registrations[i];
                if (IsInstanceAgainstSingleType(value, registration.TypeSpec))
                {
                    var distance = GetDispatchDistance(value, registration.TypeSpec);
                    if (distance < bestDistance)
                    {
                        best = registration;
                        bestDistance = distance;
                    }
                }
            }

            return best?.Callable ?? _defaultCallable;
        }

        public ICallable ResolveForType(object typeSpec, LythonSourceSpan span)
        {
            if (!IsSupportedTypeSpecifier(typeSpec))
            {
                throw new LythonRuntimeException("TypeError", "singledispatch.dispatch(cls) expects a supported class/type argument.", span);
            }

            SingleDispatchRegistration? best = null;
            var bestDistance = int.MaxValue;
            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                var registration = _registrations[i];
                if (DoesDispatchTypeMatch(typeSpec, registration.TypeSpec))
                {
                    var distance = GetDispatchTypeDistance(typeSpec, registration.TypeSpec);
                    if (distance < bestDistance)
                    {
                        best = registration;
                        bestDistance = distance;
                    }
                }
            }

            return best?.Callable ?? _defaultCallable;
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

        private readonly record struct SingleDispatchRegistration(object TypeSpec, ICallable Callable);

        private static int GetDispatchDistance(object value, object registeredType)
        {
            if (value is PyInstance instance && registeredType is PyType runtimeType)
            {
                for (var i = 0; i < instance.Type.Mro.Count; i++)
                {
                    if (ReferenceEquals(instance.Type.Mro[i], runtimeType))
                    {
                        return i;
                    }
                }
            }

            var valueTypeName = value switch
            {
                bool => "bool",
                BigInteger or int => "int",
                double => "float",
                PyString or string => "str",
                PyBytes => "bytes",
                PyList => "list",
                PyTuple => "tuple",
                PyDict => "dict",
                PySet => "set",
                PyType type when type.MetaType is not null => type.MetaType.Name,
                _ => null
            };
            return GetNamedDispatchDistance(valueTypeName, GetDispatchTypeName(registeredType));
        }

        private static int GetDispatchTypeDistance(object requestedType, object registeredType)
        {
            if (requestedType is PyType runtimeType && registeredType is PyType runtimeBase)
            {
                for (var i = 0; i < runtimeType.Mro.Count; i++)
                {
                    if (ReferenceEquals(runtimeType.Mro[i], runtimeBase))
                    {
                        return i;
                    }
                }
            }

            return GetNamedDispatchDistance(GetDispatchTypeName(requestedType), GetDispatchTypeName(registeredType));
        }

        private static int GetNamedDispatchDistance(string? requestedType, string? registeredType)
        {
            if (string.Equals(requestedType, registeredType, StringComparison.Ordinal))
            {
                return 0;
            }

            if (requestedType == "bool" && registeredType == "int")
            {
                return 1;
            }

            return registeredType == "object" ? int.MaxValue - 1 : int.MaxValue - 2;
        }

        private sealed class SingleDispatchRegisterMethod : ICallable, IPyRenderableValue
        {
            private readonly PySingleDispatchDispatcher _owner;

            public SingleDispatchRegisterMethod(PySingleDispatchDispatcher owner) => _owner = owner;

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (arguments.Length == 1 && arguments[0].IsPositional)
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
                    arguments[0].IsPositional &&
                    arguments[1].IsPositional &&
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
                if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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
                if (arguments.Length != 1 || arguments[0].IsKeyword)
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

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => _dispatcher.TryGetMember(name, out value);

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
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
            forwarded[0] = CallArgumentValue.Positional(_self);
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
                if (arguments[0].IsKeyword && arguments[0].KeywordName != "fillvalue")
                {
                    throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "functools.recursive_repr", arguments[0].KeywordName, span);
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
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
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

    private static bool DoesDispatchTypeMatch(object requestedType, object registeredType)
        => IsSubclassAgainstSingleType(requestedType, registeredType);

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

}
