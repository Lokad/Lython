using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
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
        type.TrySetMember("__dataclass_params__", new PyDataclassParamsObject(decorator));
        type.TrySetMember("__dataclass_fields__", BuildFieldMap(fields, context, span));

        if (decorator.Init && !type.TryGetOwnMember("__init__", out _))
        {
            type.TrySetMember("__init__", new DataclassInitMethod(type.Name, fields));
        }

        if (decorator.Repr && !type.TryGetOwnMember("__repr__", out _))
        {
            type.TrySetMember("__repr__", new DataclassReprMethod(type.Name, type.DataclassReprFields.RequireNotNull()));
        }

        if (decorator.Eq && !type.TryGetOwnMember("__eq__", out _))
        {
            type.TrySetMember("__eq__", new DataclassEqMethod(type.Name, type.DataclassComparableFields.RequireNotNull()));
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

    private sealed class DataclassCallableImpl : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var options = DataclassDecoratorSyntax.CreateDefault(span);
            PyType? cls = null;
            var seenCls = false;
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
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

                if (argument.KeywordName == "cls")
                {
                    if (seenCls)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.dataclass", "cls", span);
                    }

                    seenCls = true;
                    if (!ReferenceEquals(argument.Value, PyNone.Instance))
                    {
                        cls = argument.Value as PyType ??
                            throw new LythonRuntimeException("TypeError", "dataclasses.dataclass(cls=...) expects a class or None.", span);
                    }

                    continue;
                }

                options = ApplyDataclassOption(options, argument.KeywordName, argument.Value, seenOptions, "dataclasses.dataclass", span);
            }

            return cls is null
                ? new DataclassRuntimeDecorator(options)
                : ApplyRuntime(cls, options, context, span);
        }
    }

    private sealed class DataclassRuntimeDecorator(DataclassDecoratorSyntax decorator) : LythonRuntime.ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyType type)
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
            var options = DataclassDecoratorSyntax.CreateDefault(span);
            var positionalCount = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
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

                switch (argument.KeywordName)
                {
                    case "cls_name":
                        SetSingle(ref clsName, argument.Value, seen, argument.KeywordName, "dataclasses.make_dataclass", span);
                        break;
                    case "fields":
                        SetSingle(ref fieldsArgument, argument.Value, seen, argument.KeywordName, "dataclasses.make_dataclass", span);
                        break;
                    case "bases":
                        SetSingle(ref basesArgument, argument.Value, seen, argument.KeywordName, "dataclasses.make_dataclass", span);
                        break;
                    case "namespace":
                        SetSingle(ref namespaceArgument, argument.Value, seen, argument.KeywordName, "dataclasses.make_dataclass", span);
                        break;
                    case "module":
                    case "decorator":
                        throw new LythonRuntimeException("NotImplementedError", $"dataclasses.make_dataclass({argument.KeywordName}=...) is not supported by Lython.", span);
                    default:
                        options = ApplyDataclassOption(options, argument.KeywordName, argument.Value, seenOptions, "dataclasses.make_dataclass", span);
                        break;
                }
            }

            if (clsName is null || fieldsArgument is null)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(cls_name, fields, ...) expects cls_name and fields.", span);
            }

            var name = RuntimeArgumentValidation.ExpectString(clsName, "dataclasses.make_dataclass(cls_name=...)", span);
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
                    var memberName = RuntimeArgumentValidation.ExpectString(pair.Key, "dataclasses.make_dataclass(namespace=...) key", span);
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

            ApplyRuntime(type, options, context, span);
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

                name = RuntimeArgumentValidation.ExpectString(tuple[0], "dataclasses.make_dataclass() field name", span);
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

    private static void SetSingle(ref object? target, object value, HashSet<string> seen, string name, string owner, LythonSourceSpan span)
    {
        if (!seen.Add(name) || target is not null)
        {
            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, owner, name, span);
        }

        target = value;
    }

    private static DataclassDecoratorSyntax ApplyDataclassOption(DataclassDecoratorSyntax options, string name, object value, HashSet<string> seen, string owner, LythonSourceSpan span)
    {
        if (!seen.Add(name))
        {
            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, owner, name, span);
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
            _ => throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, owner, name, span)
        };
    }

    private static DataclassDecoratorSyntax RejectUnsupportedDataclassSlotOption(DataclassDecoratorSyntax options, object value, string owner, string name, LythonSourceSpan span)
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
                if (argument.IsPositional)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.field(...) only supports keyword arguments in Lython.", span);
                }

                switch (argument.KeywordName)
                {
                    case "default":
                        if (seenDefault)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "default", span);
                        }

                        defaultValue = argument.Value;
                        seenDefault = true;
                        break;
                    case "default_factory":
                        if (seenDefaultFactory)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "default_factory", span);
                        }

                        defaultFactory = argument.Value;
                        seenDefaultFactory = true;
                        break;
                    case "init":
                        if (seenInit)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "init", span);
                        }

                        init = ExpectBool(argument.Value, "field(init=...)", span);
                        seenInit = true;
                        break;
                    case "repr":
                        if (seenRepr)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "repr", span);
                        }

                        repr = ExpectBool(argument.Value, "field(repr=...)", span);
                        seenRepr = true;
                        break;
                    case "compare":
                        if (seenCompare)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "compare", span);
                        }

                        compare = ExpectBool(argument.Value, "field(compare=...)", span);
                        seenCompare = true;
                        break;
                    case "hash":
                        if (seenHash)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "hash", span);
                        }

                        hash = ExpectOptionalBool(argument.Value, "field(hash=...)", span);
                        seenHash = true;
                        break;
                    case "kw_only":
                        if (seenKwOnly)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "kw_only", span);
                        }

                        kwOnly = ExpectOptionalBool(argument.Value, "field(kw_only=...)", span);
                        seenKwOnly = true;
                        break;
                    case "metadata":
                        if (seenMetadata)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", "metadata", span);
                        }

                        metadata = NormalizeFieldMetadata(argument.Value, span, context);
                        seenMetadata = true;
                        break;
                    default:
                        throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "dataclasses.field", argument.KeywordName, span);
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

            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance || instance.Type.DataclassFields is null)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.replace(obj, **changes) expects a dataclass instance as its first positional argument.", span);
            }

            var changes = new List<CallArgumentValue>();
            for (var i = 1; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (argument.IsPositional)
                {
                    throw new LythonRuntimeException("TypeError", "dataclasses.replace(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
                }

                changes.Add(argument);
            }

            return ReplaceInstance(instance, changes, span, context, "dataclasses.replace");
        }
    }

}
