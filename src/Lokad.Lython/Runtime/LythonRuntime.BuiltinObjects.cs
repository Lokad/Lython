using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Super(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            if (context.ImplicitSuperAnchorType is null || context.ImplicitSuperReceiver is null)
            {
                throw new LythonRuntimeException("TypeError", "zero-argument super() is only supported inside instance methods, classmethods, and property accessors in Lython.", span);
            }

            return context.ImplicitSuperReceiver switch
            {
                PyInstance instance => new PySuper(context.ImplicitSuperAnchorType, instance, instance.Type),
                PyType type => new PySuper(context.ImplicitSuperAnchorType, type, type),
                _ => throw new LythonRuntimeException("TypeError", "zero-argument super() could not resolve the current receiver.", span)
            };
        }

        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects exactly two arguments in Lython.", span);
        }

        if (arguments[0] is not PyType anchorType)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects the first argument to be a class.", span);
        }

        return arguments[1] switch
        {
            PyInstance instance when instance.Type.IsSubtypeOf(anchorType) => new PySuper(anchorType, instance, instance.Type),
            PyType type when type.IsSubtypeOf(anchorType) => new PySuper(anchorType, type, type),
            PyInstance => throw new LythonRuntimeException("TypeError", "super(type, object) expects the instance to be an instance of the given class or its subclass.", span),
            PyType => throw new LythonRuntimeException("TypeError", "super(type, object) expects the class argument to be a subclass of the given class.", span),
            _ => throw new LythonRuntimeException("TypeError", "super(type, object) expects the second argument to be an instance or class.", span)
        };
    }

    private static object List(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "list(iterable) expects one argument.", span);
        }

        var result = new PyList(ToSequence(arguments[0], span, context), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> ListAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "list(iterable) expects one argument.", span);
        }

        var items = await PyIteration.MaterializeAsync(arguments[0], span).ConfigureAwait(false);
        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Tuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return PyTuple.Empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "tuple(iterable) expects one argument.", span);
        }

        var result = new PyTuple(ToSequence(arguments[0], span, context), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> TupleAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return PyTuple.Empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "tuple(iterable) expects one argument.", span);
        }

        var items = await PyIteration.MaterializeAsync(arguments[0], span).ConfigureAwait(false);
        var result = new PyTuple(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Dict(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return new PyDict(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects one argument.", span);
        }

        if (arguments[0] is PyDict sourceDict)
        {
            var copied = new PyDict(sourceDict, context.MemoryGovernor, span);
            context.ObserveCollectionCount(copied.Count, span);
            return copied;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in ToSequence(arguments[0], span, context))
        {
            using var enumerator = ToSequence(pair, span, context).GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var key = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var value = enumerator.Current;
            if (enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            result.SetItem(ValidateDictionaryKey(key, span, context.MemoryGovernor), value);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> DictAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return new PyDict(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects one argument.", span);
        }

        if (arguments[0] is PyDict sourceDict)
        {
            var copied = new PyDict(sourceDict, context.MemoryGovernor, span);
            context.ObserveCollectionCount(copied.Count, span);
            return copied;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        await foreach (var pair in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            var values = await PyIteration.MaterializeAsync(pair, span).ConfigureAwait(false);
            if (values.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            result.SetItem(ValidateDictionaryKey(values[0], span, context.MemoryGovernor), values[1]);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Set(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PySet(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static async ValueTask<object> SetAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PySet(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static object IsInstance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects two arguments.", span);
        }

        return IsInstanceOf(arguments[0], arguments[1], span);
    }

    private static object IsSubclass(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects two arguments.", span);
        }

        return IsSubclassOf(arguments[0], arguments[1], span);
    }

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects a class or tuple of classes.", span);
    }

    private static bool IsSubclassOf(object type, object baseSpec, LythonSourceSpan span)
    {
        if (!IsSupportedTypeSpecifier(type))
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects the first argument to be a class.", span);
        }

        if (TryMatchTypeTuple(baseSpec, candidate => IsSubclassAgainstSingleType(type, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects a class or tuple of classes.", span);
    }

    private static bool TryMatchTypeTuple(object typeSpec, Func<object, bool> predicate, out bool matched)
    {
        if (typeSpec is PyTuple tuple)
        {
            foreach (var candidate in tuple)
            {
                if (!IsSupportedTypeSpecifier(candidate))
                {
                    matched = false;
                    return false;
                }

                if (predicate(candidate))
                {
                    matched = true;
                    return true;
                }
            }

            matched = false;
            return true;
        }

        matched = predicate(typeSpec);
        return matched || IsSupportedTypeSpecifier(typeSpec);
    }

    private static bool IsSupportedTypeSpecifier(object typeSpec)
    {
        return typeSpec switch
        {
            PyType => true,
            PyNamedTupleType => true,
            TimeStructTimeType => true,
            BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => true,
            PyBuiltinRuntimeType builtinType when IsBuiltinTypeName(builtinType.Name) => true,
            INamedRuntimeCallable namedCallable when IsBuiltinTypeName(namedCallable.Name) => true,
            _ => false
        };
    }

    private static bool IsInstanceAgainstSingleType(object value, object typeSpec)
    {
        return typeSpec switch
        {
            PyType runtimeType => value switch
            {
                PyInstance instance => instance.Type.IsSubtypeOf(runtimeType),
                PyType typeValue => typeValue.MetaType is not null && typeValue.MetaType.IsSubtypeOf(runtimeType),
                _ => false
            },
            PyNamedTupleType namedTupleType => value is PyNamedTupleObject namedTuple && ReferenceEquals(namedTuple.Type, namedTupleType),
            TimeStructTimeType => value is TimeStructTimeValue,
            BuiltinCallable builtin => DoesObjectMatchBuiltinType(builtin.Name, value),
            PyBuiltinRuntimeType builtinType => DoesObjectMatchBuiltinType(builtinType.Name, value),
            INamedRuntimeCallable namedCallable => DoesObjectMatchBuiltinType(namedCallable.Name, value),
            _ => false
        };
    }

    private static bool IsSubclassAgainstSingleType(object type, object baseSpec)
    {
        if (type is PyType runtimeSubject)
        {
            return baseSpec is PyType runtimeBase && runtimeSubject.IsSubtypeOf(runtimeBase);
        }

        var subjectName = GetBuiltinTypeName(type);
        var baseName = GetBuiltinTypeName(baseSpec);
        if (subjectName is null || baseName is null)
        {
            return false;
        }

        return subjectName == baseName ||
               subjectName == "bool" && baseName == "int" ||
               baseName == "object";
    }

    private static object Dict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.Name is null).ToArray();
        if (positional.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "dict expected at most 1 positional argument", span);
        }

        var result = new PyDict(context.MemoryGovernor, span);
        if (positional.Length == 1)
        {
            UpdateDictionaryFromSource(result, positional[0].Value, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.Name is not null)
            {
                result.SetItem(PyString.FromString(argument.Name, context.MemoryGovernor, span), argument.Value);
            }
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static void UpdateDictionaryFromSource(PyDict target, object source, ExecutionContext context, LythonSourceSpan span)
    {
        if (source is PyDict mapping)
        {
            foreach (var pair in mapping)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return;
        }

        foreach (var pair in ToSequence(source, span, context))
        {
            var values = ToSequence(pair, span, context).ToArray();
            if (values.Length != 2)
            {
                throw new LythonRuntimeException("ValueError", "dictionary update sequence element has length other than 2", span);
            }

            target.SetItem(ValidateDictionaryKey(values[0], span, context.MemoryGovernor), values[1]);
        }
    }

    private static object UpdateDictionary(
        PyDict target,
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.Name is null).ToArray();
        if (positional.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "dict.update expected at most 1 positional argument", span);
        }

        target.AttachMemoryGovernor(context.MemoryGovernor, span);
        if (positional.Length == 1)
        {
            UpdateDictionaryFromSource(target, positional[0].Value, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.Name is not null)
            {
                target.SetItem(PyString.FromString(argument.Name, context.MemoryGovernor, span), argument.Value);
            }
        }

        context.ObserveCollectionCount(target.Count, span);
        return PyNone.Instance;
    }

    private static string? GetBuiltinTypeName(object value) => value switch
    {
        PyType type when type.Name is "object" or "type" => type.Name,
        BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        PyBuiltinRuntimeType builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        INamedRuntimeCallable callable when IsBuiltinTypeName(callable.Name) => callable.Name,
        _ => null,
    };

    private static bool IsBuiltinTypeName(string name)
    {
        return name is
            "bool" or
            "int" or
            "float" or
            "list" or
            "tuple" or
            "dict" or
            "set" or
            "str" or
            "bytes" or
            "pathlib.Path" or
            "pathlib.PurePath" or
            "pathlib.PurePosixPath" or
            "pathlib.PosixPath" or
            "datetime.timedelta" or
            "datetime.date" or
            "datetime.time" or
            "datetime.datetime" or
            "datetime.timezone" or
            "datetime.tzinfo" or
            "statistics.NormalDist" or
            "random.Random";
    }

    private static bool DoesObjectMatchBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "bool" => value is bool,
            "int" => value is BigInteger or int or bool,
            "float" => value is double,
            "list" => value is PyList,
            "tuple" => value is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject or TimeStructTimeValue,
            "dict" => value is PyDict,
            "set" => value is PySet,
            "str" => value is PyString or string,
            "bytes" => value is PyBytes,
            "pathlib.Path" or "pathlib.PurePath" or "pathlib.PurePosixPath" or "pathlib.PosixPath" => value is PyPath,
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            "datetime.tzinfo" => value is PyTimezone,
            "statistics.NormalDist" => value is StatisticsModule.PyNormalDist,
            "random.Random" => value is RandomModule.PyRandom,
            _ => false
        };
    }

    private static object GetAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "getattr(object, name[, default]) expects two or three arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "getattr(object, name[, default])", span);
        try
        {
            if (PyMemberAccess.TryResolve(arguments[0], name, context, span, out var value))
            {
                return value;
            }
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError" && arguments.Length == 3)
        {
            return arguments[2];
        }

        if (arguments.Length == 3)
        {
            return arguments[2];
        }

        throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span);
    }

    private static object HasAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "hasattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "hasattr(object, name)", span);
        if (arguments[0] is PyException && name == "message")
        {
            return false;
        }
        try
        {
            return PyMemberAccess.TryResolve(arguments[0], name, context, span, out _);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError")
        {
            return false;
        }
    }

    private static object SetAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "setattr(object, name, value) expects three arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "setattr(object, name, value)", span);
        if (!PyMemberAccess.TryAssign(arguments[0], name, arguments[2], context, span))
        {
            throw new LythonRuntimeException("AttributeError", $"Object has no writable attribute '{name}'.", span);
        }

        return PyNone.Instance;
    }

    private static object DelAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "delattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "delattr(object, name)", span);
        if (!PyMemberAccess.TryDelete(arguments[0], name, context, span))
        {
            throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{name}'.", span);
        }

        return PyNone.Instance;
    }

    private static object Dir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return CreateNameList(
                EnumerateCurrentLocalNames(context),
                context,
                span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dir([object]) expects zero or one arguments.", span);
        }

        var names = EnumerateDirNames(arguments[0]);
        if (names is null)
        {
            throw new LythonRuntimeException("TypeError", "dir(object) is not supported for this object.", span);
        }

        return CreateNameList(names, context, span);
    }

    private static object Vars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var locals = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in context.Variables)
            {
                if (!ExecutionState.BuiltinNames.Contains(pair.Key))
                {
                    locals.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                }
            }

            if (context.CurrentExecutableFrame is not null)
            {
                foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
                {
                    locals.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                }
            }

            context.ObserveCollectionCount(locals.Count, span);
            return locals;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "vars([object]) expects zero or one arguments.", span);
        }

        var result = new PyDict(context.MemoryGovernor, span);
        switch (arguments[0])
        {
            case PyInstance instance:
                foreach (var pair in instance.EnumerateOwnAttributes())
                {
                    result.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                    context.ObserveCollectionCount(result.Count, span);
                }

                return result;

            case PyType type:
                foreach (var pair in type.EnumerateOwnMembers())
                {
                    result.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                    context.ObserveCollectionCount(result.Count, span);
                }

                return result;

            case PyModule module:
                foreach (var name in module.MemberNames)
                {
                    if (module.TryGetMember(name, out var value))
                    {
                        result.SetItem(PyString.FromString(name, context.MemoryGovernor, span), value);
                        context.ObserveCollectionCount(result.Count, span);
                    }
                }

                return result;

            default:
                throw new LythonRuntimeException("TypeError", "vars(object) expects an object with a Python-shaped attribute dictionary.", span);
        }
    }

    private static string ExpectAttributeName(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var name))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects name to be a string.", span);
        }

        return name.AsString();
    }

    private static List<string>? EnumerateDirNames(object value)
    {
        var names = new List<string>();
        switch (value)
        {
            case PyInstance instance:
                foreach (var pair in instance.EnumerateOwnAttributes())
                {
                    names.Add(pair.Key);
                }

                names.Add("__class__");
                foreach (var name in instance.Type.EnumerateMemberNames())
                {
                    names.Add(name);
                }

                return names;

            case PyType type:
                foreach (var name in BuiltinTypeMemberNames)
                {
                    names.Add(name);
                }

                foreach (var name in type.EnumerateMemberNames())
                {
                    names.Add(name);
                }

                return names;

            case PyModule module:
                foreach (var name in module.MemberNames)
                {
                    names.Add(name);
                }

                return names;

            case PySlice:
                names.Add("start");
                names.Add("stop");
                names.Add("step");
                return names;

            case PyException:
                names.Add("args");
                names.Add("message");
                names.Add("type");
                return names;

            case PyString or string:
                names.AddRange(StringDirNames);
                return names;

            case PyBytes:
                names.AddRange(BytesDirNames);
                return names;

            case PyList:
                names.AddRange(ListDirNames);
                return names;

            case PyDict:
                names.AddRange(DictDirNames);
                return names;

            case PySet:
                names.AddRange(SetDirNames);
                return names;

            case BigInteger or int or double or bool:
                names.Add("__class__");
                return names;

            case BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name):
                names.AddRange(BuiltinTypeMemberNames);
                return names;

            default:
                return null;
        }
    }

    private static readonly string[] BuiltinTypeMemberNames =
    [
        "__base__",
        "__bases__",
        "__class__",
        "__mro__",
        "__name__",
        "__qualname__"
    ];

}
