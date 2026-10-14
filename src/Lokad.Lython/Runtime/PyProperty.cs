using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface IPyDeletableDescriptor
{
    void Delete(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span);
}

internal sealed class PyProperty : IPyRenderableValue, IPyDescriptor, IPySettableDescriptor,
    IPyDeletableDescriptor, IClassOwnedMember, IClassNamedMember, IPyMutableDynamicAttributes,
    IPyDeletableDynamicAttributes, IPyGovernedValue, IPyOwnershipSnapshot
{
    // Accessors, documentation and ownership metadata live in this shell.
    // Numeric fields carry conservative storage allowances, without identity maps.
    internal const long ShellBytes = 128;
    private readonly object _getter, _setter, _deleter;
    private object _doc;
    private readonly bool _getterDoc;
    private readonly MemoryGovernor _governor;
    private readonly LythonSourceSpan _span;
    private readonly long _accessorStorageBytes;
    private long _docStorageBytes;

    private PyProperty(object getter, object setter, object deleter, object doc, bool getterDoc,
        MemoryGovernor governor, LythonSourceSpan span, long accessorStorageBytes, long docStorageBytes)
    {
        _getter = getter;
        _setter = setter;
        _deleter = deleter;
        _doc = doc;
        _getterDoc = getterDoc;
        _governor = governor;
        _span = span;
        _accessorStorageBytes = accessorStorageBytes;
        _docStorageBytes = docStorageBytes;
    }

    public MemoryGovernor OwnerMemoryGovernor => _governor;
    public LythonSourceSpan? AllocationSpan => _span;
    internal long CommittedBytes => ShellBytes + _accessorStorageBytes + _docStorageBytes;
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) { bytes = CommittedBytes; return true; }
    public string? Name { get; private set; }
    public void BindName(string name) => Name ??= name;

    internal static PyProperty Create(object getter, object setter, object deleter, object doc,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var getterDoc = ReferenceEquals(doc, PyNone.Instance) && !ReferenceEquals(getter, PyNone.Instance);
        if (getterDoc) doc = ReadGetterDoc(getter, context, span);
        return CreateResolved(getter, setter, deleter, doc, getterDoc, context, span);
    }

    internal static async ValueTask<PyProperty> CreateAsync(object getter, object setter, object deleter, object doc,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var getterDoc = ReferenceEquals(doc, PyNone.Instance) && !ReferenceEquals(getter, PyNone.Instance);
        if (getterDoc) doc = await ReadGetterDocAsync(getter, context, span).ConfigureAwait(false);
        return CreateResolved(getter, setter, deleter, doc, getterDoc, context, span);
    }

    private static object ReadGetterDoc(object getter, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        try { return LythonRuntime.TryResolveRuntimeMember(getter, "__doc__", context, span, out var doc) ? doc : PyNone.Instance; }
        catch (LythonRuntimeException error) when (error.ExceptionType == "AttributeError") { return PyNone.Instance; }
    }

    private static async ValueTask<object> ReadGetterDocAsync(object getter, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        try
        {
            var result = await LythonRuntime.TryResolveRuntimeMemberAsync(getter, "__doc__", context, span).ConfigureAwait(false);
            return result.Found ? result.Value : PyNone.Instance;
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "AttributeError") { return PyNone.Instance; }
    }

    private static PyProperty CreateResolved(object getter, object setter, object deleter, object doc, bool getterDoc,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var accessorBytes = RuntimeMemoryEstimates.EstimateScalarStorageBytes(getter)
            + RuntimeMemoryEstimates.EstimateScalarStorageBytes(setter)
            + RuntimeMemoryEstimates.EstimateScalarStorageBytes(deleter);
        var docBytes = RuntimeMemoryEstimates.EstimateScalarStorageBytes(doc);
        var bytes = ShellBytes + accessorBytes + docBytes;
        context.MemoryGovernor.Reserve(bytes, span);
        PyProperty property;
        try
        {
            property = new PyProperty(getter, setter, deleter, doc, getterDoc,
                context.MemoryGovernor, span, accessorBytes, docBytes);
        }
        catch
        {
            context.MemoryGovernor.ReleaseReserved(bytes);
            throw;
        }
        context.MemoryGovernor.Commit(bytes);
        context.Services.State.CallTemporaries.TrackFreshMutable(property, property.CommittedBytes, span);
        return property;
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        switch (name)
        {
            case "fget": value = _getter; return true;
            case "fset": value = _setter; return true;
            case "fdel": value = _deleter; return true;
            case "__doc__": value = _doc; return true;
            default: return TryGetDecoratorMember(name, out value);
        }
    }

    public bool TrySetMember(string name, object value)
    {
        if (name is "fget" or "fset" or "fdel") throw new LythonRuntimeException("AttributeError", "readonly attribute", _span);
        if (name != "__doc__") return false;
        var bytes = RuntimeMemoryEstimates.EstimateScalarStorageBytes(value);
        var delta = bytes - _docStorageBytes;
        _governor.Reserve(delta, _span); // Denial precedes changing the visible metadata.
        _doc = value;
        _docStorageBytes = bytes;
        if (delta > 0) _governor.Commit(delta);
        else if (delta < 0) _governor.Release(-delta);
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedBytes);
        return true;
    }

    public bool TryDeleteMember(string name, LythonSourceSpan? span)
    {
        if (name is "fget" or "fset" or "fdel") throw new LythonRuntimeException("AttributeError", "readonly attribute", span);
        return TrySetMember(name, PyNone.Instance);
    }

    public bool TryGetDecoratorMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "getter" => new PropertyDecoratorCallable(this, PropertyAccessorKind.Getter),
            "setter" => new PropertyDecoratorCallable(this, PropertyAccessorKind.Setter),
            "deleter" => new PropertyDecoratorCallable(this, PropertyAccessorKind.Deleter),
            "__get__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Get),
            "__set__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Set),
            "__delete__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Delete),
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    public void BindOwner(PyType owner)
    {
        if (_getter is IClassOwnedMember getter) getter.BindOwner(owner);
        if (_setter is IClassOwnedMember setter) setter.BindOwner(owner);
        if (_deleter is IClassOwnedMember deleter) deleter.BindOwner(owner);
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        if (instance is null) return this;
        if (context is null || span is null) throw new InvalidOperationException("Property access requires runtime context.");
        if (ReferenceEquals(_getter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingGetterMessage(owner), span);
        return CallableInvocation.InvokeUnary(RequireCallable(_getter, span), instance, span, context);
    }

    public async ValueTask<object> GetAsync(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        if (instance is null) return this;
        if (context is null || span is null) throw new InvalidOperationException("Property access requires runtime context.");
        if (ReferenceEquals(_getter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingGetterMessage(owner), span);
        return await CallableInvocation.InvokeUnaryAsync(RequireCallable(_getter, span), instance, span, context).ConfigureAwait(false);
    }

    public void Set(PyInstance instance, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (ReferenceEquals(_setter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingSetterMessage(instance.Type), span);
        _ = CallableInvocation.InvokeBinary(RequireCallable(_setter, span), instance, value, span, context);
    }

    internal async ValueTask SetAsync(PyInstance instance, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (ReferenceEquals(_setter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingSetterMessage(instance.Type), span);
        _ = await CallableInvocation.InvokeBinaryAsync(RequireCallable(_setter, span), instance, value, span, context).ConfigureAwait(false);
    }

    public void Delete(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (ReferenceEquals(_deleter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingDeleterMessage(instance.Type), span);
        _ = CallableInvocation.InvokeUnary(RequireCallable(_deleter, span), instance, span, context);
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<property object>");
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    internal async ValueTask DeleteAsync(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (ReferenceEquals(_deleter, PyNone.Instance)) throw new LythonRuntimeException("AttributeError", BuildMissingDeleterMessage(instance.Type), span);
        _ = await CallableInvocation.InvokeUnaryAsync(RequireCallable(_deleter, span), instance, span, context).ConfigureAwait(false);
    }

    private static LythonRuntime.ICallable RequireCallable(object accessor, LythonSourceSpan span)
        => accessor as LythonRuntime.ICallable ?? throw RuntimeErrors.NotCallable(span);

    private string BuildMissingGetterMessage(PyType owner)
        => Name is null ? $"property on '{owner.Name}' has no getter." : $"property '{Name}' of '{owner.Name}' has no getter.";
    private string BuildMissingSetterMessage(PyType owner)
        => Name is null ? $"property on '{owner.Name}' has no setter." : $"property '{Name}' of '{owner.Name}' has no setter.";
    private string BuildMissingDeleterMessage(PyType owner)
        => Name is null ? $"property on '{owner.Name}' has no deleter." : $"property '{Name}' of '{owner.Name}' has no deleter.";

    private enum PropertyAccessorKind { Getter, Setter, Deleter }

    private sealed class PropertyDecoratorCallable(PyProperty property, PropertyAccessorKind kind) : LythonRuntime.ICallable
    {
        private object Argument(CallArgumentValue[] args, LythonSourceSpan span)
        {
            if (args.Length != 1 || args[0].IsKeyword) throw new LythonRuntimeException("TypeError", $"property.{kind.ToString().ToLowerInvariant()}(func) expects one positional argument.", span);
            return args[0].Value;
        }

        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var value = Argument(args, span);
            var replace = !ReferenceEquals(value, PyNone.Instance);
            var copy = Create(kind == PropertyAccessorKind.Getter && replace ? value : property._getter,
                kind == PropertyAccessorKind.Setter && replace ? value : property._setter,
                kind == PropertyAccessorKind.Deleter && replace ? value : property._deleter,
                property._getterDoc ? PyNone.Instance : property._doc, context, span);
            if (property.Name is { } name) copy.BindName(name);
            return copy;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var value = Argument(args, span);
            var replace = !ReferenceEquals(value, PyNone.Instance);
            var copy = await CreateAsync(kind == PropertyAccessorKind.Getter && replace ? value : property._getter,
                kind == PropertyAccessorKind.Setter && replace ? value : property._setter,
                kind == PropertyAccessorKind.Deleter && replace ? value : property._deleter,
                property._getterDoc ? PyNone.Instance : property._doc, context, span).ConfigureAwait(false);
            if (property.Name is { } name) copy.BindName(name);
            return copy;
        }
    }

    private enum DescriptorMethodKind { Get, Set, Delete }

    private sealed class PropertyDescriptorMethodCallable(PyProperty property, DescriptorMethodKind kind) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            return kind switch
            {
                DescriptorMethodKind.Get => InvokeGet(arguments, span, context),
                DescriptorMethodKind.Set => InvokeSet(arguments, span, context),
                DescriptorMethodKind.Delete => InvokeDelete(arguments, span, context),
                _ => throw new InvalidOperationException("Unknown property descriptor method kind.")
            };
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (kind == DescriptorMethodKind.Get)
            {
                var (instance, owner) = GetArguments(args, span);
                return await property.GetAsync(instance, owner, context, span).ConfigureAwait(false);
            }
            if (kind == DescriptorMethodKind.Set)
            {
                var instance = SetArguments(args, span);
                await property.SetAsync(instance, args[1].Value, context, span).ConfigureAwait(false);
            }
            else await property.DeleteAsync(DeleteArgument(args, span), context, span).ConfigureAwait(false);
            return PyNone.Instance;
        }

        private static (object? Instance, PyType Owner) GetArguments(CallArgumentValue[] args, LythonSourceSpan span)
        {
            if (args.Length != 2 || args[0].IsKeyword || args[1].IsKeyword)
                throw new LythonRuntimeException("TypeError", "property.__get__(instance, owner) expects two positional arguments.", span);
            var owner = args[1].Value as PyType ?? throw new LythonRuntimeException("TypeError", "property.__get__(instance, owner) expects owner to be a class.", span);
            return (ReferenceEquals(args[0].Value, PyNone.Instance) ? null : args[0].Value, owner);
        }

        private static PyInstance SetArguments(CallArgumentValue[] args, LythonSourceSpan span)
        {
            if (args.Length != 2 || args[0].IsKeyword || args[1].IsKeyword || args[0].Value is not PyInstance instance)
                throw new LythonRuntimeException("TypeError", "property.__set__(instance, value) expects an instance and a value.", span);
            return instance;
        }

        private static PyInstance DeleteArgument(CallArgumentValue[] args, LythonSourceSpan span)
        {
            if (args.Length != 1 || args[0].IsKeyword || args[0].Value is not PyInstance instance)
                throw new LythonRuntimeException("TypeError", "property.__delete__(instance) expects one instance argument.", span);
            return instance;
        }

        private object InvokeGet(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var (instance, owner) = GetArguments(args, span);
            return property.Get(instance, owner, context, span);
        }

        private object InvokeSet(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            property.Set(SetArguments(args, span), args[1].Value, context, span);
            return PyNone.Instance;
        }

        private object InvokeDelete(CallArgumentValue[] args, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            property.Delete(DeleteArgument(args, span), context, span);
            return PyNone.Instance;
        }
    }
}
