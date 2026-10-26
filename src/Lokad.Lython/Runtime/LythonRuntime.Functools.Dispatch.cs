using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class SingleDispatchFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly SingleDispatchFactory Instance = new();
        public string Name => "functools.singledispatch";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || !IsCallable(arguments[0].Value))
                throw RuntimeErrors.Type("functools.singledispatch(func) expects one callable argument.", span);
            var dispatcher = new PySingleDispatchDispatcher(arguments[0].Value, context, span);
            await ApplyUpdateWrapperAsync(dispatcher, arguments[0].Value, FunctoolsWrapperAssignments, FunctoolsWrapperUpdates, context, span, asynchronous).ConfigureAwait(false);
            return dispatcher;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function singledispatch>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static bool HasDescriptorGetter(object target)
        => target is IPyDescriptor || target is PyInstance instance && instance.Type.TryLookupInMro("__get__", 0, out _, out _);

    private sealed class SingleDispatchMethodFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly SingleDispatchMethodFactory Instance = new();
        public string Name => "functools.singledispatchmethod";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || !(IsCallable(arguments[0].Value) || HasDescriptorGetter(arguments[0].Value)))
                throw RuntimeErrors.Type("functools.singledispatchmethod(func) expects a callable or descriptor.", span);
            var dispatcher = new PySingleDispatchDispatcher(arguments[0].Value, context, span);
            await ApplyUpdateWrapperAsync(dispatcher, arguments[0].Value, FunctoolsWrapperAssignments, FunctoolsWrapperUpdates, context, span, asynchronous).ConfigureAwait(false);
            return new PySingleDispatchMethod(dispatcher);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class 'functools.singledispatchmethod'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PySingleDispatchDispatcher : ICallable, IPyBindableCallable, IPyMutableDynamicAttributes,
        IPyContextualDynamicAttributes, IPyRenderableValue, IClassOwnedMember
    {
        private readonly PyDict _registry;
        private readonly object _objectType;
        private PyMappingProxy? _view;
        private PyDict? _metadata;
        private readonly MemoryGovernor _memoryGovernor;
        private readonly LythonSourceSpan _allocationSpan;

        public PySingleDispatchDispatcher(object defaultCallable, ExecutionContext context, LythonSourceSpan span)
        {
            _memoryGovernor = context.MemoryGovernor;
            _allocationSpan = span;
            _objectType = TryGetBuiltinOrNull(context, "object") ?? throw new InvalidOperationException("Missing object type.");
            _registry = new PyDict(_memoryGovernor, span);
            _registry.SetItem(_objectType, defaultCallable);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            RequireDispatchArgument(arguments, span);
            var target = await ResolveForValueAsync(arguments[0].Value, context, span, asynchronous).ConfigureAwait(false);
            var callable = target as ICallable ?? throw NotCallableError(target, context, span);
            return asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false) : callable.Invoke(arguments, span, context);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);
        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);
        public void BindOwner(PyType owner)
        {
            foreach (var entry in _registry)
                if (entry.Value is IClassOwnedMember owned) owned.BindOwner(owner);
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, null, _allocationSpan, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, context, span, out value);
        private bool TryGetMemberCore(string name, ExecutionContext? context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__") { value = _metadata ??= new PyDict(_memoryGovernor, span); return true; }
            if (TryReadFunctoolsMetadata(_metadata, name, _memoryGovernor, span, context, out value)) return true;
            value = name switch
            {
                "register" => new SingleDispatchRegisterMethod(this),
                "dispatch" => new SingleDispatchDispatchMethod(this),
                "registry" => _view ??= new PyMappingProxy(_registry, _memoryGovernor, span),
                "__wrapped__" => _registry.TryGetValue(_objectType, out var original) ? original : PyNone.Instance,
                "__name__" or "__qualname__" => PyString.FromString("singledispatch"),
                _ => MissingMemberValue.Instance
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public bool TrySetMember(string name, object value)
            => SetFunctoolsMetadata(ref _metadata, name, value, _memoryGovernor, _allocationSpan);

        internal async ValueTask<object> ResolveForValueAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            context.CheckExecution(span);
            var resolved = asynchronous ? await TryResolveRuntimeMemberAsync(value, "__class__", context, span).ConfigureAwait(false)
                : TryResolveRuntimeMember(value, "__class__", context, span, out var cls) ? (true, cls) : (false, (object)PyNone.Instance);
            if (!resolved.Item1) throw PyMemberAccess.CreateMissingMemberError(value, "__class__", span, context);
            return ResolveForType(resolved.Item2, span, context);
        }

        public object ResolveForType(object type, LythonSourceSpan span, ExecutionContext context)
        {
            if (!IsSupportedTypeSpecifier(type))
                throw RuntimeErrors.Type("singledispatch.dispatch(cls) expects a supported class/type argument.", span);
            foreach (var ancestor in DispatchMro(type, context, span))
            {
                context.CheckExecution(span);
                if (_registry.TryGetValue(ancestor, out var callable, context, span)) return callable;
            }
            return _registry.TryGetValue(_objectType, out var fallback) ? fallback : throw new InvalidOperationException("Missing default dispatch registration.");
        }

        public void Register(object type, object callable, LythonSourceSpan span)
        {
            if (!IsSupportedTypeSpecifier(type))
                throw RuntimeErrors.Type("singledispatch.register(cls, func) expects cls to be a supported class/type.", span);
            _registry.SetItem(type, callable);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var name = TryReadWrapperMetadata(this, "__name__", context.Context, null, out var value) && PyStringOps.TryAsString(value, out var text)
                ? text.AsString() : "singledispatch";
            return PyString.FromString($"<function {name}>");
        }
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private sealed class SingleDispatchRegisterMethod(PySingleDispatchDispatcher owner) : ICallable, IPyRenderableValue
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (arguments.Length == 1 && arguments[0].IsPositional)
                {
                    if (!IsSupportedTypeSpecifier(arguments[0].Value))
                        throw RuntimeErrors.Type(IsCallable(arguments[0].Value)
                            ? "singledispatch.register(func) without an explicit type is unsupported; pass register(cls)."
                            : "singledispatch.register(cls) expects a supported class/type.", span);
                    return new SingleDispatchRegistrationDecorator(owner, arguments[0].Value);
                }
                if (arguments.Length == 2 && arguments[0].IsPositional && arguments[1].IsPositional && (IsCallable(arguments[1].Value) || HasDescriptorGetter(arguments[1].Value)))
                {
                    owner.Register(arguments[0].Value, arguments[1].Value, span);
                    return arguments[1].Value;
                }
                throw RuntimeErrors.Type("singledispatch.register(cls[, func]) expects a class/type and an optional callable.", span);
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<singledispatch.register>");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
        private sealed class SingleDispatchRegistrationDecorator(PySingleDispatchDispatcher owner, object type) : ICallable, IPyRenderableValue
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (arguments.Length != 1 || arguments[0].IsKeyword || !(IsCallable(arguments[0].Value) || HasDescriptorGetter(arguments[0].Value)))
                    throw RuntimeErrors.Type("singledispatch.register(cls)(func) expects one callable argument.", span);
                owner.Register(type, arguments[0].Value, span);
                return arguments[0].Value;
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<singledispatch.register decorator>");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
        private sealed class SingleDispatchDispatchMethod(PySingleDispatchDispatcher owner) : ICallable, IPyRenderableValue
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (arguments.Length != 1 || arguments[0].IsKeyword)
                    throw RuntimeErrors.Type("singledispatch.dispatch(cls) expects one class/type argument.", span);
                return owner.ResolveForType(arguments[0].Value, span, context);
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<singledispatch.dispatch>");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
    }

    private static void RequireDispatchArgument(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length == 0 || arguments[0].IsKeyword)
            throw RuntimeErrors.Type("singledispatch function requires at least one positional dispatch argument.", span);
    }

    private static IEnumerable<object> DispatchMro(object type, ExecutionContext context, LythonSourceSpan span)
    {
        if (type is PyType runtimeType)
        {
            foreach (var ancestor in runtimeType.Mro) yield return ancestor;
            yield break;
        }
        if (type is ExceptionTypeValue exception)
        {
            foreach (var identity in ExceptionDispatchMro(exception.ExceptionIdentity)) yield return new ExceptionTypeValue(identity);
        }
        else if (TryResolveRuntimeMember(type, "__mro__", context, span, out var mro) && mro is PyTuple tuple)
        {
            foreach (var ancestor in tuple) yield return ancestor;
            yield break;
        }
        else
        {
            yield return type;
            if (type is PyNamedTupleType or TimeStructTimeType or UrllibParseModule.UrlResultType)
                yield return TryGetBuiltinOrNull(context, "tuple") ?? throw new InvalidOperationException("Missing tuple type.");
        }
        yield return TryGetBuiltinOrNull(context, "object") ?? throw new InvalidOperationException("Missing object type.");
    }

    // This immutable native catalog has finite, ordered exception bases. Shared
    // ancestors follow later direct bases, just as in the native Python MRO.
    private static IEnumerable<PythonExceptionIdentity> ExceptionDispatchMro(PythonExceptionIdentity identity)
    {
        yield return identity;
        if (!ExceptionBaseIdentities.TryGetValue(identity, out var bases)) yield break;
        for (var i = 0; i < bases.Length; i++)
            foreach (var ancestor in ExceptionDispatchMro(bases[i]))
            {
                var shared = false;
                for (var j = i + 1; j < bases.Length; j++)
                    if (MatchesExceptionType(ancestor, bases[j])) { shared = true; break; }
                if (!shared) yield return ancestor;
            }
    }

    private sealed class PySingleDispatchMethod(PySingleDispatchDispatcher dispatcher) : IPyDescriptor, IPyMutableDynamicAttributes,
        IPyContextualDynamicAttributes, IPyRenderableValue, IClassOwnedMember
    {
        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => new BoundSingleDispatchMethod(instance, owner, dispatcher);
        public void BindOwner(PyType owner) => dispatcher.BindOwner(owner);
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => dispatcher.TryGetMember(name, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => dispatcher.TryGetMember(name, context, span, out value);
        public bool TrySetMember(string name, object value) => dispatcher.TrySetMember(name, value);
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.singledispatchmethod>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class BoundSingleDispatchMethod(object? instance, PyType owner, PySingleDispatchDispatcher dispatcher) : ICallable,
        IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            RequireDispatchArgument(arguments, span);
            var target = await dispatcher.ResolveForValueAsync(arguments[0].Value, context, span, asynchronous).ConfigureAwait(false);
            var bound = asynchronous ? await PyAttributeLookup.BindDescriptorAsync(target, instance, owner, context, span).ConfigureAwait(false)
                : PyAttributeLookup.TryBindDynamicDescriptor(target, instance, owner, context, span, out var member) ? member : target;
            var callable = bound as ICallable ?? throw NotCallableError(bound, context, span);
            return asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false) : callable.Invoke(arguments, span, context);
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => dispatcher.TryGetMember(name, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => dispatcher.TryGetMember(name, context, span, out value);
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<bound singledispatchmethod>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class RecursiveReprFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly RecursiveReprFactory Instance = new();
        public string Name => "functools.recursive_repr";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length > 1) throw RuntimeErrors.Type("functools.recursive_repr(fillvalue='...') expects zero or one argument.", span);
            if (arguments.Length == 1 && arguments[0].IsKeyword && arguments[0].KeywordName != "fillvalue")
                throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, Name, arguments[0].KeywordName, span);
            return new RecursiveReprDecorator(arguments.Length == 0 ? PyString.FromString("...") : arguments[0].Value);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function recursive_repr>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
    private sealed class RecursiveReprDecorator(object fillValue) : ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || !IsCallable(arguments[0].Value))
                throw RuntimeErrors.Type("functools.recursive_repr(...)(func) expects one callable argument.", span);
            var wrapper = new PyRecursiveReprWrapper((ICallable)arguments[0].Value, fillValue, context.MemoryGovernor, span);
            await ApplyUpdateWrapperAsync(wrapper, arguments[0].Value, FunctoolsWrapperAssignments, PyTuple.Empty, context, span, asynchronous).ConfigureAwait(false);
            return wrapper;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.recursive_repr decorator>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
    private sealed class PyRecursiveReprWrapper(ICallable callable, object fillValue, MemoryGovernor governor, LythonSourceSpan span)
        : ICallable, IPyBindableCallable, IPyMutableDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, IClassOwnedMember
    {
        private readonly HashSet<object> _active = new(ReferenceEqualityComparer.Instance);
        private int _activeCapacity;
        private PyDict? _metadata;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan sourceSpan, ExecutionContext context)
            => InvokeCoreAsync(arguments, sourceSpan, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan sourceSpan, ExecutionContext context)
            => InvokeCoreAsync(arguments, sourceSpan, context, true);
        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan sourceSpan, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(sourceSpan);
            var key = arguments.Length == 0 ? this : arguments[0].Value;
            if (_active.Contains(key)) return fillValue;
            if (_active.Count == _activeCapacity)
            {
                using var growth = governor.ReserveTemporary(64L * Math.Max(3L, 2L * (_active.Count + 1L)), sourceSpan);
                var capacity = _active.EnsureCapacity(checked(_active.Count + 1));
                growth.Commit(64L * (capacity - _activeCapacity));
                _activeCapacity = capacity;
            }
            _active.Add(key);
            try { return asynchronous ? await callable.InvokeAsync(arguments, sourceSpan, context).ConfigureAwait(false) : callable.Invoke(arguments, sourceSpan, context); }
            finally { _active.Remove(key); }
        }
        public object Bind(object self) => new PyBoundMethod(self, this);
        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? sourceSpan)
            => instance is null ? this : Bind(instance);
        public void BindOwner(PyType owner) { if (callable is IClassOwnedMember owned) owned.BindOwner(owner); }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => TryGetMemberCore(name, null, span, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan sourceSpan, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, context, sourceSpan, out value);
        private bool TryGetMemberCore(string name, ExecutionContext? context, LythonSourceSpan sourceSpan, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__") { value = _metadata ??= new PyDict(governor, sourceSpan); return true; }
            return TryReadFunctoolsMetadata(_metadata, name, governor, sourceSpan, context, out value);
        }
        public bool TrySetMember(string name, object value) => SetFunctoolsMetadata(ref _metadata, name, value, governor, span);
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<recursive_repr wrapper>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
