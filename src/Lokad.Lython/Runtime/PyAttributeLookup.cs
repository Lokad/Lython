using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyAttributeLookup
{
    public static bool TryResolveTypeMember(PyType type, string memberName, [MaybeNullWhen(false)] out object value)
    {
        // Own-dict docstrings (stored at creation or assigned later) win over the
        // absent None; subclasses do not inherit like CPython.
        if (memberName == "__doc__" && type.TryGetOwnMember("__doc__", out var doc))
        {
            value = doc;
            return true;
        }

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
        if (memberName == "__doc__" && type.TryGetOwnMember("__doc__", out var doc))
        {
            value = doc;
            return true;
        }

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
                value = CallableInvocation.InvokeUnary(getAttributeCallable, PyString.FromString(memberName), span, context);
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

                    value = CallableInvocation.InvokeUnary(getAttrCallable, PyString.FromString(memberName), span, context);
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
            (rawValue is IPySettableDescriptor ||
             rawValue is PyInstance descriptorInstance && descriptorInstance.Type.TryLookupInMro("__set__", 0, out _, out _)))
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

    // Builtin exception __new__ slots live on the defining type like CPython:
    // most builtins own theirs; the mapped ones inherit the ancestor slot.
    private static readonly IReadOnlyDictionary<string, string> InheritedExceptionNewSlots =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["KeyError"] = "LookupError",
            ["ImportError"] = "Exception",
            ["ModuleNotFoundError"] = "Exception",
            ["NameError"] = "Exception",
            ["AttributeError"] = "Exception",
            ["SyntaxError"] = "Exception",
            ["StopIteration"] = "Exception",
            ["FileNotFoundError"] = "OSError",
            ["FileExistsError"] = "OSError",
            ["IsADirectoryError"] = "OSError",
            ["NotADirectoryError"] = "OSError",
            ["PermissionError"] = "OSError",
            ["TimeoutError"] = "OSError",
            ["SystemExit"] = "BaseException",
        };

    private static bool TryResolveExceptionNewSlot(object target, LythonRuntime.ExecutionContext context, [MaybeNullWhen(false)] out object value)
    {
        string? typeName = target switch
        {
            LythonRuntime.ExceptionTypeValue typeValue => typeValue.ExceptionIdentity.ModuleName is "builtins" ? typeValue.TypeName : null,
            PyException exception when exception.Identity.IsBuiltin => exception.Identity.TypeName,
            _ => null,
        };

        if (typeName is null)
        {
            value = PyNone.Instance;
            return false;
        }

        var definingName = typeName;
        while (InheritedExceptionNewSlots.TryGetValue(definingName, out var baseName))
        {
            definingName = baseName;
        }

        if (!context.TryGetBuiltin(definingName, out var definingBase) ||
            definingBase is not LythonRuntime.ExceptionTypeValue definingType)
        {
            value = PyNone.Instance;
            return false;
        }

        return LythonRuntime.TryGetExceptionNewSlot(definingType, definingName, out value);
    }

    // Object slots resolve through the run object type for any receiver missed
    // by the flat member tables, like CPython where every object carries them.
    // Instance slots bind the receiver; __init_subclass__ binds type(target);
    // __new__ resolves through the target type own slot.
    internal static bool TryResolveObjectSlot(object target, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (memberName is "__new__")
        {
            // Exception types own or inherit their slot along the builtin
            // hierarchy like CPython; module exceptions stay missing.
            if (TryResolveExceptionNewSlot(target, context, out value))
            {
                return true;
            }

            // Instances never carry __new__ (it lives on the type): builtin
            // values resolve their type own slot, engine callables share
            // object.__new__, and anything else stays missing.
            if (!LythonRuntime.TryGetValueClass(target, context, out var classValue) ||
                classValue is null)
            {
                value = PyNone.Instance;
                return false;
            }

            if (ReferenceEquals(classValue, PyType.BuiltinFunctionType) ||
                ReferenceEquals(classValue, PyType.MethodWrapperType) ||
                ReferenceEquals(classValue, PyType.WrapperDescriptorType) ||
                ReferenceEquals(classValue, PyType.RegexPatternType) ||
                ReferenceEquals(classValue, PyType.RegexMatchType))
            {
                if (!context.TryGetBuiltin("object", out var sharedBase) ||
                    sharedBase is not PyType sharedType ||
                    !sharedType.TryLookupInMro(memberName, 0, out var sharedRaw, out _) ||
                    sharedRaw is not IPyDescriptor sharedDescriptor)
                {
                    value = PyNone.Instance;
                    return false;
                }

                value = sharedDescriptor.Get(null, sharedType, context, span);
                return true;
            }

            return LythonRuntime.TryGetTypeNewSlot(classValue, out value);
        }

        if (memberName is "__init_subclass__")
        {
            if (!context.TryGetBuiltin("object", out var subclassBase) ||
                subclassBase is not PyType subclassObject ||
                !subclassObject.TryLookupInMro(memberName, 0, out var subclassRaw, out _) ||
                subclassRaw is not IPyBindableCallable subclassBindable ||
                !LythonRuntime.TryGetValueClass(target, context, out var typeValue) ||
                typeValue is null)
            {
                value = PyNone.Instance;
                return false;
            }

            value = subclassBindable.Bind(typeValue);
            return true;
        }

        if (memberName is not ("__init__" or "__getattribute__" or "__setattr__" or "__delattr__"))
        {
            value = PyNone.Instance;
            return false;
        }

        if (!context.TryGetBuiltin("object", out var objectBase) ||
            objectBase is not PyType objectType ||
            !objectType.TryLookupInMro(memberName, 0, out var rawValue, out _))
        {
            value = PyNone.Instance;
            return false;
        }

        if (rawValue is IPyDescriptor descriptor)
        {
            value = descriptor.Get(target, objectType, context, span);
            return true;
        }

        value = rawValue;
        return true;
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
            _ = CallableInvocation.InvokeBinary(callable, instance, value, span, context);
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
            _ = CallableInvocation.InvokeUnary(callable, instance, span, context);
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
                value = CallableInvocation.InvokeBinary(
                    callable,
                    instance ?? PyNone.Instance,
                    owner,
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
        // Absent docstrings report None like CPython (doc texts are not stored).
        if (memberName == "__doc__")
        {
            value = PyNone.Instance;
            return true;
        }

        value = memberName switch
        {
            "__name__" => type.NameValue,
            "__qualname__" => type.NameValue,
            "__base__" => (object?)type.Bases.FirstOrDefault() ?? PyNone.Instance,
            "__bases__" => type.BasesTuple,
            "__mro__" => type.MroTuple,
            "__class__" => (object?)type.MetaType ?? PyNone.Instance,
            // Absent docstrings report None like CPython (doc texts are not stored).
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    private static bool TryResolveBuiltinTypeMember(PyType type, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        // Absent docstrings report None like CPython (doc texts are not stored).
        if (memberName == "__doc__")
        {
            value = PyNone.Instance;
            return true;
        }

        value = memberName switch
        {
            "__name__" => type.NameValue,
            "__qualname__" => type.NameValue,
            "__base__" => (object?)type.Bases.FirstOrDefault() ?? PyNone.Instance,
            "__bases__" => type.BasesTuple,
            "__mro__" => type.MroTuple,
            "__class__" => (object?)type.MetaType ?? PyNone.Instance,
            // Absent docstrings report None like CPython (doc texts are not stored).
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

}
