using Lokad.Lython.Frontend;
using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static void PreloadImplicitSuperArguments(ExecutionContext context, LythonSourceSpan span)
    {
        // Python's direct-member form loads the free class cell and first local
        // before calling the resolved callable, even if it shadows the builtin.
        // These reads do not validate types or replace the ordinary call path.
        _ = ResolveName("__class__", span, context);
        if (context.FirstPositionalParameterName is not { } first)
            throw new LythonRuntimeException("RuntimeError", "super(): no arguments", span);
        if (!context.TryResolveOwnLocal(first, out _))
            throw RuntimeErrors.UnboundLocalVariable(first, span);
    }

    private static object Super(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            if (context.FirstPositionalParameterName is not { } firstName)
                throw new LythonRuntimeException("RuntimeError", "super(): no arguments", span);
            if (!context.TryResolveOwnLocal(firstName, out var receiver))
                throw new LythonRuntimeException("RuntimeError", "super(): arg[0] deleted", span);
            if (!context.TryResolveImplicitClassValue(out var anchor))
                throw new LythonRuntimeException("RuntimeError", "super(): __class__ cell not found", span);
            if (ReferenceEquals(anchor, UninitializedLocal))
                throw new LythonRuntimeException("RuntimeError", "super(): empty __class__ cell", span);
            if (anchor is not PyType)
                throw new LythonRuntimeException("RuntimeError", "super(): __class__ is not a supported type", span);
            return Super([anchor, receiver], span, context);
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
            PyNone => new PySuper(anchorType, null, null),
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

        var result = new PyList(arguments[0] is IReadOnlyCollection<object> known ? known.Count : 0, context.MemoryGovernor, span);
        try
        {
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                result.Add(item);
                context.CheckExecution(span);
            }
            return result;
        }
        catch { result.DiscardUnpublishedStorage(); throw; }
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

        var result = new PyList(arguments[0] is IReadOnlyCollection<object> known ? known.Count : 0, context.MemoryGovernor, span);
        try
        {
            await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
            {
                result.Add(item);
                context.CheckExecution(span);
            }
            return result;
        }
        catch { result.DiscardUnpublishedStorage(); throw; }
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
        context.CheckExecution(span);
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

        using var items = await PyIteration.MaterializeLeasedAsync(arguments[0], span, context).ConfigureAwait(false);
        var result = new PyTuple(items.Items, context.MemoryGovernor, span);
        context.CheckExecution(span);
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
            context.CheckExecution(span);
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

        context.CheckExecution(span);
        return result;
    }

    private static object Set(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var empty = new PySet(context.MemoryGovernor, span);
            return empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        return SetMembers.MaterializeSet(arguments[0], span, context);
    }

    private static async ValueTask<object> SetAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var empty = new PySet(context.MemoryGovernor, span);
            return empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        return await SetMembers.MaterializeSetAsync(arguments[0], span, context).ConfigureAwait(false);
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
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects two arguments.", span);
        }

        return IsSubclassOf(arguments[0], arguments[1], span, context);
    }

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span, ExecutionContext context)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate, context), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance() arg 2 must be a type, a tuple of types, or a union", span);
    }

    private static bool IsSubclassOf(object type, object baseSpec, LythonSourceSpan span, ExecutionContext context)
    {
        if (!IsSupportedTypeSpecifier(type))
        {
            throw new LythonRuntimeException("TypeError", "issubclass() arg 1 must be a class", span);
        }

        if (TryMatchTypeTuple(baseSpec, candidate => IsSubclassAgainstSingleType(type, candidate, context), out var matched))
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
            ExceptionTypeValue => true,
            PyType => true,
            PyNamedTupleType => true,
            TimeStructTimeType => true,
            UrllibParseModule.UrlResultType => true,
            BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => true,
            PyBuiltinRuntimeType builtinType when IsBuiltinTypeName(builtinType.Name) => true,
            INamedRuntimeCallable namedCallable when IsBuiltinTypeName(namedCallable.Name) => true,
            _ => false
        };
    }

    private static bool IsInstanceAgainstSingleType(object value, object typeSpec, ExecutionContext context)
    {
        if (typeSpec is PyTypedDictType)
            throw new LythonRuntimeException("TypeError", "TypedDict does not support instance or class checks.", null);
        return typeSpec switch
        {
            ExceptionTypeValue exceptionType => value is PyException exception &&
                MatchesExceptionType(exceptionType.ExceptionIdentity, exception.Identity),
            PyType runtimeType => value switch
            {
                ExceptionTypeValue or GzipFileClass or GzipFileMetaType => ReferenceEquals(TryGetBuiltinOrNull(context, "type"), runtimeType) || IsObjectRootType(runtimeType, context),
                PyInstance instance => instance.Type.IsSubtypeOf(runtimeType),
                PyType typeValue => typeValue.MetaType is not null && typeValue.MetaType.IsSubtypeOf(runtimeType),
                UrllibParseModule.UrlResultType => ReferenceEquals(TryGetBuiltinOrNull(context, "type"), runtimeType) || IsObjectRootType(runtimeType, context),
                _ => IsObjectRootType(runtimeType, context)
            },
            PyNamedTupleType namedTupleType => value is PyNamedTupleObject namedTuple && ReferenceEquals(namedTuple.Type, namedTupleType),
            TimeStructTimeType => value is TimeStructTimeValue,
            UrllibParseModule.UrlResultType urlType => value is UrllibParseModule.UrlResult result && ReferenceEquals(result.Type, urlType),
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

    private static bool IsSubclassAgainstSingleType(object type, object baseSpec, ExecutionContext context)
    {
        if (baseSpec is PyTypedDictType)
            throw new LythonRuntimeException("TypeError", "TypedDict does not support instance or class checks.", null);
        if (type is PathlibModule.PathlibPathType pathType)
        {
            context.TryGetBuiltin("object", out var objectBase);
            return pathType.GetMro(objectBase ?? PyNone.Instance).Contains(baseSpec);
        }
        if (type is ExceptionTypeValue exceptionType)
            return baseSpec is ExceptionTypeValue exceptionBase
                ? MatchesExceptionType(exceptionBase.ExceptionIdentity, exceptionType.ExceptionIdentity)
                : GetBuiltinTypeName(baseSpec, context) == "object";
        if (type is UrllibParseModule.UrlResultType urlType)
            return ReferenceEquals(type, baseSpec) || GetBuiltinTypeName(baseSpec, context) is "tuple" or "object";
        if (type is PyType runtimeSubject)
        {
            if (baseSpec is PyType runtimeBase)
            {
                return runtimeSubject.IsSubtypeOf(runtimeBase);
            }

            if (runtimeSubject.JsonBase != JsonBaseKind.None)
            {
                var jsonBaseName = GetBuiltinTypeName(baseSpec, context);
                return runtimeSubject.JsonBase == JsonBaseKind.Encoder
                    ? jsonBaseName is "json.JSONEncoder" or "object"
                    : jsonBaseName is "json.JSONDecoder" or "object";
            }

            return false;
        }

        var subjectName = type is PyType subjectType && subjectType.JsonBase != JsonBaseKind.None
            ? subjectType.JsonBase == JsonBaseKind.Encoder ? "json.JSONEncoder" : "json.JSONDecoder"
            : GetBuiltinTypeName(type, context);
        var baseName = GetBuiltinTypeName(baseSpec, context);
        if (subjectName is null || baseName is null)
        {
            return false;
        }

        return subjectName == baseName ||
               subjectName == "abc.ABCMeta" && baseName == "type" ||
               subjectName == "bool" && baseName == "int" ||
               subjectName is "collections.defaultdict" or "collections.Counter" && baseName == "dict" ||
               baseName == "object";
    }

    internal static object Dict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? source = null;
        foreach (var argument in arguments)
        {
            if (!argument.IsPositional) continue;
            if (source is not null)
                throw new LythonRuntimeException("TypeError", "dict expected at most 1 positional argument", span);
            source = argument.Value;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        if (source is not null)
        {
            UpdateDictionaryFromSource(result, source, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                result.SetItem(keyword, argument.Value);
            }
        }

        context.CheckExecution(span);
        return result;
    }

    internal static async ValueTask<object> DictAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the kwargs constructor above: keyword names stay pure
        // while the positional pair stream awaits each pull.
        object? source = null;
        foreach (var argument in arguments)
        {
            if (!argument.IsPositional) continue;
            if (source is not null)
                throw new LythonRuntimeException("TypeError", "dict expected at most 1 positional argument", span);
            source = argument.Value;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        if (source is not null)
        {
            await UpdateDictionaryFromSourceAsync(result, source, context, span).ConfigureAwait(false);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                result.SetItem(keyword, argument.Value);
            }
        }

        context.CheckExecution(span);
        return result;
    }

    internal static void UpdateDictionaryFromSource(IChainMapSource target, object source, ExecutionContext context, LythonSourceSpan span)
    {
        // R13b: protocol-key inserts below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (TryUpdateFromKnownMapping(target, source, span, context))
        {
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

    // Pure-memory mapping fast paths shared by the sync drain above and the
    // async twin below: no pulls, so no suspension is possible here.
    private static bool TryUpdateFromKnownMapping(IChainMapSource target, object source, LythonSourceSpan span, ExecutionContext context)
    {
        if (source is PyMappingProxy proxy) source = proxy.Dictionary;
        if (source is PyDict mapping)
        {
            foreach (var pair in mapping)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return true;
        }

        if (source is PyDefaultDict defaultdict)
        {
            foreach (var pair in defaultdict.Items)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return true;
        }

        if (source is PyCounter counter)
        {
            foreach (var pair in counter.Items)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return true;
        }

        if (source is PyChainMap chainMap)
        {
            foreach (var key in chainMap.BuildMergedKeys())
            {
                target.SetItem(key, chainMap.GetSubscript(key, span));
            }

            return true;
        }

        return false;
    }

    internal static async ValueTask UpdateDictionaryFromSourceAsync(IChainMapSource target, object source, ExecutionContext context, LythonSourceSpan span)
    {
        // Async twin of the drain above: known mappings stay on the shared
        // pure-memory path; arbitrary pair streams await each pull while pair
        // validation reuses the same streaming sync checks over materialized
        // pair values.
        using (PyStructuralGuard.PushAmbient(context, span))
        {
            if (TryUpdateFromKnownMapping(target, source, span, context)) return;
        }

        var elementIndex = 0;
        await foreach (var pair in ToSequenceAsync(source, span, context).ConfigureAwait(false))
        {
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            ReadUpdatePair(pair, elementIndex, context, span, out var key, out var elementValue);
            target.SetItem(ValidateDictionaryKey(key, span), elementValue);
            elementIndex++;
        }
    }

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
        // Drain unknown-length pairs for their exact error and iterator effects,
        // retaining only the first two values. Context-aware iteration provides
        // cancellation even when the pair never ends.
        long pulled = 0;
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

            pulled = checked(pulled + 1);
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

        ApplyUpdateKeywords(target, arguments, context, span);
        context.CheckExecution(span);
        return PyNone.Instance;
    }

    internal static async ValueTask<object> UpdateDictionaryAsync(
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
            await UpdateDictionaryFromSourceAsync(target, positional[0].Value, context, span).ConfigureAwait(false);
        }

        ApplyUpdateKeywords(target, arguments, context, span);
        context.CheckExecution(span);
        return PyNone.Instance;
    }

    private static void ApplyUpdateKeywords(
        PyDict target,
        CallArgumentValue[] arguments,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                target.SetItem(keyword, argument.Value);
            }
        }
    }

    private static string? GetBuiltinTypeName(object value, ExecutionContext context) => value switch
    {
        PyType type when type.Name is "object" or "type" &&
            context.TryGetBuiltin(type.Name, out var root) && ReferenceEquals(type, root) => type.Name,
        BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        PyBuiltinRuntimeType builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        INamedRuntimeCallable callable when IsBuiltinTypeName(callable.Name) => callable.Name,
        _ => null,
    };

    internal static bool IsBuiltinTypeName(string name)
    {
        return name is
            "typing.TypeVar" or "typing.TypeVarTuple" or "typing.ParamSpec" or "typing.TypeAliasType" or "types.GenericAlias" or "typing._GenericAlias" or
            "generator" or
            "bool" or
            "int" or
            "complex" or
            "float" or
            "list" or
            "tuple" or
            "slice" or
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
            "xml.etree.ElementTree.Element" or
            "random.Random" or
            "zipfile.ZipInfo" or
            "collections.defaultdict" or
            "collections.Counter" or
            "collections.deque" or
            "collections.ChainMap" or
            "io.BytesIO" or
            "io.StringIO" or
            "gzip.GzipFile" or
            "abc.ABCMeta" or
            "json.JSONDecoder" or
            "json.JSONEncoder" or "argparse.Namespace" or "argparse.ArgumentParser";
    }

    internal static bool DoesObjectMatchBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "typing.TypeVar" => value is PyTypeParameter { Kind: TypeParameterKind.TypeVar },
            "typing.TypeVarTuple" => value is PyTypeParameter { Kind: TypeParameterKind.TypeVarTuple },
            "typing.ParamSpec" => value is PyTypeParameter { Kind: TypeParameterKind.ParamSpec },
            "typing.TypeAliasType" => value is PyTypeAlias,
            "types.GenericAlias" or "typing._GenericAlias" => value is PyGenericAlias,
            "argparse.Namespace" => value is ArgparseNamespaceObject,
            "argparse.ArgumentParser" => value is ArgumentParserObject,
            "generator" => value is PyGenerator or PyGeneratorExpression,
            "bool" => value is bool,
            "type" => value is UrllibParseModule.UrlResultType,
            "int" => Numbers.PyNumberOps.IsInteger(value),
            "float" => value is double,
            "complex" => value is PyComplex,
            "list" => value is PyList,
            "tuple" => PyTupleLike.TryGetItems(value, out _),
            "dict" => value is PyDict or PyDefaultDict or PyCounter,
            "mappingproxy" => value is PyMappingProxy,
            "collections.defaultdict" => value is PyDefaultDict,
            "collections.Counter" => value is PyCounter,
            "collections.deque" => value is PyDeque,
            "collections.ChainMap" => value is PyChainMap,
            "io.BytesIO" => value is BytesIOObject,
            "io.StringIO" => value is StringIOObject,
            "gzip.GzipFile" => value is GzipFileObject,
            "abc.ABCMeta" => value is GzipFileClass,
            "json.JSONDecoder" => value is JsonDecoderObject || value is PyInstance { Type.JsonBase: JsonBaseKind.Decoder },
            "json.JSONEncoder" => value is JsonEncoderObject || value is PyInstance { Type.JsonBase: JsonBaseKind.Encoder },
            "set" => value is PySet,
            "str" => value is PyString,
            "bytes" => value is PyBytes,
            "range" => value is PyRange,
            "slice" => value is PySlice,
            "pathlib.Path" or "pathlib.PosixPath" => value is PyPath { IsPure: false },
            "pathlib.PurePath" or "pathlib.PurePosixPath" => value is PyPath,
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            "datetime.tzinfo" => value is PyTimezone,
            "statistics.NormalDist" => value is StatisticsModule.PyNormalDist,
            "xml.etree.ElementTree.Element" => value is ElementTreeModule.Element,
            "zipfile.ZipInfo" => value is PyZipInfo,
            "random.Random" => value is RandomModule.PyRandom,
            _ => false
        };
    }

    private static async ValueTask<object> GetAttrAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3) throw new LythonRuntimeException("TypeError", "getattr expects two or three arguments", span);
        var name = ExpectAttributeName(arguments[1], span);
        try
        {
            var result = await TryResolveRuntimeMemberAsync(arguments[0], name, context, span).ConfigureAwait(false);
            if (result.Found) return result.Value;
        }
        catch (LythonRuntimeException exception) when (exception.ExceptionType == "AttributeError" && arguments.Length == 3)
        { return arguments[2]; }
        if (arguments.Length == 3) return arguments[2];
        throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span, context);
    }

    private static async ValueTask<object> HasAttrAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2) throw new LythonRuntimeException("TypeError", "hasattr expects two arguments", span);
        var name = ExpectAttributeName(arguments[1], span);
        try { return (await TryResolveRuntimeMemberAsync(arguments[0], name, context, span).ConfigureAwait(false)).Found; }
        catch (LythonRuntimeException exception) when (exception.ExceptionType == "AttributeError") { return false; }
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

    // One live dictionary per actual module; locals() keeps its snapshot contract.
    private static PyDict BuildGlobalsDictionary(ExecutionContext context, LythonSourceSpan span)
    {
        var globalContext = GetGlobalContext(context);
        var scope = globalContext.GlobalNamespace ?? CreateGlobalNamespace(globalContext, span);
        context.CheckExecution(span);
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
        // Python 3.13 snapshots optimized function locals. Module and class
        // bodies expose their actual execution namespace instead.
        // Inlined comprehensions keep their isolated snapshot even when their
        // enclosing execution context is a module or class body.
        if (context.IncludeCallerLocals && (context.ParentContext is null || context.IsClassBody))
            return context.GlobalNamespace ?? CreateGlobalNamespace(context, span);
        var scope = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in context.IncludeCallerLocals ? context.Variables : [])
        {
            if (!ExecutionState.BuiltinNames.Contains(pair.Key))
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        if (includeFrameLocals && context.CurrentExecutableFrame is null)
            foreach (var name in context.ScopeFacts.NonlocalNames)
                if (context.TryReadNonlocalValue(name, out var value))
                    scope.SetItem(PyString.FromString(name, context.MemoryGovernor, span), value);

        // Lowered methods keep the lexical class cell outside their local
        // dictionary. Expose a captured cell just as executable snapshots do,
        // while respecting local/global shadowing and an unbound cell.
        if (includeFrameLocals && context.CurrentExecutableFrame is null && !context.IsClassBody &&
            context.ScopeFacts.RequiresClassCell && !context.ScopeFacts.IsGlobal("__class__") &&
            !context.ScopeFacts.LocalNames.Contains("__class__") &&
            context.TryResolveImplicitClassValue(out var owner) && !ReferenceEquals(owner, UninitializedLocal))
            scope.SetItem(PyString.FromString("__class__", context.MemoryGovernor, span), owner);

        if (includeFrameLocals && context.IncludeCallerLocals && context.CurrentExecutableFrame is not null)
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateVisibleLocals())
            {
                scope.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }
        }

        if (includeFrameLocals)
            foreach (var binding in context.EnumerateInlinedBindings())
                foreach (var name in binding.ScopeFacts.LocalNames)
                {
                    var key = PyString.FromString(name, context.MemoryGovernor, span);
                    if (binding.Variables.TryGetValue(name, out var value)) scope.SetItem(key, value);
                    else scope.Remove(key);
                }

        context.CheckExecution(span);
        return scope;
    }

    private static object Vars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => VarsCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> VarsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => VarsCoreAsync(arguments, span, context, true);
    private static async ValueTask<object> VarsCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length == 0) return BuildScopeDictionary(context, span, true);
        if (arguments.Length != 1) throw RuntimeErrors.Type("vars([object]) expects zero or one arguments.", span);
        var member = asynchronous ? await TryResolveRuntimeMemberAsync(arguments[0], "__dict__", context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(arguments[0], "__dict__", context, span, out var value) ? (true, value) : (false, (object)PyNone.Instance);
        return member.Item1 ? member.Item2 : throw RuntimeErrors.Type("vars() argument must have __dict__ attribute", span);
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

            case PyNamedTupleType namedTupleType:
                foreach (var field in namedTupleType.FieldNames)
                {
                    names.Add(field);
                }

                names.AddRange(NamedTupleTypeDirNames);
                return names;

            case object integer when Numbers.PyNumberOps.IsInteger(integer):
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
