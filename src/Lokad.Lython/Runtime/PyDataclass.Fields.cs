using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private readonly record struct OwnFieldFacts(
        string Name,
        object Annotation,
        DataclassFieldKind Kind,
        PyDataclassFieldDefinition? Definition,
        bool HasDefault,
        object ClassMemberValue,
        bool DefaultKwOnly);

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
        var defaultKwOnly = syntax.DataclassDecorator.RequireNotNull().KwOnly;
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
            fields.Add(CreateOwnField(
                new OwnFieldFacts(
                statement.Name,
                GetAnnotationValue(annotations, statement.Name, statement.Annotation),
                kind,
                fieldDefinition,
                hasDefault,
                classMemberValue,
                defaultKwOnly),
                type,
                context,
                span));
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
            var classMemberValue = fieldDefinition?.DefaultValue ?? (hasDefault ? rawMember.RequireNotNull() : PyNone.Instance);
            fields.Add(CreateOwnField(
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
                span));
        }

        return fields.ToArray();
    }

    private static DataclassFieldSpec CreateOwnField(
        OwnFieldFacts facts,
        PyType type,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        var defaultValue = facts.ClassMemberValue;
        var init = facts.Definition?.Init ?? true;
        var repr = facts.Definition?.Repr ?? true;
        var compare = facts.Definition?.Compare ?? true;
        var hash = facts.Definition?.Hash;
        var kwOnly = facts.Definition?.KwOnly ?? facts.DefaultKwOnly;

        if (facts.Kind == DataclassFieldKind.ClassVar)
        {
            if (facts.Definition?.KwOnly is not null)
            {
                throw new LythonRuntimeException("TypeError", $"field '{facts.Name}' is a ClassVar but specifies kw_only.", span);
            }

            init = false;
            repr = false;
            compare = false;
            hash = false;
            kwOnly = false;
        }
        else if (facts.Kind == DataclassFieldKind.InitVar)
        {
            repr = false;
            compare = false;
            hash = false;
        }

        if (facts.HasDefault && facts.Kind != DataclassFieldKind.ClassVar)
        {
            defaultValue = ResolveDescriptorBackedDefault(type, defaultValue, context, span);
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

        return new DataclassFieldSpec(
            facts.Name,
            facts.Annotation,
            facts.Kind,
            facts.HasDefault,
            defaultValue,
            facts.Definition?.HasDefaultFactory ?? false,
            facts.Definition?.DefaultFactory ?? PyNone.Instance,
            init,
            repr,
            compare,
            hash,
            kwOnly,
            facts.Definition?.Metadata ?? new PyDict(context.MemoryGovernor, span),
            Store: facts.Kind == DataclassFieldKind.Normal);
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

    private static bool TryResolveDynamicDescriptorDefault(PyInstance descriptorInstance, PyType owner, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
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

            value = CallableInvocation.InvokeBinary(callable, PyNone.Instance, owner, span, context);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

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
}
