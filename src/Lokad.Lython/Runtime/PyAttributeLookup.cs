using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyAttributeLookup
{
    public static bool TryResolveTypeMember(PyType type, string memberName, [MaybeNullWhen(false)] out object value)
    {
        if (TryResolveBuiltinTypeMember(type, memberName, out value))
        {
            return true;
        }

        if (type.TryLookupInMro(memberName, 0, out var rawValue, out _))
        {
            value = BindForType(type, rawValue, null, null);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public static bool TryResolveTypeMember(PyType type, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (TryResolveBuiltinTypeMember(type, memberName, context, span, out value))
        {
            return true;
        }

        if (type.TryLookupInMro(memberName, 0, out var rawValue, out _))
        {
            value = BindForType(type, rawValue, context, span);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public static bool TryResolveInstanceMember(PyInstance instance, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (instance.Type.TryLookupInMro("__getattribute__", 0, out var getAttributeValue, out _))
        {
            var bound = BindForInstance(instance, getAttributeValue, context, span);
            if (bound is not LythonRuntime.ICallable getAttributeCallable)
            {
                throw new LythonRuntimeException("TypeError", "__getattribute__ must be callable.", span);
            }

            try
            {
                value = getAttributeCallable.Invoke([CallArgumentValue.Positional(PyString.FromString(memberName))], span, context);
                return true;
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError")
            {
                if (instance.Type.TryLookupInMro("__getattr__", 0, out var rawGetAttr, out _))
                {
                    var boundGetAttr = BindForInstance(instance, rawGetAttr, context, span);
                    if (boundGetAttr is not LythonRuntime.ICallable getAttrCallable)
                    {
                        throw new LythonRuntimeException("TypeError", "__getattr__ must be callable.", span);
                    }

                    value = getAttrCallable.Invoke([CallArgumentValue.Positional(PyString.FromString(memberName))], span, context);
                    return true;
                }

                value = PyNone.Instance;
                return false;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public static bool TryResolveInstanceMemberWithoutGetAttrFallback(PyInstance instance, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (memberName == "__class__")
        {
            value = instance.Type;
            return true;
        }

        if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
            IsDataDescriptor(rawValue))
        {
            value = BindForInstance(instance, rawValue, context, span);
            return true;
        }

        if (instance.TryGetOwnAttribute(memberName, out value))
        {
            return true;
        }

        if (instance.Type.TryLookupInMro(memberName, 0, out rawValue, out _))
        {
            value = BindForInstance(instance, rawValue, context, span);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public static bool TryResolveSuperMember(PySuper superObject, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (!superObject.BoundType.TryGetSuccessorMroIndex(superObject.AnchorType, out var startIndex))
        {
            value = PyNone.Instance;
            return false;
        }

        if (superObject.BoundType.TryLookupInMro(memberName, startIndex, out var rawValue, out _))
        {
            value = BindForSuper(superObject, rawValue, context, span);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public static bool TrySetDescriptorValue(object descriptor, PyInstance instance, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (descriptor is IPySettableDescriptor settable)
        {
            settable.Set(instance, value, context, span);
            return true;
        }

        if (descriptor is PyInstance descriptorInstance &&
            TryLookupDescriptorMethod(descriptorInstance, "__set__", context, span, out var callable))
        {
            _ = callable.Invoke(
                [CallArgumentValue.Positional(instance), CallArgumentValue.Positional(value)],
                span,
                context);
            return true;
        }

        return false;
    }

    public static bool TryDeleteDescriptorValue(object descriptor, PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (descriptor is IPyDeletableDescriptor deletable)
        {
            deletable.Delete(instance, context, span);
            return true;
        }

        if (descriptor is PyInstance descriptorInstance &&
            TryLookupDescriptorMethod(descriptorInstance, "__delete__", context, span, out var callable))
        {
            _ = callable.Invoke(
                [CallArgumentValue.Positional(instance)],
                span,
                context);
            return true;
        }

        return false;
    }

    private static object BindForType(PyType type, object rawValue, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        return TryBindDynamicDescriptor(rawValue, null, type, context, span, out var value)
            ? value
            : rawValue;
    }

    private static object BindForInstance(PyInstance instance, object rawValue, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return TryBindDynamicDescriptor(rawValue, instance, instance.Type, context, span, out var value)
            ? value
            : rawValue;
    }

    private static object BindForSuper(PySuper superObject, object rawValue, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return TryBindDynamicDescriptor(rawValue, superObject.BoundObject, superObject.BoundType, context, span, out var value)
            ? value
            : rawValue;
    }

    private static bool IsDataDescriptor(object rawValue)
    {
        return rawValue is IPySettableDescriptor ||
               rawValue is PyInstance descriptorInstance && descriptorInstance.Type.TryLookupInMro("__set__", 0, out _, out _);
    }

    private static bool TryBindDynamicDescriptor(object rawValue, object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span, [MaybeNullWhen(false)] out object value)
    {
        if (rawValue is IPyDescriptor descriptor)
        {
            value = descriptor.Get(instance, owner, context, span);
            return true;
        }

        if (rawValue is PyInstance descriptorInstance)
        {
            if (context is null || span is null)
            {
                value = rawValue;
                return false;
            }

            if (TryLookupDescriptorMethod(descriptorInstance, "__get__", context, span, out var callable))
            {
                value = callable.Invoke(
                    [
                        CallArgumentValue.Positional(instance ?? PyNone.Instance),
                        CallArgumentValue.Positional(owner)
                    ],
                    span,
                    context);
                return true;
            }
        }

        value = rawValue;
        return false;
    }

    private static bool TryLookupDescriptorMethod(PyInstance descriptorInstance, string methodName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out LythonRuntime.ICallable callable)
    {
        if (descriptorInstance.Type.TryLookupInMro(methodName, 0, out var rawMethod, out _))
        {
            var bound = rawMethod is IPyDescriptor descriptor
                ? descriptor.Get(descriptorInstance, descriptorInstance.Type, context, span)
                : rawMethod;
            if (bound is not LythonRuntime.ICallable resolved)
            {
                throw new LythonRuntimeException("TypeError", $"Descriptor method '{methodName}' must be callable.", span);
            }

            callable = resolved;
            return true;
        }

        callable = null;
        return false;
    }

    private static bool TryResolveBuiltinTypeMember(PyType type, string memberName, [MaybeNullWhen(false)] out object value)
    {
        value = memberName switch
        {
            "__name__" => PyString.FromString(type.Name),
            "__qualname__" => PyString.FromString(type.Name),
            "__base__" => (object?)type.Bases.FirstOrDefault() ?? PyNone.Instance,
            "__bases__" => CreateTypeTuple(type.Bases),
            "__mro__" => CreateTypeTuple(type.Mro),
            "__class__" => (object?)type.MetaType ?? PyNone.Instance,
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    private static bool TryResolveBuiltinTypeMember(PyType type, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        value = memberName switch
        {
            "__name__" => PyString.FromString(type.Name),
            "__qualname__" => PyString.FromString(type.Name),
            "__base__" => (object?)type.Bases.FirstOrDefault() ?? PyNone.Instance,
            "__bases__" => CreateTypeTuple(type.Bases, context.MemoryGovernor, span),
            "__mro__" => CreateTypeTuple(type.Mro, context.MemoryGovernor, span),
            "__class__" => (object?)type.MetaType ?? PyNone.Instance,
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    private static PyTuple CreateTypeTuple(IReadOnlyList<PyType> values)
    {
        var items = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            items[i] = values[i];
        }

        return PyTuple.FromOwnedArray(items);
    }

    private static PyTuple CreateTypeTuple(IReadOnlyList<PyType> values, MemoryGovernor governor, LythonSourceSpan span)
    {
        var items = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            items[i] = values[i];
        }

        return PyTuple.FromOwnedArray(items, governor, span);
    }
}
