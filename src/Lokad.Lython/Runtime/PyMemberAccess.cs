using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal enum MissingMemberOperation
{
    Read,
    Delete,
    Write,
}

internal static class PyMemberAccess
{
    private delegate bool ExactMemberResolver(object target, string memberName, [MaybeNullWhen(false)] out object value);

    private static readonly IReadOnlyDictionary<Type, ExactMemberResolver> ExactResolvers =
        new Dictionary<Type, ExactMemberResolver>
        {
            [typeof(PyString)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.StringMembers.TryGetMember((PyString)target, memberName, out value),
            [typeof(BigInteger)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.IntMembers.TryGetMember(target, memberName, out value),
            [typeof(int)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.IntMembers.TryGetMember(target, memberName, out value),
            [typeof(bool)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.IntMembers.TryGetMember(target, memberName, out value),
            [typeof(double)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.FloatMembers.TryGetMember((double)target, memberName, out value),
            [typeof(PyBytes)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.BytesMembers.TryGetMember((PyBytes)target, memberName, out value),
            [typeof(PyList)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.ListMembers.TryGetMember((PyList)target, memberName, out value),
            [typeof(PyDict)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictMembers.TryGetMember((PyDict)target, memberName, out value),
            [typeof(PyTuple)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TupleMembers.TryGetMember((PyTuple)target, memberName, out value),
            [typeof(PyRange)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.RangeMembers.TryGetMember((PyRange)target, memberName, out value),
            [typeof(PyNamedTupleObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TupleMembers.TryGetMember((PyNamedTupleObject)target, memberName, out value),
            [typeof(PyTypingNamedTupleObject)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.TupleMembers.TryGetMember((PyTypingNamedTupleObject)target, memberName, out value),
            [typeof(PySet)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.SetMembers.TryGetMember((PySet)target, memberName, out value),
            [typeof(LythonRuntime.DictKeysView)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictViewMembers.TryGetKeysMember((LythonRuntime.DictKeysView)target, memberName, out value),
            [typeof(LythonRuntime.DictItemsView)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictViewMembers.TryGetItemsMember((LythonRuntime.DictItemsView)target, memberName, out value),
            [typeof(ChainMapKeysView)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictViewMembers.TryGetChainMapKeysMember((ChainMapKeysView)target, memberName, out value),
            [typeof(ChainMapItemsView)] = static (object target, string memberName, [MaybeNullWhen(false)] out object value) => LythonRuntime.DictViewMembers.TryGetChainMapItemsMember((ChainMapItemsView)target, memberName, out value),
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

        if (target is PyType type)
        {
            if (type.Name is "object" or "type")
            {
                throw ImmutableTypeError(target, memberName, span);
            }

            if (type.TrySetMember(memberName, value))
            {
                return true;
            }
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

        if (target is PyException exception)
        {
            // Assigned args replace the construction slot without entering
            // the dict, so __dict__ stays clean like CPython.
            if (memberName == "args")
            {
                var items = LythonRuntime.ToSequence(value, span, context);
                var tuple = new PyTuple(items, context.MemoryGovernor, span);
                context.ObserveCollectionCount(tuple.Count, span);
                exception.ArgsOverride = tuple;
                return true;
            }

            // Chaining slots validate like CPython instead of entering the
            // dict, so __dict__ stays clean for them too.
            if (memberName == "__cause__")
            {
                if (value is PyException chainedCause)
                {
                    exception.Cause = chainedCause;
                    return true;
                }

                if (value is PyNone)
                {
                    exception.Cause = null;
                    return true;
                }

                throw new LythonRuntimeException("TypeError", "exception cause must be None or derive from BaseException", span);
            }

            if (memberName == "__context__")
            {
                if (value is PyException chainedContext)
                {
                    exception.Context = chainedContext;
                    return true;
                }

                if (value is PyNone)
                {
                    exception.Context = null;
                    return true;
                }

                throw new LythonRuntimeException("TypeError", "exception context must be None or derive from BaseException", span);
            }

            if (memberName == "__suppress_context__")
            {
                if (value is bool suppress)
                {
                    exception.SuppressContext = suppress;
                    return true;
                }

                throw new LythonRuntimeException("TypeError", "attribute value type must be bool", span);
            }

            // __dict__ assignment swaps the whole dict like CPython instead
            // of storing a "__dict__" entry; entries are snapshotted first so
            // self-assignment keeps every entry.
            if (memberName == "__dict__")
            {
                if (value is not PyDict replacement)
                {
                    throw new LythonRuntimeException("TypeError", $"__dict__ must be set to a dictionary, not a '{LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context)}'", span);
                }

                exception.CustomDict ??= new PyDict(context.MemoryGovernor, span);
                exception.CustomDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                var entries = new List<KeyValuePair<object, object>>(replacement);
                exception.CustomDict.Clear();
                foreach (var entry in entries)
                {
                    exception.CustomDict.SetItem(entry.Key, entry.Value);
                }

                return true;
            }

            exception.CustomDict ??= new PyDict(context.MemoryGovernor, span);
            exception.CustomDict.AttachMemoryGovernor(context.MemoryGovernor, span);
            exception.CustomDict.SetItem(PyString.FromString(memberName, context.MemoryGovernor, span), value);
            return true;
        }

        if (IsImmutableBuiltinType(target, context))
        {
            throw ImmutableTypeError(target, memberName, span);
        }

        ThrowIfReadOnlyBuiltinMember(target, memberName, span, context);

        if (HasInstanceDict(target))
        {
            // Dict-bearing targets (modules, exceptions, instances) keep
            // their own paths above; anything reaching here stays
            // caller-rejected like before.
            return false;
        }

        throw CreateMissingMemberError(target, memberName, span, context, operation: MissingMemberOperation.Write);
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

        if (IsImmutableBuiltinType(target, context))
        {
            throw ImmutableTypeError(target, memberName, span);
        }

        if (target is PyType type && type.TryGetOwnMember(memberName, out _))
        {
            if (memberName == "__doc__")
            {
                throw new LythonRuntimeException("TypeError", $"cannot delete '__doc__' attribute of immutable type '{type.Name}'", span);
            }

            type.RemoveOwnMember(memberName);
            return true;
        }

        // Deletable dynamic targets (ZipInfo slots) own their removal;
        // unknown names keep the shared missing path below.
        if (target is IPyDeletableDynamicAttributes deletable && deletable.TryDeleteMember(memberName, span))
        {
            return true;
        }

        if (target is PyException exceptionDelete)
        {
            if (memberName == "args")
            {
                throw new LythonRuntimeException("TypeError", "args may not be deleted", span);
            }

            if (memberName == "__dict__")
            {
                throw new LythonRuntimeException("TypeError", "cannot delete __dict__", span);
            }

            if (memberName == "__cause__")
            {
                throw new LythonRuntimeException("TypeError", "__cause__ may not be deleted", span);
            }

            if (memberName == "__context__")
            {
                throw new LythonRuntimeException("TypeError", "__context__ may not be deleted", span);
            }

            if (memberName == "__suppress_context__")
            {
                throw new LythonRuntimeException("TypeError", "can't delete numeric/char attribute", span);
            }

            if (exceptionDelete.CustomDict is not null &&
                exceptionDelete.CustomDict.Remove(PyString.FromString(memberName)))
            {
                return true;
            }

            return false;
        }

        ThrowIfReadOnlyBuiltinMember(target, memberName, span, context);

        return false;
    }

    // Members served by the per-type instance tables cannot be written or
    // deleted like CPython: methods report read-only while plain values use
    // the not-writable shape (bool names its int owner). The tables are
    // side-effect free, so probing them never runs guest code; dict-bearing
    // targets keep their own paths above.
    private static void ThrowIfReadOnlyBuiltinMember(object? target, string memberName, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (target is null
            || HasInstanceDict(target)
            || !TryResolveInstanceTableMember(target, memberName, out var resolved))
        {
            return;
        }

        if (resolved is not LythonRuntime.ICallable)
        {
            var ownerName = target is bool ? "int" : MissingMemberTypeName(target, context);
            throw new LythonRuntimeException(
                "AttributeError",
            $"attribute '{memberName}' of '{ownerName}' objects is not writable",
                span);
        }

        throw new LythonRuntimeException(
            "AttributeError",
            $"'{MissingMemberTypeName(target, context)}' object attribute '{memberName}' is read-only",
            span);
    }

    private static bool IsImmutableBuiltinType(object target, LythonRuntime.ExecutionContext context)
    {
        // Builtin immutable types reject attribute writes and deletes like
        // CPython instead of reporting a missing attribute; user classes,
        // modules and instances keep their own paths above.
        if (target is PyType type)
        {
            return type.Name is "object" or "type";
        }

        return target is not null
            && LythonRuntime.TryGetValueClass(target, context, out var classValue)
            && classValue is PyType { Name: "type" };
    }

    private static LythonRuntimeException ImmutableTypeError(object target, string memberName, LythonSourceSpan span)
    {
        return new LythonRuntimeException(
            "TypeError",
            $"cannot set '{memberName}' attribute of immutable type '{TypeObjectName(target)}'",
            span);
    }

    private static string RuntimeTypeName(string name)
    {
        // CPython spells natively modeled C extension types dotted (datetime.date, re.Pattern) but Python-defined classes short (Random, NormalDist).
        return name is "random.Random" or "statistics.NormalDist" ? ShortTypeName(name) : name;
    }

    private static string MissingMemberTypeName(object? target, LythonRuntime.ExecutionContext context) => target switch
    {
        PyException exception => exception.TypeName,
        LythonRuntime.DictKeysView => "dict_keys",
        LythonRuntime.DictValuesView => "dict_values",
        LythonRuntime.DictItemsView => "dict_items",
        ChainMapKeysView => "KeysView",
        ChainMapValuesView => "ValuesView",
        ChainMapItemsView => "ItemsView",
        // Dotted C-extension names match CPython; deque splits from the
        // short concat template, so it keeps its literal here.
        PyDateTime or PyDate or PyTime or PyTimedelta or
            PyTimezone or PyDecimal or PyDefaultDict => RuntimeErrors.OperandTypeName(target),
        PyDeque => "collections.deque",
        PySuper => "super",
        _ => LythonRuntime.UnboundTypeMethod.PythonTypeName(target, context),
    };

    private static bool HasInstanceDict(object target) => target switch
    {
        PyModule => true,
        PyException => true,
        PyInstance instance => instance.Type.Name != "object",
        _ => false,
    };

    private static string TypeObjectName(object target) => target switch
    {
        PyType type => type.Name,
        PyBuiltinRuntimeType runtimeType => RuntimeTypeName(runtimeType.Name),
        LythonRuntime.ExceptionTypeValue exceptionType => exceptionType.TypeName,
        INamedRuntimeCallable named => ShortTypeName(named.Name),
        _ => "type",
    };

    private static string ShortTypeName(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? name : name.Substring(dot + 1);
    }

    public static LythonRuntimeException CreateMissingMemberError(object target, string memberName, LythonSourceSpan span, LythonRuntime.ExecutionContext context, MissingMemberOperation operation = MissingMemberOperation.Read)
    {
        // Reads name modules after the module itself; deletions and writes
        // use the plain object shape, with a no-dict suffix outside
        // instance-dict populations, exactly like CPython.
        var suffix = operation != MissingMemberOperation.Read && !HasInstanceDict(target)
            ? " and no __dict__ for setting new attributes"
            : string.Empty;
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
            PyModule module when operation == MissingMemberOperation.Read => new LythonRuntimeException(
                "AttributeError",
                $"module '{module.Name}' has no attribute '{memberName}'",
                span),
            _ when target is not null
                && LythonRuntime.TryGetValueClass(target, context, out var classValue)
                && classValue is PyType { Name: "type" } => new LythonRuntimeException(
                "AttributeError",
                $"type object '{TypeObjectName(target)}' has no attribute '{memberName}'",
                span),
            _ => new LythonRuntimeException(
                "AttributeError",
                $"'{MissingMemberTypeName(target, context)}' object has no attribute '{memberName}'{suffix}",
                span)
        };
    }
}
