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

            var zeroResult = context.ImplicitSuperReceiver switch
            {
                PyInstance instance => new PySuper(context.ImplicitSuperAnchorType, instance, instance.Type),
                PyType type => new PySuper(context.ImplicitSuperAnchorType, type, type),
                _ => throw new LythonRuntimeException("TypeError", "zero-argument super() could not resolve the current receiver.", span)
            };

            // The anchor and receiver stay aliased; own the descriptor shell.
            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            return zeroResult;
        }

        if (arguments.Length == 1)
        {
            if (arguments[0] is not PyType singleAnchorType)
            {
                throw new LythonRuntimeException("TypeError", "super(type) expects the argument to be a class.", span);
            }

            // Unbound super carries no object; member lookup beyond its own
            // descriptors misses like CPython.
            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            return new PySuper(singleAnchorType, null, null);
        }

        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects exactly two arguments in Lython.", span);
        }

        if (arguments[0] is not PyType anchorType)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects the first argument to be a class.", span);
        }

        var result = arguments[1] switch
        {
            PyInstance instance when instance.Type.IsSubtypeOf(anchorType) => new PySuper(anchorType, instance, instance.Type),
            PyType type when type.IsSubtypeOf(anchorType) => new PySuper(anchorType, type, type),
            PyInstance => throw new LythonRuntimeException("TypeError", "super(type, object) expects the instance to be an instance of the given class or its subclass.", span),
            PyType => throw new LythonRuntimeException("TypeError", "super(type, object) expects the class argument to be a subclass of the given class.", span),
            _ => throw new LythonRuntimeException("TypeError", "super(type, object) expects the second argument to be an instance or class.", span)
        };

        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return result;
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

        var items = await PyIteration.MaterializeAsync(arguments[0], span, context).ConfigureAwait(false);
        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Tuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
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
        if (arguments.Length == 0)
        {
            return PyTuple.Empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "tuple(iterable) expects one argument.", span);
        }

        var items = await PyIteration.MaterializeAsync(arguments[0], span, context).ConfigureAwait(false);
        var result = new PyTuple(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Dict(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
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

            result.SetItem(ValidateDictionaryKey(key, span), value);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Set(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var empty = new PySet(context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(empty, empty.CommittedStorageBytes);
            return empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            result.Add(ValidateSetItem(item, span));
            context.ObserveCollectionCount(result.Count, span);
        }

        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
        return result;
    }

    private static async ValueTask<object> SetAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var empty = new PySet(context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(empty, empty.CommittedStorageBytes);
            return empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            result.Add(ValidateSetItem(item, span));
            context.ObserveCollectionCount(result.Count, span);
        }

        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
        return result;
    }

    private static object IsInstance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects two arguments.", span);
        }

        return IsInstanceOf(arguments[0], arguments[1], span, context);
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

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span, ExecutionContext context)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate, context), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance() arg 2 must be a type, a tuple of types, or a union", span);
    }

    private static bool IsSubclassOf(object type, object baseSpec, LythonSourceSpan span)
    {
        if (!IsSupportedTypeSpecifier(type))
        {
            throw new LythonRuntimeException("TypeError", "issubclass() arg 1 must be a class", span);
        }

        if (TryMatchTypeTuple(baseSpec, candidate => IsSubclassAgainstSingleType(type, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "issubclass() arg 2 must be a class, a tuple of classes, or a union", span);
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

    private static bool IsInstanceAgainstSingleType(object value, object typeSpec, ExecutionContext context)
    {
        return typeSpec switch
        {
            PyType runtimeType => value switch
            {
                PyInstance instance => instance.Type.IsSubtypeOf(runtimeType),
                PyType typeValue => typeValue.MetaType is not null && typeValue.MetaType.IsSubtypeOf(runtimeType),
                _ => IsObjectRootType(runtimeType, context)
            },
            PyNamedTupleType namedTupleType => value is PyNamedTupleObject namedTuple && ReferenceEquals(namedTuple.Type, namedTupleType),
            TimeStructTimeType => value is TimeStructTimeValue,
            BuiltinCallable builtin => DoesObjectMatchBuiltinType(builtin.Name, value),
            PyBuiltinRuntimeType builtinType => DoesObjectMatchBuiltinType(builtinType.Name, value),
            INamedRuntimeCallable namedCallable => DoesObjectMatchBuiltinType(namedCallable.Name, value),
            _ => false
        };
    }

    // The object root matches every value like CPython; it resolves through
    // the run builtins table since each run owns its type graph.
    private static bool IsObjectRootType(PyType runtimeType, ExecutionContext context)
        => context.TryGetBuiltin("object", out var objectBase) && ReferenceEquals(runtimeType, objectBase);

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
               subjectName is "collections.defaultdict" or "collections.Counter" && baseName == "dict" ||
               baseName == "object";
    }

    private static object Dict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.IsPositional).ToArray();
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
            if (argument.IsKeyword)
            {
                // Fresh names reclaim through the pool once the dict drops.
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshString(keyword);
                result.SetItem(keyword, argument.Value);
            }
        }

        context.ObserveCollectionCount(result.Count, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
        return result;
    }

    internal static void UpdateDictionaryFromSource(IChainMapSource target, object source, ExecutionContext context, LythonSourceSpan span)
    {
        // R13b: protocol-key inserts below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (source is PyDict mapping)
        {
            foreach (var pair in mapping)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return;
        }

        if (source is PyDefaultDict defaultdict)
        {
            foreach (var pair in defaultdict.Items)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return;
        }

        if (source is PyCounter counter)
        {
            foreach (var pair in counter.Items)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return;
        }

        if (source is PyChainMap chainMap)
        {
            foreach (var key in chainMap.BuildMergedKeys())
            {
                target.SetItem(key, chainMap.GetSubscript(key, span));
            }

            return;
        }

        var elementIndex = 0;
        foreach (var pair in ToSequence(source, span, context))
        {
            ReadUpdatePair(pair, elementIndex, context, span, out var key, out var elementValue);
            target.SetItem(ValidateDictionaryKey(key, span), elementValue);
            elementIndex++;
        }
    }

    // Pair elements validate with at most BoundedPairValidationCount pulls:
    // small shapes report their exact length while unbounded iterables never
    // pay a proportional transient for the message.
    private const int BoundedPairValidationCount = 100;

    // Non-sequence pair elements report the CPython conversion failure (with
    // the element index) instead of leaking the underlying iteration error.
    private static IEnumerator<object> ToPairEnumerator(object pair, int elementIndex, LythonSourceSpan span, ExecutionContext context)
    {
        try
        {
            return ToSequence(pair, span, context).GetEnumerator();
        }
        catch (PyNotIterableException)
        {
            throw new LythonRuntimeException("TypeError", "cannot convert dictionary update sequence element #" + elementIndex + " to a sequence", span);
        }
    }

    private static void ReadUpdatePair(object pair, int elementIndex, ExecutionContext context, LythonSourceSpan span, out object key, out object elementValue)
    {
        if (TryGetPairLength(pair, out var knownLength) && knownLength != 2)
        {
            throw new LythonRuntimeException("ValueError", "dictionary update sequence element #" + elementIndex + " has length " + knownLength + "; 2 is required", span);
        }

        using var enumerator = ToPairEnumerator(pair, elementIndex, span, context);
        key = PyNone.Instance;
        elementValue = PyNone.Instance;
        var pulled = 0;
        while (enumerator.MoveNext())
        {
            if (pulled == 0)
            {
                key = enumerator.Current;
            }
            else if (pulled == 1)
            {
                elementValue = enumerator.Current;
            }

            pulled++;
            if (pulled > BoundedPairValidationCount)
            {
                throw new LythonRuntimeException("ValueError", "dictionary update sequence element has length other than 2", span);
            }
        }

        if (pulled != 2)
        {
            throw new LythonRuntimeException("ValueError", "dictionary update sequence element #" + elementIndex + " has length " + pulled + "; 2 is required", span);
        }
    }

    private static bool TryGetPairLength(object pair, out int length)
    {
        length = pair switch
        {
            PyList list => list.Count,
            PyTuple tuple => tuple.Count,
            PyString text => text.Length,
            PyBytes bytes => bytes.Length,
            PyDict mapping => mapping.Count,
            PySet set => set.Count,
            PyRange range => range.Length > int.MaxValue ? -1 : (int)range.Length,
            System.Collections.ICollection collection => collection.Count,
            _ => -1,
        };

        return length >= 0;
    }

    internal static object UpdateDictionary(
        PyDict target,
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.IsPositional).ToArray();
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
            if (argument.IsKeyword)
            {
                // Fresh names reclaim through the pool once the dict drops (dict-ctor kwargs-key pattern).
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshString(keyword);
                target.SetItem(keyword, argument.Value);
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
            "random.Random" or
            "zipfile.ZipInfo" or
            "collections.defaultdict" or
            "collections.Counter" or
            "collections.deque" or
            "collections.ChainMap";
    }

    internal static bool DoesObjectMatchBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "bool" => value is bool,
            "int" => value is BigInteger or int or bool,
            "float" => value is double,
            "list" => value is PyList,
            "tuple" => value is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject or TimeStructTimeValue,
            "dict" => value is PyDict or PyDefaultDict or PyCounter,
            "collections.defaultdict" => value is PyDefaultDict,
            "collections.Counter" => value is PyCounter,
            "collections.deque" => value is PyDeque,
            "collections.ChainMap" => value is PyChainMap,
            "set" => value is PySet,
            "str" => value is PyString,
            "bytes" => value is PyBytes,
            "range" => value is PyRange,
            "pathlib.Path" or "pathlib.PurePath" or "pathlib.PurePosixPath" or "pathlib.PosixPath" => value is PyPath,
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            "datetime.tzinfo" => value is PyTimezone,
            "statistics.NormalDist" => value is StatisticsModule.PyNormalDist,
            "zipfile.ZipInfo" => value is PyZipInfo,
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

        var name = ExpectAttributeName(arguments[1], span);
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

        throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span, context);
    }

    private static object HasAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "hasattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], span);
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

        var name = ExpectAttributeName(arguments[1], span);
        if (!PyMemberAccess.TryAssign(arguments[0], name, arguments[2], context, span))
        {
            throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span, context, operation: MissingMemberOperation.Write);
        }

        return PyNone.Instance;
    }

    private static object DelAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "delattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], span);
        if (!PyMemberAccess.TryDelete(arguments[0], name, context, span))
        {
            throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span, context, operation: MissingMemberOperation.Delete);
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

    private static object Globals(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "globals() takes no arguments (" + arguments.Length + " given)", span);
        }

        return BuildGlobalsDictionary(context, span);
    }

    // Globals resolve through the root context like name loads: module
    // assignments live in the module executable frame unless mirrored,
    // so the frame wins over the context variables on collision.
    private static PyDict BuildGlobalsDictionary(ExecutionContext context, LythonSourceSpan span)
    {
        var globalContext = GetGlobalContext(context);
        var scope = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in globalContext.Variables)
        {
            if (!ExecutionState.BuiltinNames.Contains(pair.Key))
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        if (globalContext.CurrentExecutableFrame is not null)
        {
            foreach (var pair in globalContext.CurrentExecutableFrame.EnumerateLocals())
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        context.ObserveCollectionCount(scope.Count, span);
        return scope;
    }

    private static object Locals(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "locals() takes no arguments (" + arguments.Length + " given)", span);
        }

        return BuildScopeDictionary(context, span, includeFrameLocals: true);
    }

    private static PyDict BuildScopeDictionary(ExecutionContext context, LythonSourceSpan span, bool includeFrameLocals)
    {
        var scope = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in context.Variables)
        {
            if (!ExecutionState.BuiltinNames.Contains(pair.Key))
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        if (includeFrameLocals && context.CurrentExecutableFrame is not null)
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        context.ObserveCollectionCount(scope.Count, span);
        return scope;
    }

    private static object Vars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return BuildScopeDictionary(context, span, includeFrameLocals: true);
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

            case PyException exceptionVars:
                // vars(e) is the live __dict__ like CPython.
                exceptionVars.CustomDict ??= new PyDict(context.MemoryGovernor, span);
                exceptionVars.CustomDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                return exceptionVars.CustomDict;

            default:
                throw new LythonRuntimeException("TypeError", "vars() argument must have __dict__ attribute", span);
        }
    }

    private static string ExpectAttributeName(object value, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var name))
        {
            throw new LythonRuntimeException("TypeError", $"attribute name must be string, not '{RuntimeErrors.OperandTypeName(value)}'", span);
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

            case PyZipInfo:
                names.Add("CRC");
                names.Add("comment");
                names.Add("compress_size");
                names.Add("compress_type");
                names.Add("create_system");
                names.Add("date_time");
                names.Add("external_attr");
                names.Add("extra");
                names.Add("file_size");
                names.Add("filename");
                names.Add("flag_bits");
                names.Add("header_offset");
                names.Add("is_dir");
                return names;

            case PySlice:
                names.Add("start");
                names.Add("stop");
                names.Add("step");
                return names;

            case PyException exceptionDir:
                names.Add("args");
                names.Add("message");
                names.Add("type");
                names.Add("__dict__");
                if (exceptionDir.CustomDict is not null)
                {
                    foreach (var key in exceptionDir.CustomDict.Keys)
                    {
                        if (key is PyString keyText)
                        {
                            names.Add(keyText.AsString());
                        }
                    }
                }

                return names;

            case PyString:
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

            case PyTuple:
                names.AddRange(TupleDirNames);
                return names;

            case PyRange:
                names.AddRange(RangeDirNames);
                return names;

            case PyNamedTupleObject namedTuple:
                foreach (var field in namedTuple.Type.FieldNames)
                {
                    names.Add(field);
                }

                names.AddRange(NamedTupleDirNames);
                return names;

            case PyTypingNamedTupleObject typingTuple:
                foreach (var field in typingTuple.FieldNames)
                {
                    names.Add(field);
                }

                names.AddRange(TypingNamedTupleDirNames);
                return names;

            case PyNamedTupleType namedTupleType:
                foreach (var field in namedTupleType.FieldNames)
                {
                    names.Add(field);
                }

                names.AddRange(NamedTupleTypeDirNames);
                return names;

            case PyTypingConstructedType constructed when constructed.Kind == PyTypingConstructedKind.NamedTuple:
                foreach (var field in constructed.FieldNames)
                {
                    names.Add(field);
                }

                names.AddRange(TupleDirNames);
                return names;

            case BigInteger or int or bool:
                names.AddRange(IntDirNames);
                return names;

            case double:
                names.AddRange(FloatDirNames);
                return names;

            case PySet:
                names.AddRange(SetDirNames);
                return names;

            case DictKeysView:
                names.AddRange(DictKeysViewDirNames);
                return names;

            case ChainMapKeysView:
                names.AddRange(DictKeysViewDirNames);
                return names;

            case ChainMapItemsView:
                names.AddRange(DictItemsViewDirNames);
                return names;

            case ChainMapValuesView:
                return names;

            case DictItemsView:
                names.AddRange(DictItemsViewDirNames);
                return names;

            case DictValuesView:
                return names;

            case BuiltinCallable builtin when builtin.Name is "list" or "str" or "bytes" or "set" or "tuple" or "int" or "float" or "bool" or "range":
                names.AddRange(builtin.Name switch
                {
                    "list" => ListDirNames,
                    "str" => StringDirNames,
                    "bytes" => BytesDirNames,
                    "tuple" => TupleDirNames,
                    "int" => IntMethodDirNames,
                    "bool" => IntMethodDirNames,
                    "range" => RangeMethodDirNames,
                    "float" => FloatMethodDirNames,
                    _ => SetDirNames,
                });
                names.Add("__new__");
                return names;

            case BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name):
                names.AddRange(BuiltinTypeMemberNames);
                return names;

            case DictCallable:
                names.AddRange(DictDirNames);
                names.Add("__new__");
                return names;

            default:
                return null;
        }
    }

    private static readonly string[] BuiltinTypeMemberNames =
    [
        "__name__",
        "__qualname__"
    ];

}
