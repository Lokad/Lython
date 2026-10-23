using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private static readonly object DefaultFactorySentinel = new();
    public static readonly LythonRuntime.ICallable DataclassCallable = new DataclassCallableImpl();
    public static readonly LythonRuntime.ICallable FieldCallable = new DataclassFieldCallable();
    public static readonly LythonRuntime.ICallable FieldType = new DataclassFieldType();
    public static readonly LythonRuntime.ICallable MakeDataclassCallable = new DataclassMakeDataclassCallable();
    public static readonly LythonRuntime.ICallable ReplaceCallable = new DataclassReplaceCallable();

    public static ValueTask<PyType> ApplyAsync(PyType type, ClassDefinitionStatementSyntax syntax, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        => syntax.DataclassDecorator is { } options ? ApplyRuntime(type, options, context, span, asynchronous) : ValueTask.FromResult(type);

    private static async ValueTask<PyType> ApplyRuntime(PyType type, DataclassDecoratorSyntax options, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous, PyDataclassParamsObject? parameters = null)
    {
        long fieldCount = type.TryGetOwnMember("__annotations__", out var rawAnnotations) && rawAnnotations is PyDict annotations ? annotations.Count : 0;
        foreach (var parent in type.Mro.Skip(1))
            if (parent.TryGetOwnMember("__dataclass_fields__", out var rawMap) && rawMap is PyDict map) fieldCount = checked(fieldCount + map.Count);
        // Plans survive with the type; cover temporary construction overlap as
        // well as the retained arrays/specifications before calling guest hooks.
        var planBytes = checked(256L + fieldCount * 512L);
        using var plans = context.MemoryGovernor.ReserveTemporary(planBytes, span);
        var ownFields = await CollectRuntimeFields(type, options.KwOnly, context, span, asynchronous).ConfigureAwait(false);
        var fields = await MergeInheritedFields(type, ownFields, context, span, asynchronous).ConfigureAwait(false);
        ValidateFieldOrdering(fields, span);
        plans.Commit(planBytes);
        ApplyCore(type, options, fields, context, span, parameters);
        return type;
    }

    private static void ApplyCore(PyType type, DataclassDecoratorSyntax decorator, IReadOnlyList<DataclassFieldSpec> fields, LythonRuntime.ExecutionContext context, LythonSourceSpan span, PyDataclassParamsObject? parameters)
    {
        ValidateDataclassOptions(type, decorator, span);

        // Generated methods report the defining module like CPython; when the
        // module cannot be resolved they leave __module__ missing instead of
        // reporting a wrong value.
        PyFunctionBase.TryGetModuleName(context, out var moduleName);

        type.SetDataclassMetadata(fields, decorator.Repr, decorator.Eq, decorator.Order, DetermineHashMode(type, decorator));
        type.TrySetMember("__dataclass_params__", parameters ?? new PyDataclassParamsObject(decorator));
        type.TrySetMember("__dataclass_fields__", BuildFieldMap(fields, context, span));

        if (decorator.Init && !type.TryGetOwnMember("__init__", out _))
        {
            type.TrySetMember("__init__", new DataclassInitMethod(type, fields, moduleName, context));
        }

        if (decorator.Repr && !type.TryGetOwnMember("__repr__", out _))
        {
            type.TrySetMember("__repr__", new DataclassReprMethod(type.Name, type.DataclassReprFields.RequireNotNull(), moduleName));
        }

        if (decorator.Eq && !type.TryGetOwnMember("__eq__", out _))
        {
            type.TrySetMember("__eq__", new DataclassEqMethod(type.Name, type.DataclassComparableFields.RequireNotNull(), moduleName));
        }

        if (decorator.Order)
        {
            type.TrySetMember("__lt__", new DataclassOrderMethod(type.Name, type.DataclassComparableFields.RequireNotNull(), DataclassOrderOperation.Less, moduleName));
            type.TrySetMember("__le__", new DataclassOrderMethod(type.Name, type.DataclassComparableFields.RequireNotNull(), DataclassOrderOperation.LessEqual, moduleName));
            type.TrySetMember("__gt__", new DataclassOrderMethod(type.Name, type.DataclassComparableFields.RequireNotNull(), DataclassOrderOperation.Greater, moduleName));
            type.TrySetMember("__ge__", new DataclassOrderMethod(type.Name, type.DataclassComparableFields.RequireNotNull(), DataclassOrderOperation.GreaterEqual, moduleName));
        }

        if (decorator.Frozen)
        {
            type.TrySetMember("__setattr__", new DataclassFrozenSetAttrMethod(type, fields, moduleName));
            type.TrySetMember("__delattr__", new DataclassFrozenDelAttrMethod(type, fields, moduleName));
        }

        if (type.DataclassHashMode == DataclassHashMode.Generated && !type.TryGetOwnMember("__hash__", out _))
        {
            type.TrySetMember("__hash__", new DataclassHashMethod(type.Name, moduleName));
        }

        if (decorator.MatchArgs && !type.TryGetOwnMember("__match_args__", out _))
        {
            var matchArgs = new PyTuple(fields
                .Where(field => field.Kind == DataclassFieldKind.Normal && field.Init && !field.KwOnly)
                .Select(field => (object)PyString.FromString(field.Name)), context.MemoryGovernor, span);
            type.TrySetMember("__match_args__", matchArgs);
        }
    }

    private sealed class DataclassCallableImpl : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private static async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            PyType? type = null; var hasClass = false;
            var options = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (argument.IsPositional || argument.KeywordName == "cls")
                {
                    if (hasClass) throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.dataclass", "cls", span);
                    hasClass = true;
                    type = argument.Value is PyNone ? null : argument.Value as PyType ?? throw RuntimeErrors.Type("dataclass() expects a class", span);
                }
                else
                {
                    if (!IsDataclassOption(argument.KeywordName)) throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "dataclasses.dataclass", argument.KeywordName, span);
                    if (!options.TryAdd(argument.KeywordName, argument.Value)) throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.dataclass", argument.KeywordName, span);
                }
            }
            var decorator = new DataclassRuntimeDecorator(options);
            return type is null ? decorator : await decorator.Apply(type, span, context, asynchronous).ConfigureAwait(false);
        }
    }

    private static bool IsDataclassOption(string name) => name is "init" or "repr" or "eq" or "order" or "unsafe_hash" or "frozen" or "kw_only" or "match_args" or "slots" or "weakref_slot";

    private sealed class DataclassRuntimeDecorator(IReadOnlyDictionary<string, object> rawOptions) : LythonRuntime.ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyType type) throw RuntimeErrors.Type("dataclass decorator expects one class", span);
            return Apply(type, span, context, asynchronous);
        }
        internal async ValueTask<object> Apply(PyType type, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            var options = DataclassDecoratorSyntax.CreateDefault(span);
            foreach (var pair in rawOptions)
            {
                var value = asynchronous ? await LythonRuntime.IsTruthyAsync(pair.Value, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(pair.Value, context, span);
                options = ApplyDataclassOption(options, pair.Key, value, "dataclasses.dataclass", span);
            }
            return await ApplyRuntime(type, options, context, span, asynchronous, new PyDataclassParamsObject(options, rawOptions)).ConfigureAwait(false);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function dataclasses.dataclass.<locals>.wrap>");
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
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private static async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);

            object? clsName = null;
            object? fieldsArgument = null;
            object? basesArgument = null;
            object? namespaceArgument = null;
            var options = DataclassDecoratorSyntax.CreateDefault(span);
            var positionalCount = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);
            var rawOptions = new Dictionary<string, object>(StringComparer.Ordinal);

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
                        if (!seenOptions.Add(argument.KeywordName)) throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.make_dataclass", argument.KeywordName, span);
                        var truth = asynchronous ? await LythonRuntime.IsTruthyAsync(argument.Value, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(argument.Value, context, span);
                        options = ApplyDataclassOption(options, argument.KeywordName, truth, "dataclasses.make_dataclass", span);
                        rawOptions[argument.KeywordName] = argument.Value;
                        break;
                }
            }

            if (clsName is null || fieldsArgument is null)
            {
                throw new LythonRuntimeException("TypeError", "dataclasses.make_dataclass(cls_name, fields, ...) expects cls_name and fields.", span);
            }

            var name = RuntimeArgumentValidation.ExpectString(clsName, "dataclasses.make_dataclass(cls_name=...)", span);
            var bases = ParseBases(basesArgument, span, context);
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
                if (field.Default is PyDataclassFieldObject definition)
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
                type = new PyType(name, bases, members, context.MemoryGovernor, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }

            if (context.TryGetBuiltinType("type", out var metaType))
            {
                type.SetMetaType(metaType);
            }

            await ApplyRuntime(type, options, context, span, asynchronous, new PyDataclassParamsObject(options, rawOptions)).ConfigureAwait(false);
            type.InitializeClassMembers(context, span);
            LythonRuntime.ChargeClassTypeValue(members.Count, context.MemoryGovernor, span);
            return type;
        }
    }

    private sealed record MakeDataclassField(string Name, object Annotation, object Default);

    private static IEnumerable<MakeDataclassField> ParseMakeDataclassFields(object fieldsArgument, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in LythonRuntime.ToSequence(fieldsArgument, span, context))
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

            context.CheckExecution(span);
            yield return new MakeDataclassField(name, annotation, defaultValue);
        }
    }

    private static IReadOnlyList<PyType> ParseBases(object? basesArgument, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (basesArgument is null || ReferenceEquals(basesArgument, PyNone.Instance))
        {
            return [];
        }

        var bases = new List<PyType>();
        foreach (var item in LythonRuntime.ToSequence(basesArgument, span, context))
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

    private static DataclassDecoratorSyntax ApplyDataclassOption(DataclassDecoratorSyntax options, string name, bool value, string owner, LythonSourceSpan span)
        => name switch
        {
            "init" => options with { Init = value },
            "repr" => options with { Repr = value },
            "eq" => options with { Eq = value },
            "order" => options with { Order = value },
            "unsafe_hash" => options with { UnsafeHash = value },
            "frozen" => options with { Frozen = value },
            "kw_only" => options with { KwOnly = value },
            "match_args" => options with { MatchArgs = value },
            "slots" or "weakref_slot" when !value => options,
            "slots" or "weakref_slot" => throw new LythonRuntimeException("NotImplementedError", $"{owner}({name}=True) is not supported by Lython", span),
            _ => throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, owner, name, span)
        };

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
            context.CheckExecution(span);
            object defaultValue = PyDataclassMissing.Instance, factory = PyDataclassMissing.Instance;
            object init = true, repr = true, compare = true, hash = PyNone.Instance, kwOnly = PyDataclassMissing.Instance, metadata = PyNone.Instance;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (argument.IsPositional) throw RuntimeErrors.Type("field() accepts only keyword arguments", span);
                if (!seen.Add(argument.KeywordName)) throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "dataclasses.field", argument.KeywordName, span);
                switch (argument.KeywordName)
                {
                    case "default": defaultValue = argument.Value; break;
                    case "default_factory": factory = argument.Value; break;
                    case "init": init = argument.Value; break;
                    case "repr": repr = argument.Value; break;
                    case "compare": compare = argument.Value; break;
                    case "hash": hash = argument.Value; break;
                    case "kw_only": kwOnly = argument.Value; break;
                    case "metadata": metadata = argument.Value; break;
                    default: throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "dataclasses.field", argument.KeywordName, span);
                }
            }
            if (!ReferenceEquals(defaultValue, PyDataclassMissing.Instance) && !ReferenceEquals(factory, PyDataclassMissing.Instance))
                throw new LythonRuntimeException("ValueError", "cannot specify both default and default_factory", span);
            return new PyDataclassFieldObject
            {
                Default = defaultValue,
                DefaultFactory = factory,
                Init = init,
                Repr = repr,
                Compare = compare,
                Hash = hash,
                KwOnly = kwOnly,
                Metadata = NormalizeFieldMetadata(metadata, span, context)
            };
        }
    }

    private sealed class DataclassReplaceCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private static ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance || !HasFieldMap(instance.Type))
                throw RuntimeErrors.Type("replace() expects a dataclass instance", span);
            return ReplaceInstanceCore(instance, new ArraySegment<CallArgumentValue>(arguments, 1, arguments.Length - 1), span, context, "dataclasses.replace", asynchronous);
        }
    }
}
