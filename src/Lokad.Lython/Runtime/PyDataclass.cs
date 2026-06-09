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
    object Annotation,
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

    public object Annotation { get; }

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
            case "type":
                value = Annotation;
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

internal sealed class PyDataclassAnnotationValue(ExpressionSyntax expression) : IPyRenderableValue
{
    public ExpressionSyntax Expression { get; } = expression;

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(Format(Expression));
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => Format(Expression);

    private static string Format(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax identifier => identifier.Name,
            MemberExpressionSyntax member => $"{Format(member.Target)}.{member.MemberName}",
            SubscriptExpressionSyntax subscript => $"{Format(subscript.Target)}[{Format(subscript.Index)}]",
            TupleLiteralExpressionSyntax tuple => string.Join(", ", tuple.Items.Select(Format)),
            StringLiteralExpressionSyntax text => $"'{text.Value}'",
            IntegerLiteralExpressionSyntax integer => integer.ValueText,
            FloatLiteralExpressionSyntax floating => floating.ValueText,
            BooleanLiteralExpressionSyntax boolean => boolean.Value ? "True" : "False",
            NoneLiteralExpressionSyntax => "None",
            ParenthesizedExpressionSyntax parenthesized => $"({Format(parenthesized.Inner)})",
            _ => "<annotation>"
        };
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
    public static readonly LythonRuntime.ICallable DataclassCallable = new DataclassCallableImpl();
    public static readonly LythonRuntime.ICallable FieldCallable = new DataclassFieldCallable();
    public static readonly LythonRuntime.ICallable FieldType = new DataclassFieldType();
    public static readonly LythonRuntime.ICallable MakeDataclassCallable = new DataclassMakeDataclassCallable();
    public static readonly LythonRuntime.ICallable ReplaceCallable = new DataclassReplaceCallable();

    public static PyDataclassAnnotationValue CreateAnnotationValue(ExpressionSyntax annotation)
        => new(annotation);

    public static void Apply(PyType type, ClassDefinitionStatementSyntax syntax, Dictionary<string, object> members, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var decorator = syntax.DataclassDecorator;
        if (decorator is null)
        {
            return;
        }

        var fields = MergeInheritedFields(type, CollectFields(type, syntax, members, context, span));
        ValidateFieldOrdering(fields, span);
        ApplyCore(type, decorator, fields, context, span);
    }

    public static PyType ApplyRuntime(PyType type, DataclassDecoratorSyntax decorator, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var fields = MergeInheritedFields(type, CollectRuntimeFields(type, decorator.KwOnly, context, span));
        ValidateFieldOrdering(fields, span);
        ApplyCore(type, decorator, fields, context, span);
        return type;
    }

    private static void ApplyCore(PyType type, DataclassDecoratorSyntax decorator, IReadOnlyList<DataclassFieldSpec> fields, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
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

    private sealed record DataclassOptions(
        bool Init = true,
        bool Repr = true,
        bool Eq = true,
        bool Order = false,
        bool UnsafeHash = false,
        bool Frozen = false,
        bool KwOnly = false,
        bool MatchArgs = true);

    private static DataclassDecoratorSyntax ToDecorator(DataclassOptions options, LythonSourceSpan span)
        => new(options.Init, options.Repr, options.Eq, options.Order, options.UnsafeHash, options.Frozen, options.KwOnly, options.MatchArgs, span);

    private sealed class DataclassCallableImpl : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var options = new DataclassOptions();
            PyType? cls = null;
            var seenCls = false;
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (seenCls)
                    {
                        throw new LythonRuntimeException("TypeError", "dataclasses.dataclass() accepts at most one positional class argument.", span);
                    }

                    seenCls = true;
                    if (!ReferenceEquals(argument.Value, PyNone.Instance))
                    {
                        cls = argument.Value as PyType ??
                            throw new LythonRuntimeException("TypeError", "dataclasses.dataclass(cls=...) expects a class or None.", span);
                    }

                    continue;
                }

                if (argument.Name == "cls")
                {
                    if (seenCls)
                    {
                        throw CallErrors.MultipleValues("Builtin", "dataclasses.dataclass", "cls", span);
                    }

                    seenCls = true;
                    if (!ReferenceEquals(argument.Value, PyNone.Instance))
                    {
                        cls = argument.Value as PyType ??
                            throw new LythonRuntimeException("TypeError", "dataclasses.dataclass(cls=...) expects a class or None.", span);
                    }

                    continue;
                }

                options = ApplyDataclassOption(options, argument.Name, argument.Value, seenOptions, "dataclasses.dataclass", span);
            }

            var decorator = ToDecorator(options, span);
            return cls is null
                ? new DataclassRuntimeDecorator(decorator)
                : ApplyRuntime(cls, decorator, context, span);
        }
    }

    private sealed class DataclassRuntimeDecorator(DataclassDecoratorSyntax decorator) : LythonRuntime.ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.dataclass(...) decorator expects one class argument.", span);
            }

            return ApplyRuntime(type, decorator, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<function dataclasses.dataclass.<locals>.wrap>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class DataclassFieldType : LythonRuntime.ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("TypeError", "dataclasses.Field objects are created by dataclasses.fields().", span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'dataclasses.Field'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class DataclassMakeDataclassCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            object? clsName = null;
            object? fieldsArgument = null;
            object? basesArgument = null;
            object? namespaceArgument = null;
            var options = new DataclassOptions();
            var positionalCount = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    positionalCount++;
                    switch (positionalCount)
                    {
                        case 1:
                            clsName = argument.Value;
                            break;
                        case 2:
                            fieldsArgument = argument.Value;
                            break;
                        default:
                            throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(cls_name, fields, ...) accepts exactly two positional arguments.", span);
                    }

                    continue;
                }

                switch (argument.Name)
                {
                    case "cls_name":
                        SetSingle(ref clsName, argument.Value, seen, argument.Name, "dataclasses.make_dataclass", span);
                        break;
                    case "fields":
                        SetSingle(ref fieldsArgument, argument.Value, seen, argument.Name, "dataclasses.make_dataclass", span);
                        break;
                    case "bases":
                        SetSingle(ref basesArgument, argument.Value, seen, argument.Name, "dataclasses.make_dataclass", span);
                        break;
                    case "namespace":
                        SetSingle(ref namespaceArgument, argument.Value, seen, argument.Name, "dataclasses.make_dataclass", span);
                        break;
                    case "module":
                    case "decorator":
                        throw new LythonRuntimeException("NotImplementedError", $"dataclasses.make_dataclass({argument.Name}=...) is not supported by Lython.", span);
                    default:
                        options = ApplyDataclassOption(options, argument.Name, argument.Value, seenOptions, "dataclasses.make_dataclass", span);
                        break;
                }
            }

            if (clsName is null || fieldsArgument is null)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(cls_name, fields, ...) expects cls_name and fields.", span);
            }

            var name = ExpectIdentifierString(clsName, "dataclasses.make_dataclass(cls_name=...)", span);
            var bases = ParseBases(basesArgument, span);
            var members = new Dictionary<string, object>(StringComparer.Ordinal);
            if (namespaceArgument is not null && !ReferenceEquals(namespaceArgument, PyNone.Instance))
            {
                if (namespaceArgument is not PyDict namespaceDict)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(namespace=...) expects a dict or None.", span);
                }

                foreach (var pair in namespaceDict)
                {
                    var memberName = ExpectIdentifierString(pair.Key, "dataclasses.make_dataclass(namespace=...) key", span);
                    members[memberName] = pair.Value;
                }
            }

            var annotations = new PyDict(context.MemoryGovernor, span);
            foreach (var field in ParseMakeDataclassFields(fieldsArgument, context, span))
            {
                annotations.SetItem(PyString.FromString(field.Name), field.Annotation);
                if (field.Default is PyDataclassFieldDefinition definition)
                {
                    members[field.Name] = definition;
                }
                else if (!ReferenceEquals(field.Default, PyDataclassMissing.Instance))
                {
                    members[field.Name] = field.Default;
                }
            }

            members["__annotations__"] = annotations;

            PyType type;
            try
            {
                type = new PyType(name, bases, members);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }

            if (context.TryGetBuiltinType("type", out var metaType))
            {
                type.SetMetaType(metaType);
            }

            ApplyRuntime(type, ToDecorator(options, span), context, span);
            type.InitializeClassMembers(context, span);
            return type;
        }
    }

    private sealed record MakeDataclassField(string Name, object Annotation, object Default);

    private static IEnumerable<MakeDataclassField> ParseMakeDataclassFields(object fieldsArgument, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in LythonRuntime.ToSequence(fieldsArgument, span))
        {
            string name;
            object annotation = PyString.FromString("typing.Any");
            object defaultValue = PyDataclassMissing.Instance;

            if (PyStringOps.TryAsString(item, out var text))
            {
                name = text.AsString();
            }
            else if (item is PyTuple tuple)
            {
                if (tuple.Count is not 2 and not 3)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass() field tuples must have 2 or 3 items.", span);
                }

                name = ExpectIdentifierString(tuple[0], "dataclasses.make_dataclass() field name", span);
                annotation = tuple[1];
                if (tuple.Count == 3)
                {
                    defaultValue = tuple[2];
                }
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass() fields must contain names or field tuples.", span);
            }

            if (!IsIdentifierName(name))
            {
                throw new LythonRuntimeException("TypeError", $"Field name '{name}' is not a valid identifier.", span);
            }

            if (!seenNames.Add(name))
            {
                throw new LythonRuntimeException("TypeError", $"Field name '{name}' is duplicated.", span);
            }

            context.CheckExecutionBudget(span);
            yield return new MakeDataclassField(name, annotation, defaultValue);
        }
    }

    private static IReadOnlyList<PyType> ParseBases(object? basesArgument, LythonSourceSpan span)
    {
        if (basesArgument is null || ReferenceEquals(basesArgument, PyNone.Instance))
        {
            return [];
        }

        var bases = new List<PyType>();
        foreach (var item in LythonRuntime.ToSequence(basesArgument, span))
        {
            if (item is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(bases=...) expects classes.", span);
            }

            bases.Add(type);
        }

        return bases;
    }

    private static string ExpectIdentifierString(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects a string.", span);
        }

        return text.AsString();
    }

    private static void SetSingle(ref object? target, object value, HashSet<string> seen, string name, string owner, LythonSourceSpan span)
    {
        if (!seen.Add(name) || target is not null)
        {
            throw CallErrors.MultipleValues("Builtin", owner, name, span);
        }

        target = value;
    }

    private static DataclassOptions ApplyDataclassOption(DataclassOptions options, string name, object value, HashSet<string> seen, string owner, LythonSourceSpan span)
    {
        if (!seen.Add(name))
        {
            throw CallErrors.MultipleValues("Builtin", owner, name, span);
        }

        return name switch
        {
            "init" => options with { Init = ExpectBool(value, $"{owner}(init=...)", span) },
            "repr" => options with { Repr = ExpectBool(value, $"{owner}(repr=...)", span) },
            "eq" => options with { Eq = ExpectBool(value, $"{owner}(eq=...)", span) },
            "order" => options with { Order = ExpectBool(value, $"{owner}(order=...)", span) },
            "unsafe_hash" => options with { UnsafeHash = ExpectBool(value, $"{owner}(unsafe_hash=...)", span) },
            "frozen" => options with { Frozen = ExpectBool(value, $"{owner}(frozen=...)", span) },
            "kw_only" => options with { KwOnly = ExpectBool(value, $"{owner}(kw_only=...)", span) },
            "match_args" => options with { MatchArgs = ExpectBool(value, $"{owner}(match_args=...)", span) },
            "slots" => RejectUnsupportedDataclassSlotOption(options, value, owner, "slots", span),
            "weakref_slot" => RejectUnsupportedDataclassSlotOption(options, value, owner, "weakref_slot", span),
            _ => throw CallErrors.UnexpectedKeyword("Builtin", owner, name, span)
        };
    }

    private static DataclassOptions RejectUnsupportedDataclassSlotOption(DataclassOptions options, object value, string owner, string name, LythonSourceSpan span)
    {
        if (!ExpectBool(value, $"{owner}({name}=...)", span))
        {
            return options;
        }

        throw new LythonRuntimeException("NotImplementedError", $"{owner}({name}=True) is not supported by Lython.", span);
    }

    private static bool IsIdentifierName(string value)
    {
        if (value.Length == 0 || !(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '_' && !char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
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

            var changes = new List<CallArgumentValue>();
            for (var i = 1; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.replace(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
                }

                changes.Add(argument);
            }

            return ReplaceInstance(instance, changes, span, context, "dataclasses.replace");
        }
    }

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
            if (argument.Name is null)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
            }

            if (!changes.TryAdd(argument.Name, argument.Value))
            {
                throw CallErrors.MultipleValues("Builtin", owner, argument.Name, span);
            }
        }

            foreach (var change in changes.Keys)
            {
                var field = instance.Type.DataclassFields.FirstOrDefault(candidate => candidate.Name == change);
                if (field is null)
                {
                    throw new LythonRuntimeException("TypeError", $"{owner}() got an unexpected field '{change}'.", span);
                }

                if (!field.Init)
                {
                    throw new LythonRuntimeException("TypeError", $"{owner}() cannot override init=False field '{change}'.", span);
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

                    throw new LythonRuntimeException("ValueError", $"InitVar '{field.Name}' must be specified with {owner}().", span);
                }

                _ = instance.TryGetOwnAttribute(field.Name, out var existingValue);
                callArguments.Add(new CallArgumentValue(field.Name, existingValue ?? PyNone.Instance));
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
        var annotations = TryGetAnnotations(members, span);

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
                GetAnnotationValue(annotations, statement.Name, statement.Annotation),
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

    private static DataclassFieldSpec[] CollectRuntimeFields(PyType type, bool decoratorKwOnly, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var fields = new List<DataclassFieldSpec>();
        var defaultKwOnly = decoratorKwOnly;

        if (!type.TryGetOwnMember("__annotations__", out var rawAnnotations) ||
            ReferenceEquals(rawAnnotations, PyNone.Instance))
        {
            return [];
        }

        if (rawAnnotations is not PyDict annotations)
        {
            throw new LythonRuntimeException("TypeError", "dataclasses.dataclass() expects __annotations__ to be a dict when present.", span);
        }

        foreach (var pair in annotations)
        {
            if (!PyStringOps.TryAsString(pair.Key, out var nameText))
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.dataclass() expects string keys in __annotations__.", span);
            }

            var name = nameText.AsString();
            var annotation = pair.Value;
            if (IsKwOnlyAnnotation(annotation))
            {
                defaultKwOnly = true;
                type.RemoveOwnMember(name);
                continue;
            }

            var kind = ClassifyFieldKind(annotation);
            var fieldDefinition = type.TryGetOwnMember(name, out var rawMember) && rawMember is PyDataclassFieldDefinition definition
                ? definition
                : null;

            var hasDefault = fieldDefinition?.HasDefault ?? type.TryGetOwnMember(name, out rawMember);
            var classMemberValue = fieldDefinition?.DefaultValue ?? (hasDefault ? rawMember : PyNone.Instance);
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
                    throw new LythonRuntimeException("TypeError", $"field '{name}' is a ClassVar but specifies kw_only.", span);
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
                name,
                annotation,
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
                type.TrySetMember(name, classMemberValue);
            }
            else
            {
                type.RemoveOwnMember(name);
            }
        }

        return fields.ToArray();
    }

    private static DataclassFieldSpec[] MergeInheritedFields(PyType type, IReadOnlyList<DataclassFieldSpec> ownFields)
    {
        var merged = new List<DataclassFieldSpec>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var baseType in type.Mro.Skip(1).Reverse())
        {
            if (baseType.DataclassFields is null)
            {
                continue;
            }

            foreach (var field in baseType.DataclassFields)
            {
                AddOrReplace(field);
            }
        }

        foreach (var field in ownFields)
        {
            AddOrReplace(field);
        }

        return [.. merged];

        void AddOrReplace(DataclassFieldSpec field)
        {
            if (positions.TryGetValue(field.Name, out var index))
            {
                merged[index] = field;
                return;
            }

            positions[field.Name] = merged.Count;
            merged.Add(field);
        }
    }

    private static PyDict? TryGetAnnotations(Dictionary<string, object> members, LythonSourceSpan span)
    {
        if (!members.TryGetValue("__annotations__", out var raw) || ReferenceEquals(raw, PyNone.Instance))
        {
            return null;
        }

        return raw as PyDict ??
            throw new LythonRuntimeException("TypeError", "Class __annotations__ must be a dict.", span);
    }

    private static object GetAnnotationValue(PyDict? annotations, string name, ExpressionSyntax fallback)
    {
        if (annotations is not null && annotations.TryGetValue(PyString.FromString(name), out var value))
        {
            return value;
        }

        return new PyDataclassAnnotationValue(fallback);
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

    private static bool IsKwOnlyAnnotation(object annotation)
        => annotation switch
        {
            PyDataclassKwOnlyMarker => true,
            PyDataclassAnnotationValue syntax => IsKwOnlyMarker(syntax.Expression),
            PyString text when text.AsString() == "KW_ONLY" || text.AsString() == "dataclasses.KW_ONLY" => true,
            string text when text == "KW_ONLY" || text == "dataclasses.KW_ONLY" => true,
            _ => false
        };

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

    private static DataclassFieldKind ClassifyFieldKind(object annotation)
        => annotation switch
        {
            PyDataclassInitVarMarker => DataclassFieldKind.InitVar,
            PyDataclassAnnotationValue syntax => ClassifyFieldKind(syntax.Expression),
            PyTypingAlias { ShortName: "ClassVar" } => DataclassFieldKind.ClassVar,
            PyString text when IsClassVarText(text.AsString()) => DataclassFieldKind.ClassVar,
            PyString text when IsInitVarText(text.AsString()) => DataclassFieldKind.InitVar,
            string text when IsClassVarText(text) => DataclassFieldKind.ClassVar,
            string text when IsInitVarText(text) => DataclassFieldKind.InitVar,
            _ => DataclassFieldKind.Normal
        };

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

    private static bool IsClassVarText(string text)
        => text == "ClassVar" ||
           text == "typing.ClassVar" ||
           text.StartsWith("ClassVar[", StringComparison.Ordinal) ||
           text.StartsWith("typing.ClassVar[", StringComparison.Ordinal);

    private static bool IsInitVarText(string text)
        => text == "InitVar" ||
           text == "dataclasses.InitVar" ||
           text.StartsWith("InitVar[", StringComparison.Ordinal) ||
           text.StartsWith("dataclasses.InitVar[", StringComparison.Ordinal);

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
