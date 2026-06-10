using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface IPyDeletableDescriptor
{
    void Delete(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span);
}

internal sealed class PyProperty : IPyRenderableValue, IPyDescriptor, IPySettableDescriptor, IPyDeletableDescriptor, IClassOwnedMember, IClassNamedMember
{
    private readonly LythonRuntime.ICallable? _getter;
    private readonly LythonRuntime.ICallable? _setter;
    private readonly LythonRuntime.ICallable? _deleter;

    public PyProperty(LythonRuntime.ICallable? getter, LythonRuntime.ICallable? setter, LythonRuntime.ICallable? deleter = null)
    {
        _getter = getter;
        _setter = setter;
        _deleter = deleter;
    }

    public string? Name { get; private set; }

    public void BindName(string name) => Name ??= name;

    public PyProperty WithGetter(LythonRuntime.ICallable getter)
    {
        var property = new PyProperty(getter, _setter, _deleter);
        if (Name is not null)
        {
            property.BindName(Name);
        }

        return property;
    }

    public PyProperty WithSetter(LythonRuntime.ICallable setter)
    {
        var property = new PyProperty(_getter, setter, _deleter);
        if (Name is not null)
        {
            property.BindName(Name);
        }

        return property;
    }

    public PyProperty WithDeleter(LythonRuntime.ICallable deleter)
    {
        var property = new PyProperty(_getter, _setter, deleter);
        if (Name is not null)
        {
            property.BindName(Name);
        }

        return property;
    }

    public bool TryGetDecoratorMember(string name, out object value)
    {
        value = name switch
        {
            "getter" => new PropertyDecoratorCallable(this, getter: true),
            "setter" => new PropertyDecoratorCallable(this, getter: false),
            "deleter" => new PropertyDecoratorCallable(this, getter: null),
            "__get__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Get),
            "__set__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Set),
            "__delete__" => new PropertyDescriptorMethodCallable(this, DescriptorMethodKind.Delete),
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    public void BindOwner(PyType owner)
    {
        if (_getter is IClassOwnedMember getter)
        {
            getter.BindOwner(owner);
        }

        if (_setter is IClassOwnedMember setter)
        {
            setter.BindOwner(owner);
        }

        if (_deleter is IClassOwnedMember deleter)
        {
            deleter.BindOwner(owner);
        }
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        if (instance is null)
        {
            return this;
        }

        if (context is null || span is null)
        {
            throw new InvalidOperationException("Property access requires runtime context.");
        }

        var accessSpan = span;

        if (_getter is null)
        {
            throw new LythonRuntimeException("AttributeError", BuildMissingGetterMessage(owner), accessSpan);
        }

        var callable = BindAccessor(_getter, instance, owner, context, accessSpan, "getter");
        return callable.Invoke(Array.Empty<CallArgumentValue>(), accessSpan, context);
    }

    public void Set(PyInstance instance, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_setter is null)
        {
            throw new LythonRuntimeException("AttributeError", BuildMissingSetterMessage(instance.Type), span);
        }

        var callable = BindAccessor(_setter, instance, instance.Type, context, span, "setter");
        _ = callable.Invoke([new CallArgumentValue(null, value)], span, context);
    }

    public void Delete(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_deleter is null)
        {
            throw new LythonRuntimeException("AttributeError", BuildMissingDeleterMessage(instance.Type), span);
        }

        var callable = BindAccessor(_deleter, instance, instance.Type, context, span, "deleter");
        _ = callable.Invoke([], span, context);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<property object>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private LythonRuntime.ICallable BindAccessor(LythonRuntime.ICallable accessor, object instance, PyType owner, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string kind)
    {
        var resolved = accessor is IPyDescriptor descriptor
            ? descriptor.Get(instance, owner, context, span)
            : accessor;

        if (resolved is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"property {kind} must be callable.", span);
        }

        return callable;
    }

    private string BuildMissingGetterMessage(PyType owner)
        => Name is null
            ? $"property on '{owner.Name}' has no getter."
            : $"property '{Name}' of '{owner.Name}' has no getter.";

    private string BuildMissingSetterMessage(PyType owner)
        => Name is null
            ? $"property on '{owner.Name}' has no setter."
            : $"property '{Name}' of '{owner.Name}' has no setter.";

    private string BuildMissingDeleterMessage(PyType owner)
        => Name is null
            ? $"property on '{owner.Name}' has no deleter."
            : $"property '{Name}' of '{owner.Name}' has no deleter.";

    private sealed class PropertyDecoratorCallable(PyProperty property, bool? getter) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not LythonRuntime.ICallable callable)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    getter switch
                    {
                        true => "property.getter(func) expects one callable argument.",
                        false => "property.setter(func) expects one callable argument.",
                        _ => "property.deleter(func) expects one callable argument."
                    },
                    span);
            }

            return getter switch
            {
                true => property.WithGetter(callable),
                false => property.WithSetter(callable),
                _ => property.WithDeleter(callable)
            };
        }
    }

    private enum DescriptorMethodKind
    {
        Get,
        Set,
        Delete
    }

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

        private object InvokeGet(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0].Name is not null || arguments[1].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "property.__get__(instance, owner) expects two positional arguments.", span);
            }

            var owner = arguments[1].Value as PyType
                ?? throw new LythonRuntimeException("TypeError", "property.__get__(instance, owner) expects owner to be a class.", span);
            var instance = ReferenceEquals(arguments[0].Value, PyNone.Instance) ? null : arguments[0].Value;
            return property.Get(instance, owner, context, span);
        }

        private object InvokeSet(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
                arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", "property.__set__(instance, value) expects an instance and a value.", span);
            }

            property.Set(instance, arguments[1].Value, context, span);
            return PyNone.Instance;
        }

        private object InvokeDelete(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", "property.__delete__(instance) expects one instance argument.", span);
            }

            property.Delete(instance, context, span);
            return PyNone.Instance;
        }
    }
}
