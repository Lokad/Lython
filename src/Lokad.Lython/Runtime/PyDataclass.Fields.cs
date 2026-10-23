using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private readonly record struct OwnFieldFacts(
        string Name,
        object Annotation,
        DataclassFieldKind Kind,
        PyDataclassFieldObject? Definition,
        bool HasDefault,
        object ClassMemberValue,
        bool DefaultKwOnly);

    private static PyDict BuildFieldMap(IReadOnlyList<DataclassFieldSpec> fields, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var dict = new PyDict(context.MemoryGovernor, span);
        foreach (var field in fields)
        {
            dict.SetItem(PyString.FromString(field.Name), field.Field);
        }

        return dict;
    }

    private static async ValueTask<DataclassFieldSpec[]> CollectRuntimeFields(PyType type, bool decoratorKwOnly, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
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
            var fieldDefinition = type.TryGetMember(name, out var rawMember) && rawMember is PyDataclassFieldObject definition
                ? definition
                : null;

            var hasDefault = fieldDefinition?.HasDefault ?? type.TryGetMember(name, out rawMember);
            var classMemberValue = fieldDefinition?.Default ?? (hasDefault ? rawMember.RequireNotNull() : PyNone.Instance);
            fields.Add(await CreateOwnField(
                new OwnFieldFacts(
                name,
                annotation,
                kind,
                fieldDefinition,
                hasDefault,
                classMemberValue,
                defaultKwOnly),
                type,
                context,
                span, asynchronous).ConfigureAwait(false));
        }

        return fields.ToArray();
    }

    private static async ValueTask<DataclassFieldSpec> CreateOwnField(
        OwnFieldFacts facts,
        PyType type,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        var defaultValue = facts.ClassMemberValue;
        var fieldObject = facts.Definition ?? new PyDataclassFieldObject { Default = facts.HasDefault ? facts.ClassMemberValue : PyDataclassMissing.Instance, Metadata = NormalizeFieldMetadata(PyNone.Instance, span, context) };
        fieldObject.Name = facts.Name;
        fieldObject.Annotation = facts.Annotation;
        fieldObject.Kind = facts.Kind;
        var explicitKwOnly = !ReferenceEquals(fieldObject.KwOnly, PyDataclassMissing.Instance);
        if (!explicitKwOnly) fieldObject.KwOnly = facts.DefaultKwOnly;
        if (facts.Kind == DataclassFieldKind.ClassVar && facts.Definition is not null && explicitKwOnly)
        {
            throw new LythonRuntimeException("TypeError", $"field '{facts.Name}' is a ClassVar but specifies kw_only.", span);
        }

        if (facts.HasDefault && facts.Definition is null && facts.Kind != DataclassFieldKind.ClassVar)
        {
            defaultValue = await ResolveDescriptorBackedDefault(type, defaultValue, context, span, asynchronous).ConfigureAwait(false);
            fieldObject.Default = defaultValue;
        }

        if (facts.Definition is not null)
        {
            if (facts.HasDefault)
            {
                type.TrySetMember(facts.Name, facts.ClassMemberValue);
            }
            else
            {
                type.RemoveOwnMember(facts.Name);
            }
        }

        return await SnapshotField(fieldObject, context, span, asynchronous).ConfigureAwait(false);
    }

    private static async ValueTask<DataclassFieldSpec> SnapshotField(PyDataclassFieldObject field, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var init = asynchronous ? await LythonRuntime.IsTruthyAsync(field.Init, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.Init, context, span);
        var repr = asynchronous ? await LythonRuntime.IsTruthyAsync(field.Repr, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.Repr, context, span);
        var compare = asynchronous ? await LythonRuntime.IsTruthyAsync(field.Compare, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.Compare, context, span);
        bool? hash = field.Hash is PyNone ? null : asynchronous ? await LythonRuntime.IsTruthyAsync(field.Hash, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.Hash, context, span);
        var kwOnly = asynchronous ? await LythonRuntime.IsTruthyAsync(field.KwOnly, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.KwOnly, context, span);
        if (field.Kind == DataclassFieldKind.ClassVar) { init = false; kwOnly = false; }
        if (field.Kind != DataclassFieldKind.Normal) { repr = false; compare = false; hash = false; }
        return new DataclassFieldSpec(field.Name.RequireNotNull(), field.Annotation, field.Kind, field.HasDefault, field.Default, field.HasDefaultFactory, field.DefaultFactory,
            init, repr, compare, hash, kwOnly, field, field.Kind == DataclassFieldKind.Normal);
    }

    private static async ValueTask<DataclassFieldSpec[]> MergeInheritedFields(PyType type, IReadOnlyList<DataclassFieldSpec> ownFields, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var merged = new Dictionary<string, DataclassFieldSpec>(StringComparer.Ordinal);
        foreach (var baseType in type.Mro.Skip(1).Reverse())
        {
            if (!baseType.TryGetOwnMember("__dataclass_fields__", out var raw) || raw is not PyDict map) continue;
            foreach (var pair in map)
            {
                context.CheckExecution(span);
                if (pair.Value is not PyDataclassFieldObject field) throw RuntimeErrors.Type("invalid dataclass field metadata", span);
                merged[field.Name.RequireNotNull()] = await SnapshotField(field, context, span, asynchronous).ConfigureAwait(false);
            }
        }
        foreach (var field in ownFields) merged[field.Name] = field;
        return merged.Values.ToArray();
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

    private static bool IsKwOnlyAnnotation(object annotation)
        => annotation switch
        {
            PyDataclassKwOnlyMarker => true,
            PyString text when text.AsString() == "KW_ONLY" || text.AsString() == "dataclasses.KW_ONLY" => true,
            _ => false
        };

    internal static DataclassFieldKind ClassifyFieldKind(ExpressionSyntax annotation)
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

    internal static DataclassFieldKind ClassifyFieldKind(object annotation)
        => annotation switch
        {
            PyDataclassInitVarMarker => DataclassFieldKind.InitVar,
            PyTypingAlias { ShortName: "ClassVar" } => DataclassFieldKind.ClassVar,
            PyString text when IsClassVarText(text.AsString()) => DataclassFieldKind.ClassVar,
            PyString text when IsInitVarText(text.AsString()) => DataclassFieldKind.InitVar,
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
        var dictionary = value is PyNone ? new PyDict(context.MemoryGovernor, span) : value as PyDict
            ?? throw RuntimeErrors.Type("dataclasses.field(metadata=...) expects a dict or None in Lython.", span);
        return new PyMappingProxy(dictionary, context.MemoryGovernor, span);
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

    private static async ValueTask<object> ResolveDescriptorBackedDefault(PyType type, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        if (value is IPyDescriptor descriptor) return asynchronous ? await descriptor.GetAsync(null, type, context, span).ConfigureAwait(false) : descriptor.Get(null, type, context, span);
        if (value is PyInstance instance && instance.Type.TryLookupInMro("__get__", 0, out var raw, out _))
        {
            var bound = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false) : raw is IPyDescriptor getter ? getter.Get(instance, instance.Type, context, span) : raw;
            if (bound is not LythonRuntime.ICallable callable) throw RuntimeErrors.Type("__get__ must be callable", span);
            return asynchronous ? await CallableInvocation.InvokeBinaryAsync(callable, PyNone.Instance, type, span, context).ConfigureAwait(false) : CallableInvocation.InvokeBinary(callable, PyNone.Instance, type, span, context);
        }
        return value;
    }

    internal static bool ShouldIncludeInGeneratedHash(DataclassFieldSpec field)
    {
        if (field.Kind != DataclassFieldKind.Normal)
        {
            return false;
        }

        return field.Hash ?? field.Compare;
    }

}
