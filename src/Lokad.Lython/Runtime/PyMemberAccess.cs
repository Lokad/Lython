using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyMemberAccess
{
    private delegate bool ExactMemberResolver(object target, string memberName, [MaybeNullWhen(false)] out object value);

    private static readonly IReadOnlyDictionary<Type, ExactMemberResolver> ExactResolvers =
        new Dictionary<Type, ExactMemberResolver>
        {
            [typeof(PyString)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.StringMembers.TryGetMember((PyString)target, memberName, out value),
            [typeof(PyBytes)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.BytesMembers.TryGetMember((PyBytes)target, memberName, out value),
            [typeof(PyList)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.ListMembers.TryGetMember((PyList)target, memberName, out value),
            [typeof(PyDict)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictMembers.TryGetMember((PyDict)target, memberName, out value),
            [typeof(PyTuple)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TupleMembers.TryGetMember((PyTuple)target, memberName, out value),
            [typeof(PySet)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.SetMembers.TryGetMember((PySet)target, memberName, out value),
            [typeof(PyPath)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.PathMembers.TryGetMember((PyPath)target, memberName, out value),
            [typeof(PyDecimal)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DecimalMembers.TryGetMember((PyDecimal)target, memberName, out value),
            [typeof(PyDate)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DateMembers.TryGetMember((PyDate)target, memberName, out value),
            [typeof(PyTime)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TimeMembers.TryGetMember((PyTime)target, memberName, out value),
            [typeof(PyDateTime)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DateTimeMembers.TryGetMember((PyDateTime)target, memberName, out value),
            [typeof(PyTimedelta)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TimedeltaMembers.TryGetMember((PyTimedelta)target, memberName, out value),
            [typeof(PyTimezone)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TimezoneMembers.TryGetMember((PyTimezone)target, memberName, out value),
            [typeof(PyDefaultDict)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DefaultDictMembers.TryGetMember((PyDefaultDict)target, memberName, out value),
            [typeof(PyCounter)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CounterMembers.TryGetMember((PyCounter)target, memberName, out value),
            [typeof(PyDeque)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DequeMembers.TryGetMember((PyDeque)target, memberName, out value),
            [typeof(PyDataclassFieldObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((PyDataclassFieldObject)target).TryGetMember(memberName, out value),
            [typeof(PyDataclassParamsObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((PyDataclassParamsObject)target).TryGetMember(memberName, out value),
            [typeof(LythonPathStat)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.PathStatMembers.TryGetMember((LythonPathStat)target, memberName, out value),
            [typeof(LythonRuntime.CsvReaderObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CsvReaderMembers.TryGetMember((LythonRuntime.CsvReaderObject)target, memberName, out value),
            [typeof(LythonRuntime.CsvDictReaderObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CsvDictReaderMembers.TryGetMember((LythonRuntime.CsvDictReaderObject)target, memberName, out value),
            [typeof(LythonRuntime.CsvWriterObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CsvWriterMembers.TryGetMember((LythonRuntime.CsvWriterObject)target, memberName, out value),
            [typeof(LythonRuntime.CsvDictWriterObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CsvDictWriterMembers.TryGetMember((LythonRuntime.CsvDictWriterObject)target, memberName, out value),
            [typeof(LythonRuntime.ReMatchObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.ReMatchMembers.TryGetMember((LythonRuntime.ReMatchObject)target, memberName, out value),
            [typeof(LythonRuntime.RePatternObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.RePatternMembers.TryGetMember((LythonRuntime.RePatternObject)target, memberName, out value),
            [typeof(PyException)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.ExceptionInstanceMembers.TryGetMember((PyException)target, memberName, out value),
            [typeof(LythonRuntime.ExecutionContext.TextFileHandle)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TextFileHandleMembers.TryGetMember((LythonRuntime.ExecutionContext.TextFileHandle)target, memberName, out value),
            [typeof(HostTextInputHandle)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.HostTextInputMembers.TryGetMember((HostTextInputHandle)target, memberName, out value),
            [typeof(HostTextOutputHandle)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.HostTextOutputMembers.TryGetMember((HostTextOutputHandle)target, memberName, out value),
            [typeof(PyCompletedProcess)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.CompletedProcessMembers.TryGetMember((PyCompletedProcess)target, memberName, out value),
            [typeof(LythonRuntime.ArgumentParserObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.ArgumentParserObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.ArgparseNamespaceObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.ArgparseNamespaceObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.ArgparseMutuallyExclusiveGroupObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.ArgparseMutuallyExclusiveGroupObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.ChainFactory)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.ChainFactory)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.DifflibDifferObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.DifflibDifferObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.DifflibHtmlDiffObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.DifflibHtmlDiffObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.DifflibMatchObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.DifflibMatchObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.DifflibSequenceMatcherObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.DifflibSequenceMatcherObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.PkgutilModuleInfoObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.PkgutilModuleInfoObject)target).TryGetMember(memberName, out value),
            [typeof(LythonRuntime.PkgutilLoaderObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => ((LythonRuntime.PkgutilLoaderObject)target).TryGetMember(memberName, out value),
        };

    public static bool TryResolve(object target, string memberName, [MaybeNullWhen(false)] out object value)
    {
        return TryResolveNonContextual(target, memberName, out value);
    }

    public static bool TryResolve(object target, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (target is not IPyContextualDynamicAttributes &&
            target is not PyPath &&
            target is not LythonPathStat &&
            target is not LythonRuntime.OpenPyxlWorksheet &&
            target is not PySuper &&
            target is not PyInstance &&
            target is not PyType &&
            TryResolveNonContextual(target, memberName, out value))
        {
            return true;
        }

        if (target is IPyContextualDynamicAttributes contextualDynamicAttributes &&
            contextualDynamicAttributes.TryGetMember(memberName, context, span, out value))
        {
            return true;
        }

        if (target is PyPath path && LythonRuntime.PathMembers.TryGetMember(path, memberName, context, span, out value))
        {
            return true;
        }

        if (target is LythonPathStat stat && LythonRuntime.PathStatMembers.TryGetMember(stat, memberName, context, span, out value))
        {
            return true;
        }

        if (target is LythonRuntime.OpenPyxlWorksheet worksheet && worksheet.TryGetMember(memberName, context, span, out value))
        {
            return true;
        }

        if (target is PySuper superObject && PyAttributeLookup.TryResolveSuperMember(superObject, memberName, context, span, out value))
        {
            return true;
        }

        if (target is PyInstance instance && instance.TryGetAttribute(memberName, context, span, out value))
        {
            return true;
        }

        if (target is PyType type && PyAttributeLookup.TryResolveTypeMember(type, memberName, context, span, out value))
        {
            return true;
        }

        if (memberName == "__class__" &&
            ((target is PyNamedTupleType && context.TryGetBuiltin("type", out value) && value is not null) ||
             LythonRuntime.TryGetValueClass(target, context, out value)))
        {
            return true;
        }

        if (target is PyException exception &&
            LythonRuntime.ExceptionInstanceMembers.TryGetMember(exception, memberName, context, span, out value))
        {
            return true;
        }

        if (PyAttributeLookup.TryResolveObjectSlot(target, memberName, context, span, out value))
        {
            return true;
        }

        return TryResolveNonContextual(target, memberName, out value);
    }

    private static bool TryResolveNonContextual(object target, string memberName, [MaybeNullWhen(false)] out object value)
    {
        var targetType = target.GetType();
        if (ExactResolvers.TryGetValue(targetType, out var exactResolver) && exactResolver(target, memberName, out value))
        {
            return true;
        }

        if (target is PyModule module && module.TryGetCachedMember(memberName, out value))
        {
            return true;
        }

        if (target is PyType type && type.TryGetMember(memberName, out value))
        {
            return true;
        }

        if (target is PyBuiltinRuntimeType builtinType && builtinType.TryGetMember(memberName, out value))
        {
            return true;
        }

        if (target is IPyDynamicAttributes dynamicAttributes && dynamicAttributes.TryGetMember(memberName, out value))
        {
            return true;
        }

        if (target is PyProperty property && property.TryGetDecoratorMember(memberName, out value))
        {
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    // Resolves members owned by the per-type instance tables only, so the
    // unbound-descriptor choke never picks up dynamic attributes or dunders
    // served through other shapes.
    internal static bool TryResolveInstanceTableMember(object probe, string memberName, [MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        if (!ExactResolvers.TryGetValue(probe.GetType(), out var resolver))
        {
            return false;
        }

        return resolver(probe, memberName, out value);
    }

    public static bool TryAssign(object target, string memberName, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (target is PyModule module && module.TrySetMember(memberName, value))
        {
            return true;
        }

        if (target is PyType type && type.TrySetMember(memberName, value))
        {
            return true;
        }

        if (target is IPyMutableDynamicAttributes dynamicAttributes && dynamicAttributes.TrySetMember(memberName, value))
        {
            return true;
        }

        if (target is PyInstance instance)
        {
            if (instance.Type.TryLookupInMro("__setattr__", 0, out var setattrValue, out _))
            {
                var boundSetAttr = setattrValue switch
                {
                    IPyBindableCallable bindable => bindable.Bind(instance),
                    IPyDescriptor descriptor => descriptor.Get(instance, instance.Type, context, span),
                    _ => setattrValue
                };
                if (boundSetAttr is not LythonRuntime.ICallable setattrCallable)
                {
                    throw new LythonRuntimeException("TypeError", "__setattr__ must be callable.", span);
                }

                _ = CallableInvocation.InvokeBinary(
                    setattrCallable,
                    PyString.FromString(memberName),
                    value,
                    span,
                    context);
                return true;
            }
        }

        return false;
    }

    public static bool TryDelete(object target, string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (target is PyInstance instance)
        {
            if (instance.Type.TryLookupInMro("__delattr__", 0, out var delattrValue, out _))
            {
                var boundDelAttr = delattrValue switch
                {
                    IPyBindableCallable bindable => bindable.Bind(instance),
                    IPyDescriptor descriptor => descriptor.Get(instance, instance.Type, context, span),
                    _ => delattrValue
                };
                if (boundDelAttr is not LythonRuntime.ICallable delattrCallable)
                {
                    throw new LythonRuntimeException("TypeError", "__delattr__ must be callable.", span);
                }

                _ = CallableInvocation.InvokeUnary(delattrCallable, PyString.FromString(memberName), span, context);
                return true;
            }
        }

        return false;
    }

    public static LythonRuntimeException CreateMissingMemberError(object target, string memberName, LythonSourceSpan span)
    {
        return target switch
        {
            PyPath when memberName is "write_bytes" or "read_bytes"
                => new LythonRuntimeException(
                    "AttributeError",
                    $"Path.{memberName}(...) is not supported by Lython. The host boundary is UTF-8 text-shaped only.",
                    span),
            PyPath when memberName == "glob"
                => new LythonRuntimeException(
                    "AttributeError",
                    "Path.glob(...) is not supported by Lython. Use Path.rglob(...) for the supported recursive form.",
                    span),
            _ => new LythonRuntimeException(
                "AttributeError",
                $"Object has no attribute '{memberName}'.",
                span)
        };
    }
}
