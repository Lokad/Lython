using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal enum DataclassFieldKind
{
    Normal,
    InitVar,
    ClassVar,
}

internal enum DataclassHashMode
{
    Identity,
    Generated,
    Unhashable,
}

internal sealed record DataclassFieldSpec(
    string Name,
    ExpressionSyntax Annotation,
    DataclassFieldKind Kind,
    bool HasDefault,
    object DefaultValue,
    bool HasDefaultFactory,
    object DefaultFactory,
    bool Init,
    bool Repr,
    bool Compare,
    bool? Hash,
    bool KwOnly,
    object Metadata,
    bool Store);

internal sealed class PyDataclassFieldDefinition
{
    public required bool HasDefault { get; init; }

    public required object DefaultValue { get; init; }

    public required bool HasDefaultFactory { get; init; }

    public required object DefaultFactory { get; init; }

    public required bool Init { get; init; }

    public required bool Repr { get; init; }

    public required bool Compare { get; init; }

    public required bool? Hash { get; init; }

    public required bool? KwOnly { get; init; }

    public required object Metadata { get; init; }
}

internal sealed class PyDataclassFieldObject : IPyRenderableValue
{
    public PyDataclassFieldObject(DataclassFieldSpec field)
    {
        Name = field.Name;
        Annotation = field.Annotation;
        Default = field.HasDefault ? field.DefaultValue : PyDataclassMissing.Instance;
        DefaultFactory = field.HasDefaultFactory ? field.DefaultFactory : PyDataclassMissing.Instance;
        Init = field.Init;
        Repr = field.Repr;
        Compare = field.Compare;
        Hash = field.Hash is null ? PyNone.Instance : field.Hash.Value;
        KwOnly = field.KwOnly;
        Metadata = field.Metadata;
    }

    public string Name { get; }

    public ExpressionSyntax Annotation { get; }

    public object Default { get; }

    public object DefaultFactory { get; }

    public bool Init { get; }

    public bool Repr { get; }

    public bool Compare { get; }

    public object Hash { get; }

    public bool KwOnly { get; }

    public object Metadata { get; }

    public bool TryGetMember(string name, out object value)
    {
        switch (name)
        {
            case "name":
                value = PyString.FromString(Name);
                return true;
            case "default":
                value = Default;
                return true;
            case "default_factory":
                value = DefaultFactory;
                return true;
            case "init":
                value = Init;
                return true;
            case "repr":
                value = Repr;
                return true;
            case "compare":
                value = Compare;
                return true;
            case "hash":
                value = Hash;
                return true;
            case "kw_only":
                value = KwOnly;
                return true;
            case "metadata":
                value = Metadata;
                return true;
            default:
                value = PyNone.Instance;
                return false;
        }
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString($"Field(name='{Name}')");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassParamsObject : IPyRenderableValue
{
    public PyDataclassParamsObject(bool init, bool repr, bool eq, bool order, bool unsafeHash, bool frozen, bool kwOnly, bool matchArgs)
    {
        Init = init;
        Repr = repr;
        Eq = eq;
        Order = order;
        UnsafeHash = unsafeHash;
        Frozen = frozen;
        KwOnly = kwOnly;
        MatchArgs = matchArgs;
    }

    public bool Init { get; }

    public bool Repr { get; }

    public bool Eq { get; }

    public bool Order { get; }

    public bool UnsafeHash { get; }

    public bool Frozen { get; }

    public bool KwOnly { get; }

    public bool MatchArgs { get; }

    public bool TryGetMember(string name, out object value)
    {
        value = name switch
        {
            "init" => Init,
            "repr" => Repr,
            "eq" => Eq,
            "order" => Order,
            "unsafe_hash" => UnsafeHash,
            "frozen" => Frozen,
            "kw_only" => KwOnly,
            "match_args" => MatchArgs,
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString($"_DataclassParams(init={Init}, repr={Repr}, eq={Eq}, order={Order}, unsafe_hash={UnsafeHash}, frozen={Frozen}, kw_only={KwOnly}, match_args={MatchArgs})");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassMissing : IPyRenderableValue
{
    public static readonly PyDataclassMissing Instance = new();

    private PyDataclassMissing()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("MISSING");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassKwOnlyMarker : IPyRenderableValue
{
    public static readonly PyDataclassKwOnlyMarker Instance = new();

    private PyDataclassKwOnlyMarker()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("KW_ONLY");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassInitVarMarker : IPyRenderableValue
{
    public static readonly PyDataclassInitVarMarker Instance = new();

    private PyDataclassInitVarMarker()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("InitVar");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal static class PyDataclass
{
    private static readonly object DefaultFactorySentinel = new();
    public static readonly LythonRuntime.ICallable FieldCallable = new DataclassFieldCallable();
    public static readonly LythonRuntime.ICallable ReplaceCallable = new DataclassReplaceCallable();

    public static void Apply(PyType type, ClassDefinitionStatementSyntax syntax, Dictionary<string, object> members, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var decorator = syntax.DataclassDecorator;
        if (decorator is null)
        {
            return;
        }

        var fields = CollectFields(type, syntax, members, context, span);
        ValidateFieldOrdering(fields, span);

        ValidateDataclassOptions(type, decorator, span);

        type.SetDataclassMetadata(fields, decorator.Repr, decorator.Eq, decorator.Order, DetermineHashMode(type, decorator));
        type.TrySetMember("__dataclass_params__", new PyDataclassParamsObject(decorator.Init, decorator.Repr, decorator.Eq, decorator.Order, decorator.UnsafeHash, decorator.Frozen, decorator.KwOnly, decorator.MatchArgs));
        type.TrySetMember("__dataclass_fields__", BuildFieldMap(fields, context, span));

        if (decorator.Init && !type.TryGetOwnMember("__init__", out _))
        {
            type.TrySetMember("__init__", new DataclassInitMethod(type.Name, fields));
        }

        if (decorator.Repr && !type.TryGetOwnMember("__repr__", out _))
        {
            type.TrySetMember("__repr__", new DataclassReprMethod(type.Name, fields.Where(field => field.Repr).ToArray()));
        }

        if (decorator.Eq && !type.TryGetOwnMember("__eq__", out _))
        {
            type.TrySetMember("__eq__", new DataclassEqMethod(type.Name, fields.Where(field => field.Compare).ToArray()));
        }

        if (decorator.Order)
        {
            type.TrySetMember("__lt__", new DataclassOrderMethod(type.Name, DataclassOrderOperation.Less));
            type.TrySetMember("__le__", new DataclassOrderMethod(type.Name, DataclassOrderOperation.LessEqual));
            type.TrySetMember("__gt__", new DataclassOrderMethod(type.Name, DataclassOrderOperation.Greater));
            type.TrySetMember("__ge__", new DataclassOrderMethod(type.Name, DataclassOrderOperation.GreaterEqual));
        }

        if (decorator.Frozen)
        {
            type.TrySetMember("__setattr__", new DataclassFrozenSetAttrMethod(type.Name));
            type.TrySetMember("__delattr__", new DataclassFrozenDelAttrMethod(type.Name));
        }

        if (type.DataclassHashMode == DataclassHashMode.Generated && !type.TryGetOwnMember("__hash__", out _))
        {
            type.TrySetMember("__hash__", new DataclassHashMethod(type.Name));
        }

        if (decorator.MatchArgs && !type.TryGetOwnMember("__match_args__", out _))
        {
            type.TrySetMember(
                "__match_args__",
                new PyTuple(fields
                    .Where(field => field.Kind == DataclassFieldKind.Normal && field.Init && !field.KwOnly)
                    .Select(field => (object)PyString.FromString(field.Name)), context.MemoryGovernor, span));
        }
    }

    public static object CreateField(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        object defaultValue = PyDataclassMissing.Instance;
        object defaultFactory = PyDataclassMissing.Instance;
        var init = true;
        var repr = true;
        var compare = true;
        bool? hash = null;
        bool? kwOnly = null;
        object metadata = new PyDict(context.MemoryGovernor, span);

        if (arguments.Length >= 1)
        {
            defaultValue = arguments[0];
        }

        if (arguments.Length >= 2)
        {
            defaultFactory = ReferenceEquals(arguments[1], PyNone.Instance)
                ? PyDataclassMissing.Instance
                : arguments[1];
        }

        if (arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance))
        {
            init = ExpectBool(arguments[2], "field(init=...)", span);
        }

        if (arguments.Length >= 4 && !ReferenceEquals(arguments[3], PyNone.Instance))
        {
            repr = ExpectBool(arguments[3], "field(repr=...)", span);
        }

        if (arguments.Length >= 5 && !ReferenceEquals(arguments[4], PyNone.Instance))
        {
            compare = ExpectBool(arguments[4], "field(compare=...)", span);
        }

        if (arguments.Length >= 6 && !ReferenceEquals(arguments[5], PyNone.Instance))
        {
            hash = ExpectOptionalBool(arguments[5], "field(hash=...)", span);
        }

        if (arguments.Length >= 7 && !ReferenceEquals(arguments[6], PyNone.Instance))
        {
            kwOnly = ExpectOptionalBool(arguments[6], "field(kw_only=...)", span);
        }

        if (arguments.Length >= 8 && !ReferenceEquals(arguments[7], PyNone.Instance))
        {
            metadata = NormalizeFieldMetadata(arguments[7], span, context);
        }

        if (!ReferenceEquals(defaultValue, PyDataclassMissing.Instance) &&
            !ReferenceEquals(defaultFactory, PyDataclassMissing.Instance))
        {
            throw new LythonRuntimeException("ValueError", "dataclasses.field() cannot specify both default and default_factory.", span);
        }

        if (!ReferenceEquals(defaultFactory, PyDataclassMissing.Instance) &&
            defaultFactory is not LythonRuntime.ICallable)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.field(default_factory=...) expects a callable.", span);
        }

        return new PyDataclassFieldDefinition
        {
            HasDefault = !ReferenceEquals(defaultValue, PyDataclassMissing.Instance),
            DefaultValue = defaultValue,
            HasDefaultFactory = !ReferenceEquals(defaultFactory, PyDataclassMissing.Instance),
            DefaultFactory = defaultFactory,
            Init = init,
            Repr = repr,
            Compare = compare,
            Hash = hash,
            KwOnly = kwOnly,
            Metadata = metadata
        };
    }

    private sealed class DataclassFieldCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var seenDefault = false;
            var seenDefaultFactory = false;
            var seenInit = false;
            var seenRepr = false;
            var seenCompare = false;
            var seenHash = false;
            var seenKwOnly = false;
            var seenMetadata = false;

            object defaultValue = PyDataclassMissing.Instance;
            object defaultFactory = PyDataclassMissing.Instance;
            var init = true;
            var repr = true;
            var compare = true;
            bool? hash = null;
            bool? kwOnly = null;
            object metadata = new PyDict(context.MemoryGovernor, span);

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.field(...) only supports keyword arguments in Lython.", span);
                }

                switch (argument.Name)
                {
                    case "default":
                        if (seenDefault)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "default", span);
                        }

                        defaultValue = argument.Value;
                        seenDefault = true;
                        break;
                    case "default_factory":
                        if (seenDefaultFactory)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "default_factory", span);
                        }

                        defaultFactory = argument.Value;
                        seenDefaultFactory = true;
                        break;
                    case "init":
                        if (seenInit)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "init", span);
                        }

                        init = ExpectBool(argument.Value, "field(init=...)", span);
                        seenInit = true;
                        break;
                    case "repr":
                        if (seenRepr)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "repr", span);
                        }

                        repr = ExpectBool(argument.Value, "field(repr=...)", span);
                        seenRepr = true;
                        break;
                    case "compare":
                        if (seenCompare)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "compare", span);
                        }

                        compare = ExpectBool(argument.Value, "field(compare=...)", span);
                        seenCompare = true;
                        break;
                    case "hash":
                        if (seenHash)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "hash", span);
                        }

                        hash = ExpectOptionalBool(argument.Value, "field(hash=...)", span);
                        seenHash = true;
                        break;
                    case "kw_only":
                        if (seenKwOnly)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "kw_only", span);
                        }

                        kwOnly = ExpectOptionalBool(argument.Value, "field(kw_only=...)", span);
                        seenKwOnly = true;
                        break;
                    case "metadata":
                        if (seenMetadata)
                        {
                            throw CallErrors.MultipleValues("Builtin", "dataclasses.field", "metadata", span);
                        }

                        metadata = NormalizeFieldMetadata(argument.Value, span, context);
                        seenMetadata = true;
                        break;
                    default:
                        throw CallErrors.UnexpectedKeyword("Builtin", "dataclasses.field", argument.Name, span);
                }
            }

            return CreateField(
                [
                    seenDefault ? defaultValue : PyDataclassMissing.Instance,
                    seenDefaultFactory ? defaultFactory : PyDataclassMissing.Instance,
                    seenInit ? init : PyNone.Instance,
                    seenRepr ? repr : PyNone.Instance,
                    seenCompare ? compare : PyNone.Instance,
                    seenHash ? (object?)hash ?? PyNone.Instance : PyNone.Instance,
                    seenKwOnly ? (object?)kwOnly ?? PyNone.Instance : PyNone.Instance,
                    metadata
                ],
                span,
                context);
        }
    }

    private sealed class DataclassReplaceCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance || instance.Type.DataclassFields is null)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.replace(obj, **changes) expects a dataclass instance as its first positional argument.", span);
            }

            var changes = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var i = 1; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.replace(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
                }

                if (!changes.TryAdd(argument.Name, argument.Value))
                {
                    throw CallErrors.MultipleValues("Builtin", "dataclasses.replace", argument.Name, span);
                }
            }

            foreach (var change in changes.Keys)
            {
                var field = instance.Type.DataclassFields.FirstOrDefault(candidate => candidate.Name == change);
                if (field is null)
                {
                    throw new LythonRuntimeException("TypeError", $"dataclasses.replace() got an unexpected field '{change}'.", span);
                }

                if (!field.Init)
                {
                    throw new LythonRuntimeException("TypeError", $"dataclasses.replace() cannot override init=False field '{change}'.", span);
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
                    callArguments.Add(new CallArgumentValue(field.Name, changedValue));
                    continue;
                }

                if (field.Kind == DataclassFieldKind.InitVar)
                {
                    if (field.HasDefaultFactory)
                    {
                        callArguments.Add(new CallArgumentValue(field.Name, DataclassInitMethod.InvokeDefaultFactory(field, span, context)));
                        continue;
                    }

                    if (field.HasDefault)
                    {
                        callArguments.Add(new CallArgumentValue(field.Name, field.DefaultValue));
                        continue;
                    }

                    throw new LythonRuntimeException("ValueError", $"InitVar '{field.Name}' must be specified with dataclasses.replace().", span);
                }

                _ = instance.TryGetOwnAttribute(field.Name, out var existingValue);
                callArguments.Add(new CallArgumentValue(field.Name, existingValue ?? PyNone.Instance));
            }

            return instance.Type.Invoke(callArguments.ToArray(), span, context);
        }
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
        var visibleFields = GetHelperVisibleFields(type.DataclassFields!).ToArray();
        var items = new object[visibleFields.Length];
        for (var i = 0; i < visibleFields.Length; i++)
        {
            items[i] = new PyDataclassFieldObject(visibleFields[i]);
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

        foreach (var field in left.Type.DataclassFields.Where(field => field.Compare))
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
            PyDict dict => BuildMappedDict(dict, pairValue => AsDictInner(pairValue, dictFactory, span, context), dictFactory, span, context),
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

    private static object BuildDictFromPairs(IEnumerable<(string Key, object Value)> pairs, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (dictFactory is null)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in pairs)
            {
                dict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return dict;
        }

        var items = new PyList([], context.MemoryGovernor, span);
        foreach (var pair in pairs)
        {
            items.Add(new PyTuple([PyString.FromString(pair.Key), pair.Value], context.MemoryGovernor, span));
        }

        return dictFactory.Invoke([new CallArgumentValue(null, items)], span, context);
    }

    private static object BuildTupleFromItems(IEnumerable<object> items, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (tupleFactory is null)
        {
            var materialized = MaterializeRuntimeValueItems(items);
            return new PyTuple(materialized, context.MemoryGovernor, span);
        }

        var list = new PyList(MaterializeRuntimeValueItems(items), context.MemoryGovernor, span);
        return tupleFactory.Invoke([new CallArgumentValue(null, list)], span, context);
    }

    private static object BuildDataclassDict(PyInstance instance, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var visibleFields = GetHelperVisibleFields(instance.Type.DataclassFields!).ToArray();
        var pairs = new (string Key, object Value)[visibleFields.Length];
        for (var i = 0; i < visibleFields.Length; i++)
        {
            var field = visibleFields[i];
            _ = instance.TryGetOwnAttribute(field.Name, out var fieldValue);
            pairs[i] = (field.Name, AsDictInner(fieldValue ?? PyNone.Instance, dictFactory, span, context));
        }

        return BuildDictFromPairs(pairs, dictFactory, span, context);
    }

    private static object BuildDataclassTuple(PyInstance instance, LythonRuntime.ICallable? tupleFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var visibleFields = GetHelperVisibleFields(instance.Type.DataclassFields!).ToArray();
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

    private static object BuildMappedDict(PyDict dict, Func<object, object> mapValue, LythonRuntime.ICallable? dictFactory, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var pairs = new (string Key, object Value)[dict.Count];
        var index = 0;
        foreach (var pair in dict)
        {
            pairs[index++] = (ToPythonStringKey(pair.Key, span), mapValue(pair.Value));
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

    private static string ToPythonStringKey(object key, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(key, out var text))
        {
            return text.AsString();
        }

        throw new LythonRuntimeException("TypeError", "asdict() only supports dictionaries with string keys inside dataclass values.", span);
    }

    private static PyDict BuildFieldMap(IReadOnlyList<DataclassFieldSpec> fields, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var dict = new PyDict(context.MemoryGovernor, span);
        foreach (var field in fields)
        {
            dict.SetItem(PyString.FromString(field.Name), new PyDataclassFieldObject(field));
        }

        return dict;
    }

    private static DataclassFieldSpec[] CollectFields(PyType type, ClassDefinitionStatementSyntax syntax, Dictionary<string, object> members, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var fields = new List<DataclassFieldSpec>();
        var defaultKwOnly = syntax.DataclassDecorator!.KwOnly;

        foreach (var statement in syntax.Body.OfType<AnnotatedAssignmentStatementSyntax>())
        {
            if (IsKwOnlyMarker(statement.Annotation))
            {
                defaultKwOnly = true;
                type.RemoveOwnMember(statement.Name);
                continue;
            }

            var kind = ClassifyFieldKind(statement.Annotation);

            var fieldDefinition = members.TryGetValue(statement.Name, out var rawMember) && rawMember is PyDataclassFieldDefinition definition
                ? definition
                : null;

            var hasDefault = fieldDefinition?.HasDefault ?? (statement.Expression is not null);
            var classMemberValue = fieldDefinition?.DefaultValue ?? (statement.Expression is not null && members.TryGetValue(statement.Name, out var value) ? value : PyNone.Instance);
            var defaultValue = classMemberValue;
            var hasDefaultFactory = fieldDefinition?.HasDefaultFactory ?? false;
            var defaultFactory = fieldDefinition?.DefaultFactory ?? PyNone.Instance;
            var init = fieldDefinition?.Init ?? true;
            var repr = fieldDefinition?.Repr ?? true;
            var compare = fieldDefinition?.Compare ?? true;
            var hash = fieldDefinition?.Hash;
            var kwOnly = fieldDefinition?.KwOnly ?? defaultKwOnly;
            var metadata = fieldDefinition?.Metadata ?? new PyDict(context.MemoryGovernor, span);

            if (kind == DataclassFieldKind.ClassVar)
            {
                if (fieldDefinition?.KwOnly is not null)
                {
                    throw new LythonRuntimeException("TypeError", $"field '{statement.Name}' is a ClassVar but specifies kw_only.", span);
                }

                init = false;
                repr = false;
                compare = false;
                hash = false;
                kwOnly = false;
            }
            else if (kind == DataclassFieldKind.InitVar)
            {
                repr = false;
                compare = false;
                hash = false;
            }

            if (hasDefault && kind != DataclassFieldKind.ClassVar)
            {
                defaultValue = ResolveDescriptorBackedDefault(type, defaultValue, context, span);
            }

            fields.Add(new DataclassFieldSpec(
                statement.Name,
                statement.Annotation,
                kind,
                hasDefault,
                defaultValue,
                hasDefaultFactory,
                defaultFactory,
                init,
                repr,
                compare,
                hash,
                kwOnly,
                metadata,
                Store: kind == DataclassFieldKind.Normal));

            if (fieldDefinition is null)
            {
                continue;
            }

            if (hasDefault)
            {
                type.TrySetMember(statement.Name, classMemberValue);
            }
            else
            {
                type.RemoveOwnMember(statement.Name);
            }
        }

        return fields.ToArray();
    }

    private static void ValidateFieldOrdering(IReadOnlyList<DataclassFieldSpec> fields, LythonSourceSpan span)
    {
        var seenDefault = false;
        foreach (var field in fields.Where(field => field.Kind is not DataclassFieldKind.ClassVar && field.Init && !field.KwOnly))
        {
            var hasAnyDefault = field.HasDefault || field.HasDefaultFactory;
            if (!hasAnyDefault && seenDefault)
            {
                throw new LythonRuntimeException("TypeError", $"Dataclass field '{field.Name}' without a default cannot follow a field with a default.", span);
            }

            seenDefault |= hasAnyDefault;
        }
    }

    private static void ValidateDataclassOptions(PyType type, DataclassDecoratorSyntax decorator, LythonSourceSpan span)
    {
        if (decorator.Order && !decorator.Eq)
        {
            throw new LythonRuntimeException("TypeError", "@dataclass(order=True) requires eq=True.", span);
        }

        if (decorator.UnsafeHash && type.TryGetOwnMember("__hash__", out _))
        {
            throw new LythonRuntimeException("TypeError", "@dataclass(unsafe_hash=True) cannot be combined with an explicit __hash__.", span);
        }

        if (decorator.Order &&
            (type.TryGetOwnMember("__lt__", out _) ||
             type.TryGetOwnMember("__le__", out _) ||
             type.TryGetOwnMember("__gt__", out _) ||
             type.TryGetOwnMember("__ge__", out _)))
        {
            throw new LythonRuntimeException("TypeError", "@dataclass(order=True) cannot be combined with explicit comparison methods.", span);
        }

        if (decorator.Frozen &&
            (type.TryGetOwnMember("__setattr__", out _) ||
             type.TryGetOwnMember("__delattr__", out _)))
        {
            throw new LythonRuntimeException("TypeError", "@dataclass(frozen=True) cannot be combined with explicit __setattr__ or __delattr__.", span);
        }
    }

    private static bool IsKwOnlyMarker(ExpressionSyntax annotation)
    {
        return annotation switch
        {
            IdentifierExpressionSyntax { Name: "KW_ONLY" } => true,
            MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                MemberName: "KW_ONLY"
            } => true,
            _ => false
        };
    }

    private static DataclassFieldKind ClassifyFieldKind(ExpressionSyntax annotation)
    {
        if (IsInitVarAnnotation(annotation))
        {
            return DataclassFieldKind.InitVar;
        }

        if (IsClassVarAnnotation(annotation))
        {
            return DataclassFieldKind.ClassVar;
        }

        return DataclassFieldKind.Normal;
    }

    private static bool IsInitVarAnnotation(ExpressionSyntax annotation)
        => annotation switch
        {
            SubscriptExpressionSyntax { Target: var target } => IsInitVarTarget(target),
            _ => false
        };

    private static bool IsInitVarTarget(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax { Name: "InitVar" } => true,
            MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                MemberName: "InitVar"
            } => true,
            _ => false
        };

    private static bool IsClassVarAnnotation(ExpressionSyntax annotation)
        => annotation switch
        {
            SubscriptExpressionSyntax { Target: var target } => IsClassVarTarget(target),
            _ => false
        };

    private static bool IsClassVarTarget(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax { Name: "ClassVar" } => true,
            MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "typing" },
                MemberName: "ClassVar"
            } => true,
            _ => false
        };

    private static object NormalizeFieldMetadata(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (ReferenceEquals(value, PyNone.Instance))
        {
            return new PyDict(context.MemoryGovernor, span);
        }

        if (value is not PyDict dict)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.field(metadata=...) expects a dict or None in Lython.", span);
        }

        var copy = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in dict)
        {
            copy.SetItem(pair.Key, pair.Value);
        }

        return copy;
    }

    private static DataclassHashMode DetermineHashMode(PyType type, DataclassDecoratorSyntax decorator)
    {
        if (decorator.UnsafeHash)
        {
            return DataclassHashMode.Generated;
        }

        if (decorator.Eq && decorator.Frozen)
        {
            return DataclassHashMode.Generated;
        }

        if (decorator.Eq)
        {
            return DataclassHashMode.Unhashable;
        }

        return DataclassHashMode.Identity;
    }

    private static object ResolveDescriptorBackedDefault(PyType type, object defaultValue, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return defaultValue switch
        {
            IPyDescriptor descriptor => descriptor.Get(null, type, context, span),
            PyInstance descriptorInstance when TryResolveDynamicDescriptorDefault(descriptorInstance, type, context, span, out var value) => value,
            _ => defaultValue
        };
    }

    private static bool TryResolveDynamicDescriptorDefault(PyInstance descriptorInstance, PyType owner, LythonRuntime.ExecutionContext context, LythonSourceSpan span, out object value)
    {
        if (descriptorInstance.Type.TryLookupInMro("__get__", 0, out var rawMethod, out _))
        {
            var bound = rawMethod is IPyDescriptor descriptor
                ? descriptor.Get(descriptorInstance, descriptorInstance.Type, context, span)
                : rawMethod;
            if (bound is not LythonRuntime.ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "Descriptor method '__get__' must be callable.", span);
            }

            value = callable.Invoke(
                [
                    new CallArgumentValue(null, PyNone.Instance),
                    new CallArgumentValue(null, owner)
                ],
                span,
                context);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    private static IEnumerable<DataclassFieldSpec> GetHelperVisibleFields(IEnumerable<DataclassFieldSpec> fields)
        => fields.Where(field => field.Kind == DataclassFieldKind.Normal);

    internal static bool ShouldIncludeInGeneratedHash(DataclassFieldSpec field)
    {
        if (field.Kind != DataclassFieldKind.Normal)
        {
            return false;
        }

        return field.Hash ?? field.Compare;
    }

    private static void SetAttributeDuringDataclassInit(PyInstance instance, string name, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (instance.Type.TryLookupInMro(name, 0, out var rawValue, out _) &&
            PyAttributeLookup.TrySetDescriptorValue(rawValue, instance, value, context, span))
        {
            return;
        }

        instance.SetAttribute(name, value);
    }

    private static bool ExpectBool(object value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            bool boolean => boolean,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects a bool.", span)
        };
    }

    private static bool? ExpectOptionalBool(object value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            PyNone => null,
            bool boolean => boolean,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects a bool or None.", span)
        };
    }

    private sealed class DataclassInitMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var parameters = new List<LoweredFunctionParameter>(fields.Count + 1)
            {
                new("self", FunctionParameterKind.Positional, null, null)
            };
            foreach (var field in fields)
            {
                if (field.Kind == DataclassFieldKind.ClassVar || !field.Init)
                {
                    continue;
                }

                parameters.Add(new LoweredFunctionParameter(
                    field.Name,
                    field.KwOnly ? FunctionParameterKind.KeywordOnly : FunctionParameterKind.Positional,
                    null,
                    null));
            }

            var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field.Kind == DataclassFieldKind.ClassVar || !field.Init)
                {
                    continue;
                }

                if (field.HasDefaultFactory)
                {
                    defaults[field.Name] = DefaultFactorySentinel;
                }
                else if (field.HasDefault)
                {
                    defaults[field.Name] = field.DefaultValue;
                }
            }

            var bound = LythonRuntime.BindFunctionArguments(arguments, span, $"{typeName}.__init__", "Function", parameters, defaults, context);
            if (bound["self"] is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__init__ expected a bound instance.", span);
            }

            var initVarValues = new List<object>();
            foreach (var field in fields)
            {
                if (!field.Store)
                {
                    if (field.Kind == DataclassFieldKind.InitVar && field.Init)
                    {
                        var initVarValue = bound[field.Name];
                        if (ReferenceEquals(initVarValue, DefaultFactorySentinel))
                        {
                            initVarValue = InvokeDefaultFactory(field, span, context);
                        }

                        initVarValues.Add(initVarValue);
                    }
                    continue;
                }

                object value;
                if (field.Init)
                {
                    value = bound[field.Name];
                    if (ReferenceEquals(value, DefaultFactorySentinel))
                    {
                        value = InvokeDefaultFactory(field, span, context);
                    }
                }
                else if (field.HasDefaultFactory)
                {
                    value = InvokeDefaultFactory(field, span, context);
                }
                else if (field.HasDefault)
                {
                    value = field.DefaultValue;
                }
                else
                {
                    continue;
                }

                SetAttributeDuringDataclassInit(instance, field.Name, value, context, span);
            }

            if (instance.Type.TryGetMember("__post_init__", out var postInitRaw))
            {
                var callable = postInitRaw switch
                {
                    IPyBindableCallable bindable => bindable.Bind(instance),
                    IPyDescriptor descriptor => descriptor.Get(instance, instance.Type, context, span),
                    _ => postInitRaw
                };
                if (callable is not LythonRuntime.ICallable postInitCallable)
                {
                    throw new LythonRuntimeException("TypeError", $"{typeName}.__post_init__ must be callable.", span);
                }

                var postInitArguments = initVarValues.Select(value => new CallArgumentValue(null, value)).ToArray();
                _ = postInitCallable.Invoke(postInitArguments, span, context);
            }

            return PyNone.Instance;
        }

        internal static object InvokeDefaultFactory(DataclassFieldSpec field, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (field.DefaultFactory is not LythonRuntime.ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", $"Dataclass field '{field.Name}' has a non-callable default_factory.", span);
            }

            return callable.Invoke([], span, context);
        }
    }

    private sealed class DataclassReprMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__repr__() expected a bound instance.", span);
            }

            var builder = new Utf8ValueBuilder();
            builder.AppendString(typeName);
            builder.AppendAscii("(");
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendAscii(", ");
                }

                var field = fields[i];
                builder.AppendString(field.Name);
                builder.AppendAscii("=");
                _ = instance.TryGetOwnAttribute(field.Name, out var value);
                builder.Append(PyRendering.ToPythonPyString(value ?? PyNone.Instance, new PyRenderingContext(context)));
            }

            builder.AppendAscii(")");
            return builder.ToPyString();
        }
    }

    private sealed class DataclassEqMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var parameters = new[]
            {
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("other", FunctionParameterKind.Positional, null, null)
            };
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, $"{typeName}.__eq__", "Function", parameters, new Dictionary<string, object>(StringComparer.Ordinal), context);
            if (bound["self"] is not PyInstance self)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__eq__ expected a bound instance.", span);
            }

            if (bound["other"] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
            {
                return false;
            }

            foreach (var field in fields)
            {
                _ = self.TryGetOwnAttribute(field.Name, out var left);
                _ = other.TryGetOwnAttribute(field.Name, out var right);
                if (!PyEquality.AreEqual(left ?? PyNone.Instance, right ?? PyNone.Instance))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private enum DataclassOrderOperation
    {
        Less,
        LessEqual,
        Greater,
        GreaterEqual
    }

    private sealed class DataclassOrderMethod(string typeName, DataclassOrderOperation operation) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var parameters = new[]
            {
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("other", FunctionParameterKind.Positional, null, null)
            };
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, $"{typeName}.__{OperationName(operation)}__", "Function", parameters, new Dictionary<string, object>(StringComparer.Ordinal), context);
            if (bound["self"] is not PyInstance self || bound["other"] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
            {
                throw new LythonRuntimeException("TypeError", $"{typeName} ordering expects two instances of the same dataclass type.", span);
            }

            var comparison = CompareOrderedInstances(self, other, span);
            return operation switch
            {
                DataclassOrderOperation.Less => comparison < 0,
                DataclassOrderOperation.LessEqual => comparison <= 0,
                DataclassOrderOperation.Greater => comparison > 0,
                DataclassOrderOperation.GreaterEqual => comparison >= 0,
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
        }

        private static string OperationName(DataclassOrderOperation operation)
            => operation switch
            {
                DataclassOrderOperation.Less => "lt",
                DataclassOrderOperation.LessEqual => "le",
                DataclassOrderOperation.Greater => "gt",
                DataclassOrderOperation.GreaterEqual => "ge",
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
    }

    private sealed class DataclassHashMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__hash__() expected a bound instance.", span);
            }

            return new BigInteger(instance.GetPyHashCode());
        }
    }

    private sealed class DataclassFrozenSetAttrMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            _ = arguments;
            throw new LythonRuntimeException("FrozenInstanceError", $"cannot assign to field of frozen dataclass '{typeName}'.", span);
        }
    }

    private sealed class DataclassFrozenDelAttrMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            _ = arguments;
            throw new LythonRuntimeException("FrozenInstanceError", $"cannot delete field of frozen dataclass '{typeName}'.", span);
        }
    }
}
