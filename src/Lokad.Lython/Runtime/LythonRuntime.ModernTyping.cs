using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly record struct TypeScope(ExecutionContext Annotations, ExecutionContext Closure, PyTuple Parameters);

    private static ExecutionContext CreateAnnotationScope(ExecutionContext parent, int names, LythonSourceSpan span)
    {
        var bytes = checked(512L + 96L * names);
        parent.MemoryGovernor.Reserve(bytes, span);
        parent.MemoryGovernor.Commit(bytes);
        var scope = new ExecutionContext(parent) { IsTypeParameterScope = names != 0 };
        parent.Services.State.CallTemporaries.TrackFreshMutable(scope, bytes, span);
        RetainLocalsForLambda(parent);
        return scope;
    }

    private static TypeScope CreateTypeScope(IReadOnlyList<LoweredTypeParameter>? parameters, ExecutionContext context, LythonSourceSpan span, bool alias = false)
    {
        if (parameters is null && !alias) return new(context, context, PyTuple.Empty);
        var scope = CreateAnnotationScope(context, parameters?.Count ?? 0, span);
        context.HasModernTypeDeclarations = true;
        var closure = ReferenceEquals(context.FunctionClosureContext, context) ? scope
            : CreateAnnotationScope(context.FunctionClosureContext, parameters?.Count ?? 0, span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(parameters?.Count ?? 0), span);
        var values = new object[parameters?.Count ?? 0];
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = parameters![i];
            var boundSyntax = parameter.Syntax.Bound;
            while (boundSyntax is ParenthesizedExpressionSyntax parenthesized) boundSyntax = parenthesized.Inner;
            var variable = new PyTypeParameter(parameter.Syntax.Name, parameter.Syntax.Kind,
                parameter.Syntax.Kind == TypeParameterKind.TypeVar,
                parameter.Bound is null ? null : new(parameter.Bound, scope), boundSyntax is TupleLiteralExpressionSyntax,
                parameter.Default is null ? null : new(parameter.Default, scope), parameter.Syntax.UnpackDefault, context, parameter.Syntax.Span);
            values[i] = variable;
            scope.Variables[variable.Name] = variable;
            if (!ReferenceEquals(scope, closure)) closure.Variables[variable.Name] = variable;
        }
        var tuple = values.Length == 0 ? PyTuple.Empty : new PyTuple(values, context.MemoryGovernor, span);
        if (tuple.Count != 0) context.Services.State.CallTemporaries.TrackFreshMutable(tuple, tuple.CommittedStorageBytes, span);
        return new(scope, closure, tuple);
    }

    private static bool HasTypeParameterScope(ExecutionContext context)
    {
        for (var current = context; current is not null; current = current.ParentContext)
            if (current.IsTypeParameterScope) return true;
        return false;
    }

    private static void ExecuteTypeAlias(LoweredTypeAliasStatement alias, ExecutionContext context)
    {
        var scope = CreateTypeScope(alias.TypeParameters, context, alias.Span, alias: true);
        StoreName(alias.Syntax.Name, new PyTypeAlias(alias.Syntax.Name, scope.Parameters, alias.Value, scope.Annotations, alias.Span), context, alias.Span);
    }

    internal static ValueTask<object> EvaluateTypeExpressionAsync(LoweredExpression expression, ExecutionContext context, bool asynchronous)
        => asynchronous ? EvaluateLoweredExpressionAsync(expression, context) : new(EvaluateLoweredExpression(expression, context));

    internal static async ValueTask<object> CreateGenericSubscriptAsync(object origin, object index, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        if (origin is not PyType type || !type.TryGetMember("__type_params__", out var raw) || raw is not PyTuple parameters)
            return PyGenericAlias.Create(origin, index, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
        var arguments = index is PyTuple tuple ? tuple : new PyTuple([index], context.MemoryGovernor, span);
        var values = new PyList([], context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(values, values.CommittedStorageBytes, span);
        var offset = 0;
        for (var parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
        {
            var parameter = (PyTypeParameter)parameters[parameterIndex];
            if (parameter.Kind == TypeParameterKind.TypeVarTuple)
            {
                var suffix = parameters.Count - parameterIndex - 1;
                var count = Math.Max(0, arguments.Count - offset - suffix);
                if (count == 0 && parameter.HasDefault)
                {
                    var defaultValue = await Default(parameter).ConfigureAwait(false);
                    if (defaultValue is PyUnpackedType { Value: PyGenericAlias alias })
                        foreach (var argument in alias.Arguments) values.Add(argument);
                    else if (defaultValue is PyTuple defaults)
                        foreach (var argument in defaults) values.Add(argument);
                    else throw new LythonRuntimeException("TypeError", "TypeVarTuple default must be a tuple or unpacked tuple type", span);
                }
                else while (count-- > 0) values.Add(arguments[offset++]);
                continue;
            }
            if (parameter.Kind == TypeParameterKind.ParamSpec && parameters.Count == 1 && arguments.Count > 0)
            {
                object specification = arguments.Count == 1 ? arguments[0] : arguments;
                if (specification is PyList list) specification = new PyTuple(list, context.MemoryGovernor, span);
                else if (specification is not (PyTuple or PyTypeParameter or PyEllipsis)) specification = new PyTuple([specification], context.MemoryGovernor, span);
                values.Add(specification); offset = arguments.Count;
                continue;
            }
            if (offset < arguments.Count) values.Add(arguments[offset++]);
            else if (parameter.HasDefault) values.Add(await Default(parameter).ConfigureAwait(false));
            else throw new LythonRuntimeException("TypeError", "Too few arguments for generic class", span);
        }
        if (offset != arguments.Count) throw new LythonRuntimeException("TypeError", "Too many arguments for generic class", span);
        context.Services.State.CallTemporaries.TrackGrowth(values, values.CommittedStorageBytes, span);
        var final = new PyTuple(values, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(final, final.CommittedStorageBytes, span);
        return PyGenericAlias.Create(origin, final, context.MemoryGovernor, span, context.Services.State.CallTemporaries);

        async ValueTask<object> Default(PyTypeParameter parameter)
            => asynchronous ? (await parameter.TryGetMemberAsync("__default__", context, span).ConfigureAwait(false)).Value
                : parameter.TryGetMember("__default__", context, span, out var value) ? value : PyTypeParameter.NoDefault;
    }

    private static async ValueTask AttachTypeMetadataAsync(PyFunctionBase function, LoweredFunctionDefinitionStatement definition,
        TypeScope scope, ExecutionContext definingContext, bool asynchronous)
    {
        if (definition.TypeParameters is not null)
            function.TrySetMember("__type_params__", scope.Parameters);
        if (definition.TypeParameters is null && !HasTypeParameterScope(definingContext)) return;
        var annotations = new PyDict(definingContext.MemoryGovernor, definition.Span);
        foreach (var parameter in definition.Parameters)
            if (parameter.Annotation is not null)
                annotations.SetItem(PyString.FromString(parameter.Name), await EvaluateTypeExpressionAsync(parameter.Annotation, scope.Annotations, asynchronous).ConfigureAwait(false));
        if (definition.ReturnAnnotation is not null)
            annotations.SetItem(PyString.FromString("return"), await EvaluateTypeExpressionAsync(definition.ReturnAnnotation, scope.Annotations, asynchronous).ConfigureAwait(false));
        definingContext.Services.State.CallTemporaries.TrackFreshMutable(annotations, annotations.CommittedStorageBytes, definition.Span);
        function.TrySetMember("__annotations__", annotations);
    }

    private static async ValueTask AttachGenericClassMetadataAsync(LoweredClassDefinitionStatement definition, TypeScope scope,
        ExecutionContext classContext, bool asynchronous, object[] baseTypes)
    {
        if (definition.TypeParameters is null) return;
        classContext.HasModernTypeDeclarations = true;
        classContext.Variables["__type_params__"] = scope.Parameters;
        classContext.Variables["__parameters__"] = scope.Parameters;
        var originalBases = new PyTuple(baseTypes.Concat([PyGenericAlias.Create(GetTypingGeneric(classContext, definition.Span), scope.Parameters, classContext.MemoryGovernor, definition.Span, classContext.Services.State.CallTemporaries)]), classContext.MemoryGovernor, definition.Span);
        classContext.Services.State.CallTemporaries.TrackFreshMutable(originalBases, originalBases.CommittedStorageBytes, definition.Span);
        classContext.Variables["__orig_bases__"] = originalBases;
        await ValueTask.CompletedTask;
    }

    private static async ValueTask StoreModernClassAnnotationAsync(LoweredAnnotatedAssignmentStatement statement, ExecutionContext context, bool asynchronous)
    {
        if (!context.EvaluateModernClassAnnotations || statement.Assignment.Target is not NameAssignmentTargetSyntax name) return;
        if (!context.Variables.TryGetValue("__annotations__", out var value))
        {
            value = new PyDict(context.MemoryGovernor, statement.Span);
            context.Variables["__annotations__"] = value;
        }
        var annotations = (PyDict)value;
        annotations.SetItem(PyString.FromString(name.Name), await EvaluateTypeExpressionAsync(statement.Annotation, context, asynchronous).ConfigureAwait(false));
        context.Services.State.CallTemporaries.TrackGrowth(annotations, annotations.CommittedStorageBytes, statement.Span);
    }

    private static PyType GetTypingGeneric(ExecutionContext context, LythonSourceSpan span)
    {
        if (context.Services.State.TypingGeneric is { } existing) return existing;
        context.TryGetBuiltinType("object", out var root);
        using var scratch = context.MemoryGovernor.ReserveTemporary(1024, span);
        var members = new Dictionary<string, object>(StringComparer.Ordinal) { ["__module__"] = PyString.FromString("typing") };
        var generic = new PyType("Generic", [root!], members, context.MemoryGovernor, span);
        if (context.TryGetBuiltinType("type", out var metatype)) generic.SetMetaType(metatype);
        ChargeClassTypeValue(members.Count, context.MemoryGovernor, span);
        TrackClassTypeValue(generic, members.Count, context, span);
        context.Services.State.TypingGeneric = generic;
        return generic;
    }

    private static IReadOnlyList<PyType> AddGenericClassBase(LoweredClassDefinitionStatement definition, IReadOnlyList<PyType> bases, ExecutionContext context)
    {
        if (definition.TypeParameters is null) return bases;
        var generic = GetTypingGeneric(context, definition.Span);
        if (bases.Contains(generic)) return bases;
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(bases.Count + 1), definition.Span);
        return bases.Count == 1 && bases[0].Name == "object" ? [generic] : bases.Concat([generic]).ToArray();
    }

    internal static object ConstructTypeParameter(TypeParameterKind kind, CallArgumentValue[] arguments, ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length == 0 || !PyStringOps.TryAsString(arguments[0].Value, out var name))
            throw new LythonRuntimeException("TypeError", "Type parameter name must be a string", span);
        object? bound = null, defaultValue = null;
        var covariant = false; var contravariant = false; var inferVariance = false;
        foreach (var argument in arguments.Skip(1))
        {
            if (!argument.IsKeyword) continue;
            switch (argument.KeywordName)
            {
                case "bound": bound = argument.Value; break;
                case "default": defaultValue = ReferenceEquals(argument.Value, PyTypeParameter.NoDefault) ? null : argument.Value; break;
                case "covariant": covariant = IsTruthy(argument.Value, context, span); break;
                case "contravariant": contravariant = IsTruthy(argument.Value, context, span); break;
                case "infer_variance": inferVariance = IsTruthy(argument.Value, context, span); break;
                default: throw new LythonRuntimeException("TypeError", "Unexpected type parameter keyword", span);
            }
        }
        var constraints = arguments.Skip(1).Where(argument => !argument.IsKeyword).Select(argument => argument.Value).ToArray();
        if (constraints.Length == 1 || constraints.Length != 0 && (kind != TypeParameterKind.TypeVar || bound is not null))
            throw new LythonRuntimeException("TypeError", "Invalid type parameter constraints", span);
        if (covariant && contravariant || inferVariance && (covariant || contravariant))
            throw new LythonRuntimeException("ValueError", "Invalid variance flags", span);
        if (constraints.Length != 0) bound = new PyTuple(constraints, context.MemoryGovernor, span);
        return new PyTypeParameter(name.AsString(), kind, inferVariance, null, constraints.Length != 0, null, false,
            context, span, bound, defaultValue, covariant, contravariant);
    }

    private static bool IsTypeLike(object value) => value is PyType or PyTypeAlias or PyGenericAlias or PyTypingAlias or PyNone ||
        value is PyTypeParameter { Kind: TypeParameterKind.TypeVar } || value is PyBuiltinRuntimeType ||
        value is INamedRuntimeCallable named && IsBuiltinTypeName(named.Name);

    private static bool TryTypeUnion(object left, object right, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        value = null;
        if (!IsTypeLike(left) || !IsTypeLike(right)) return false;
        var leftItems = left is PyGenericAlias leftAlias && ReferenceEquals(leftAlias.Origin, PyGenericAlias.UnionOrigin) ? leftAlias.Arguments : null;
        var rightItems = right is PyGenericAlias rightAlias && ReferenceEquals(rightAlias.Origin, PyGenericAlias.UnionOrigin) ? rightAlias.Arguments : null;
        using var scratch = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes((leftItems?.Count ?? 1) + (rightItems?.Count ?? 1)), span);
        var values = new List<object>();
        Add(leftItems ?? new PyTuple([left])); Add(rightItems ?? new PyTuple([right]));
        if (values.Count == 1) { value = values[0]; return true; }
        var args = new PyTuple(values, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(args, args.CommittedStorageBytes, span);
        value = PyGenericAlias.Create(PyGenericAlias.UnionOrigin, args, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
        context.Services.State.CallTemporaries.TrackCallResult(value, span);
        return true;

        void Add(PyTuple items)
        {
            foreach (var item in items)
            {
                var normalized = item is PyNone ? PyType.NoneType : item;
                if (!values.Any(existing => PyEquality.AreEqual(existing, normalized))) values.Add(normalized);
            }
        }
    }

    internal static async ValueTask<(bool Found, object Value)> TryResolveRuntimeMemberAsync(object target, string member,
        ExecutionContext context, LythonSourceSpan span)
    {
        if (target is IPyAsyncDynamicAttributes asynchronous)
        {
            var result = await asynchronous.TryGetMemberAsync(member, context, span).ConfigureAwait(false);
            if (result.Found) return result;
        }
        return TryResolveRuntimeMember(target, member, context, span, out var value) ? (true, value) : (false, PyNone.Instance);
    }
}
