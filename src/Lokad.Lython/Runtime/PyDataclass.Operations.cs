using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    public static object ReplaceInstance(
        PyInstance instance,
        IReadOnlyList<CallArgumentValue> changeArguments,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context,
        string owner)
    {
        if (instance.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(obj, **changes) expects a dataclass instance.", span);
        }

        var changes = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var argument in changeArguments)
        {
            if (argument.IsPositional)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
            }

            if (!changes.TryAdd(argument.KeywordName, argument.Value))
            {
                throw CallErrors.MultipleValues(PythonCallableKind.Builtin, owner, argument.KeywordName, span);
            }
        }

        foreach (var change in changes.Keys)
        {
            if (!instance.Type.DataclassFieldsByName.RequireNotNull().TryGetValue(change, out var field))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}() got an unexpected field '{change}'.", span);
            }

            if (!field.Init)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}() cannot override init=False field '{change}'.", span);
            }
        }

        var callArguments = new List<CallArgumentValue>(instance.Type.DataclassFields.Count);
        foreach (var field in instance.Type.DataclassFields)
        {
            if (field.Kind == DataclassFieldKind.ClassVar || !field.Init)
            {
                continue;
            }

            if (changes.TryGetValue(field.Name, out var changedValue))
            {
                callArguments.Add(CallArgumentValue.Keyword(field.Name, changedValue));
                continue;
            }

            if (field.Kind == DataclassFieldKind.InitVar)
            {
                if (field.HasDefaultFactory)
                {
                    callArguments.Add(CallArgumentValue.Keyword(field.Name, DataclassInitMethod.InvokeDefaultFactory(field, span, context)));
                    continue;
                }

                if (field.HasDefault)
                {
                    callArguments.Add(CallArgumentValue.Keyword(field.Name, field.DefaultValue));
                    continue;
                }

                throw new LythonRuntimeException("ValueError", $"InitVar '{field.Name}' must be specified with {owner}().", span);
            }

            _ = instance.TryGetOwnAttribute(field.Name, out var existingValue);
            callArguments.Add(CallArgumentValue.Keyword(field.Name, existingValue ?? PyNone.Instance));
        }

        return instance.Type.Invoke(callArguments.ToArray(), span, context);
    }

    public static object IsDataclass(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.is_dataclass(value) expects one argument.", span);
        }

        return arguments[0] switch
        {
            PyType type => type.DataclassFields is not null,
            PyInstance instance => instance.Type.DataclassFields is not null,
            _ => false
        };
    }

    public static object Fields(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.fields(class_or_instance) expects one argument.", span);
        }

        var type = GetDataclassType(arguments[0], span, "dataclasses.fields()");
        var visibleFields = type.DataclassHelperFields.RequireNotNull();
        if (!type.TryGetOwnMember("__dataclass_fields__", out var rawFieldMap) || rawFieldMap is not PyDict fieldMap)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.fields() could not read the dataclass field map.", span);
        }

        var items = new object[visibleFields.Length];
        for (var i = 0; i < visibleFields.Length; i++)
        {
            var key = PyString.FromString(visibleFields[i].Name);
            if (!fieldMap.TryGetValue(key, out var item))
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.fields() found an incomplete dataclass field map.", span);
            }

            items[i] = item;
        }

        return new PyTuple(items, context.MemoryGovernor, span);
    }

    public static object AsDict(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.asdict(obj[, dict_factory]) expects one dataclass instance and an optional callable factory.", span);
        }

        if (arguments[0] is not PyInstance instance || instance.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", "asdict() should be called on a dataclass instance.", span);
        }

        var dictFactory = arguments.Length >= 2 && !ReferenceEquals(arguments[1], PyNone.Instance)
            ? arguments[1] as LythonRuntime.ICallable ?? throw new LythonRuntimeException("TypeError", "asdict(..., dict_factory=...) expects a callable or None.", span)
            : null;

        return AsDictInner(instance, dictFactory, span, context);
    }

    public static object AsTuple(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.astuple(obj[, tuple_factory]) expects one dataclass instance and an optional callable factory.", span);
        }

        if (arguments[0] is not PyInstance instance || instance.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", "astuple() should be called on a dataclass instance.", span);
        }

        var tupleFactory = arguments.Length >= 2 && !ReferenceEquals(arguments[1], PyNone.Instance)
            ? arguments[1] as LythonRuntime.ICallable ?? throw new LythonRuntimeException("TypeError", "astuple(..., tuple_factory=...) expects a callable or None.", span)
            : null;

        return AsTupleInner(instance, tupleFactory, span, context);
    }

    public static int CompareOrderedInstances(PyInstance left, PyInstance right, LythonSourceSpan span)
    {
        if (!ReferenceEquals(left.Type, right.Type) || left.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", "Values are not comparable.", span);
        }

        foreach (var field in left.Type.DataclassComparableFields.RequireNotNull())
        {
            _ = left.TryGetOwnAttribute(field.Name, out var leftValue);
            _ = right.TryGetOwnAttribute(field.Name, out var rightValue);
            var result = PyComparison.Compare(leftValue ?? PyNone.Instance, rightValue ?? PyNone.Instance, span);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static PyType GetDataclassType(object value, LythonSourceSpan span, string owner)
    {
        return value switch
        {
            PyType type when type.DataclassFields is not null => type,
            PyInstance instance when instance.Type.DataclassFields is not null => instance.Type,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} must be called with a dataclass type or instance.", span)
        };
    }

    private static object AsDictInner(object value, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        return value switch
        {
            PyInstance instance when instance.Type.DataclassFields is not null => BuildDataclassDict(instance, dictFactory, span, context),
            PyList list => MapDataclassList(list, item => AsDictInner(item, dictFactory, span, context), context, span),
            PyTuple tuple => MapDataclassTuple(tuple, item => AsDictInner(item, dictFactory, span, context), context, span),
            PyDict dict => BuildMappedDict(dict, dictFactory, span, context),
            _ => value
        };
    }

    private static object AsTupleInner(object value, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        return value switch
        {
            PyInstance instance when instance.Type.DataclassFields is not null => BuildDataclassTuple(instance, tupleFactory, span, context),
            PyList list => MapDataclassList(list, item => AsTupleInner(item, tupleFactory, span, context), context, span),
            PyTuple tuple => MapDataclassTuple(tuple, item => AsTupleInner(item, tupleFactory, span, context), context, span),
            PyDict dict => BuildTupleMappedDict(dict, tupleFactory, span, context),
            _ => value
        };
    }

    private static PyDict BuildTupleMappedDict(PyDict dict, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in dict)
        {
            result.SetItem(
                LythonRuntime.RuntimeValue(AsTupleInner(pair.Key, tupleFactory, span, context)),
                LythonRuntime.RuntimeValue(AsTupleInner(pair.Value, tupleFactory, span, context)));
        }

        return result;
    }

    private static object BuildDictFromPairs(IEnumerable<(object Key, object Value)> pairs, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (dictFactory is null)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in pairs)
            {
                dict.SetItem(LythonRuntime.RuntimeValue(pair.Key), LythonRuntime.RuntimeValue(pair.Value));
            }

            return dict;
        }

        var items = new PyList([], context.MemoryGovernor, span);
        foreach (var pair in pairs)
        {
            items.Add(PyTuple.FromOwnedArray([LythonRuntime.RuntimeValue(pair.Key), LythonRuntime.RuntimeValue(pair.Value)], context.MemoryGovernor, span));
        }

        return dictFactory.Invoke([CallArgumentValue.Positional(items)], span, context);
    }

    private static object BuildTupleFromItems(IEnumerable<object> items, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (tupleFactory is null)
        {
            var materialized = MaterializeRuntimeValueItems(items);
            return new PyTuple(materialized, context.MemoryGovernor, span);
        }

        var list = new PyList(MaterializeRuntimeValueItems(items), context.MemoryGovernor, span);
        return tupleFactory.Invoke([CallArgumentValue.Positional(list)], span, context);
    }

    private static object BuildDataclassDict(PyInstance instance, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var visibleFields = instance.Type.DataclassHelperFields.RequireNotNull();
        var pairs = new (object Key, object Value)[visibleFields.Length];
        for (var i = 0; i < visibleFields.Length; i++)
        {
            var field = visibleFields[i];
            _ = instance.TryGetOwnAttribute(field.Name, out var fieldValue);
            pairs[i] = (PyString.FromString(field.Name), AsDictInner(fieldValue ?? PyNone.Instance, dictFactory, span, context));
        }

        return BuildDictFromPairs(pairs, dictFactory, span, context);
    }

    private static object BuildDataclassTuple(PyInstance instance, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var visibleFields = instance.Type.DataclassHelperFields.RequireNotNull();
        var items = new object[visibleFields.Length];
        for (var i = 0; i < visibleFields.Length; i++)
        {
            var field = visibleFields[i];
            _ = instance.TryGetOwnAttribute(field.Name, out var fieldValue);
            items[i] = AsTupleInner(fieldValue ?? PyNone.Instance, tupleFactory, span, context);
        }

        return BuildTupleFromItems(items, tupleFactory, span, context);
    }

    private static PyList MapDataclassList(PyList list, Func<object, object> map, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var items = new object[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            items[i] = LythonRuntime.RuntimeValue(map(list[i]));
        }

        return new PyList(items, context.MemoryGovernor, span);
    }

    private static PyTuple MapDataclassTuple(PyTuple tuple, Func<object, object> map, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var items = new object[tuple.Count];
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = LythonRuntime.RuntimeValue(map(tuple[i]));
        }

        return new PyTuple(items, context.MemoryGovernor, span);
    }

    private static object BuildMappedDict(PyDict dict, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var pairs = new (object Key, object Value)[dict.Count];
        var index = 0;
        foreach (var pair in dict)
        {
            pairs[index++] = (
                AsDictInner(pair.Key, dictFactory, span, context),
                AsDictInner(pair.Value, dictFactory, span, context));
        }

        return BuildDictFromPairs(pairs, dictFactory, span, context);
    }

    private static object[] MaterializeRuntimeValueItems(IEnumerable<object> items)
    {
        if (items is object[] array)
        {
            var normalized = new object[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                normalized[i] = LythonRuntime.RuntimeValue(array[i]);
            }

            return normalized;
        }

        var materialized = new List<object>();
        foreach (var item in items)
        {
            materialized.Add(LythonRuntime.RuntimeValue(item));
        }

        return [.. materialized];
    }

}
