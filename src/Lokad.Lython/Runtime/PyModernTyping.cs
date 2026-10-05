using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface IPyAsyncDynamicAttributes
{
    ValueTask<(bool Found, object Value)> TryGetMemberAsync(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span);
}

// Lazy cells publish successes only. Failed resolutions can be retried after a
// forward name is bound, and no host work runs while a type object is created.
internal sealed class PyLazyTypeValue(LoweredExpression expression, LythonRuntime.ExecutionContext scope)
{
    private bool _resolved, _evaluating;
    private object _value = PyNone.Instance;

    public async ValueTask<object> GetAsync(bool asynchronous)
    {
        if (_resolved) return _value;
        if (_evaluating) throw new LythonRuntimeException("RecursionError", "recursive lazy type evaluation", expression.Span);
        scope.EnterInterpreterFrame(expression.Span);
        try
        {
            _evaluating = true;
            var result = await LythonRuntime.EvaluateTypeExpressionAsync(expression, scope, asynchronous).ConfigureAwait(false);
            _value = result;
            _resolved = true;
            return result;
        }
        finally { _evaluating = false; scope.LeaveInterpreterFrame(); }
    }
}

internal sealed class PyTypeParameter : IPyContextualDynamicAttributes, IPyAsyncDynamicAttributes,
    IPyRenderableValue, IPyIterableValue, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    internal static readonly PyBuiltinRuntimeType TypeVarType = TypeObject("TypeVar", TypeParameterKind.TypeVar);
    internal static readonly PyBuiltinRuntimeType TypeVarTupleType = TypeObject("TypeVarTuple", TypeParameterKind.TypeVarTuple);
    internal static readonly PyBuiltinRuntimeType ParamSpecType = TypeObject("ParamSpec", TypeParameterKind.ParamSpec);
    internal static readonly PyNoTypeDefault NoDefault = new();
    private static PyBuiltinRuntimeType TypeObject(string name, TypeParameterKind kind)
        => new("typing." + name, (arguments, span, context) => LythonRuntime.ConstructTypeParameter(kind, arguments, context, span));

    private readonly PyString _name;
    private readonly object _module;
    private readonly PyLazyTypeValue? _bound, _default;
    private readonly object? _constantBound, _constantDefault;
    private readonly bool _constraints, _unpackDefault;
    private readonly long _bytes;
    private object? _hasDefaultMethod, _args, _kwargs, _unpacked, _unpackedDefault;
    public TypeParameterKind Kind { get; }
    public bool InferVariance { get; }
    public bool Covariant { get; }
    public bool Contravariant { get; }
    public bool HasDefault => _default is not null || _constantDefault is not null;
    public string Name => _name.AsString();
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) => OwnershipSnapshot.Owned(OwnerMemoryGovernor, _bytes, out bytes);

    internal PyTypeParameter(string name, TypeParameterKind kind, bool inferVariance,
        PyLazyTypeValue? bound, bool constraints, PyLazyTypeValue? defaultValue, bool unpackDefault,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        object? constantBound = null, object? constantDefault = null, bool covariant = false, bool contravariant = false)
    {
        context.MemoryGovernor.Reserve(512, span);
        context.MemoryGovernor.Commit(512);
        try { _name = PyString.FromString(name, context.MemoryGovernor, span); }
        catch { context.MemoryGovernor.Release(512); throw; }
        _bytes = 512 + PyString.EstimateApproximateBytes(_name.Utf8Bytes.Length);
        Kind = kind; InferVariance = inferVariance; Covariant = covariant; Contravariant = contravariant;
        _bound = bound; _constraints = constraints; _default = defaultValue; _unpackDefault = unpackDefault;
        _constantBound = constantBound; _constantDefault = constantDefault;
        _module = PyFunctionBase.TryGetModuleName(context, out var module) ? module : PyNone.Instance;
        OwnerMemoryGovernor = context.MemoryGovernor; AllocationSpan = span;
        context.Services.State.CallTemporaries.TrackFreshMutable(this, _bytes, span);
    }

    public bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        var result = GetAsync(name, context, span, false).GetAwaiter().GetResult();
        value = result.Value;
        return result.Found;
    }
    public ValueTask<(bool Found, object Value)> TryGetMemberAsync(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => GetAsync(name, context, span, true);

    private async ValueTask<(bool Found, object Value)> GetAsync(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        switch (name)
        {
            case "__name__": return (true, _name);
            case "__module__": return (true, _module);
            case "__infer_variance__" when Kind == TypeParameterKind.TypeVar: return (true, InferVariance);
            case "__covariant__" when Kind != TypeParameterKind.TypeVarTuple: return (true, Covariant);
            case "__contravariant__" when Kind != TypeParameterKind.TypeVarTuple: return (true, Contravariant);
            case "__bound__" when Kind != TypeParameterKind.TypeVarTuple:
                return (true, _constraints ? PyNone.Instance : _bound is null ? _constantBound ?? PyNone.Instance : await _bound.GetAsync(asynchronous).ConfigureAwait(false));
            case "__constraints__" when Kind == TypeParameterKind.TypeVar:
                return (true, !_constraints ? PyTuple.Empty : _bound is null ? _constantBound! : await _bound.GetAsync(asynchronous).ConfigureAwait(false));
            case "__default__":
                var result = _default is null ? _constantDefault ?? NoDefault : await _default.GetAsync(asynchronous).ConfigureAwait(false);
                return (true, _unpackDefault ? _unpackedDefault ??= new PyUnpackedType(result) : result);
            case "has_default": return (true, _hasDefaultMethod ??= new HasDefaultMethod(this));
            case "args" when Kind == TypeParameterKind.ParamSpec: return (true, _args ??= new PyParamSpecPart(this, true));
            case "kwargs" when Kind == TypeParameterKind.ParamSpec: return (true, _kwargs ??= new PyParamSpecPart(this, false));
            default: return (false, PyNone.Instance);
        }
    }
    public IEnumerable<object> Iterate()
    {
        if (Kind != TypeParameterKind.TypeVarTuple) throw new PyNotIterableException($"'{Kind}' object is not iterable", AllocationSpan);
        yield return _unpacked ??= new PyUnpackedType(this);
    }
    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString((InferVariance || Kind != TypeParameterKind.TypeVar ? "" : Covariant ? "+" : Contravariant ? "-" : "~") + Name, context.Context.MemoryGovernor);
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    private sealed class HasDefaultMethod(PyTypeParameter parameter) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 0) throw new LythonRuntimeException("TypeError", "has_default() takes no arguments", span);
            return parameter.HasDefault;
        }
    }
}

internal sealed class PyNoTypeDefault : IPyRenderableValue
{
    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("typing.NoDefault");
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed record PyUnpackedType(object Value) : IPyRenderableValue, IPyDynamicAttributes
{
    public PyString RenderPython(PyRenderingContext context) => PyString.FromString((Value is PyTypeParameter ? "typing.Unpack[" : "*") + PyGenericAlias.RenderArgument(Value, context) + (Value is PyTypeParameter ? "]" : ""), context.Context.MemoryGovernor);
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__origin__" => new PyTypingAlias("Unpack"),
            "__args__" => new PyTuple([Value]),
            _ => PyNone.Instance,
        };
        return value is not PyNone;
    }
}

internal sealed record PyParamSpecPart(PyTypeParameter Parameter, bool Positional) : IPyRenderableValue
{
    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Parameter.Name + (Positional ? ".args" : ".kwargs"));
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyTypeAlias : IPyContextualDynamicAttributes, IPyAsyncDynamicAttributes, IPySubscriptableValue,
    IPyRenderableValue, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    internal static readonly PyBuiltinRuntimeType RuntimeType = new("typing.TypeAliasType",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Use a type statement to define a type alias in Lython.", span));
    private readonly PyString _name;
    private readonly object _module;
    private readonly PyLazyTypeValue _value;
    private readonly ChargeReclamationPool _pool;
    private readonly long _bytes;
    public string Name => _name.AsString();
    public PyTuple TypeParameters { get; }
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) => OwnershipSnapshot.Owned(OwnerMemoryGovernor, _bytes, out bytes);

    internal PyTypeAlias(string name, PyTuple parameters, LoweredExpression value, LythonRuntime.ExecutionContext scope, LythonSourceSpan span)
    {
        scope.MemoryGovernor.Reserve(512, span);
        scope.MemoryGovernor.Commit(512);
        try { _name = PyString.FromString(name, scope.MemoryGovernor, span); }
        catch { scope.MemoryGovernor.Release(512); throw; }
        _bytes = 512 + PyString.EstimateApproximateBytes(_name.Utf8Bytes.Length);
        TypeParameters = parameters; _value = new(value, scope); _pool = scope.Services.State.CallTemporaries;
        _module = PyFunctionBase.TryGetModuleName(scope, out var module) ? module : PyNone.Instance;
        OwnerMemoryGovernor = scope.MemoryGovernor; AllocationSpan = span;
        scope.Services.State.CallTemporaries.TrackFreshMutable(this, _bytes, span);
    }
    public bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        var result = GetAsync(name, false).GetAwaiter().GetResult(); value = result.Value; return result.Found;
    }
    public ValueTask<(bool Found, object Value)> TryGetMemberAsync(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span) => GetAsync(name, true);
    private async ValueTask<(bool Found, object Value)> GetAsync(string name, bool asynchronous)
        => name switch
        {
            "__name__" => (true, _name),
            "__module__" => (true, _module),
            "__type_params__" => (true, TypeParameters),
            "__value__" => (true, await _value.GetAsync(asynchronous).ConfigureAwait(false)),
            _ => (false, PyNone.Instance),
        };
    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (TypeParameters.Count == 0) throw new LythonRuntimeException("TypeError", $"{Name} is not a generic type alias", span);
        return PyGenericAlias.Create(this, index, OwnerMemoryGovernor!, span, _pool);
    }
    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    public PyString RenderPython(PyRenderingContext context) => _name;
    public PyString RenderInterpolated(PyRenderingContext context) => _name;
}

internal sealed class PyGenericAlias : IPyDynamicAttributes, IPySubscriptableValue, IPyIterableValue,
    IPyRenderableValue, LythonRuntime.ICallable, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    internal static readonly PyBuiltinRuntimeType RuntimeType = new("types.GenericAlias",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Use generic subscripting in Lython.", span));
    internal static readonly PyTypingAlias UnionOrigin = new("Union");
    internal static readonly PyBuiltinRuntimeType UnionType = new("types.UnionType",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Cannot construct UnionType directly", span));
    private readonly long _bytes;
    private readonly ChargeReclamationPool? _pool;
    private object? _unpacked;
    internal static readonly PyBuiltinRuntimeType TypingRuntimeType = new("typing._GenericAlias",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Cannot construct _GenericAlias directly", span));
    public object Origin { get; }
    public PyTuple Arguments { get; }
    public PyTuple Parameters { get; }
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) => OwnershipSnapshot.Owned(OwnerMemoryGovernor, _bytes, out bytes);

    public static PyGenericAlias Create(object origin, object index, MemoryGovernor governor, LythonSourceSpan span, ChargeReclamationPool? pool = null)
    {
        var arguments = index is PyTuple tuple ? tuple : new PyTuple([index], governor, span);
        if (index is not PyTuple) pool?.TrackFreshMutable(arguments, arguments.CommittedStorageBytes, span);
        var possibleParameters = arguments.Sum(value => value is PyTypeParameter ? 1 : value is PyGenericAlias alias ? alias.Parameters.Count :
            value is PyUnpackedType { Value: PyTypeParameter } ? 1 : 0);
        var bytes = checked(128L + 64L * (arguments.Count + possibleParameters));
        governor.Reserve(bytes, span); governor.Commit(bytes);
        PyGenericAlias created;
        try { created = new PyGenericAlias(origin, arguments, governor, span, bytes, pool); }
        catch { governor.Release(bytes); throw; }
        pool?.TrackFreshMutable(created, created._bytes, span);
        return created;
    }
    private PyGenericAlias(object origin, PyTuple arguments, MemoryGovernor governor, LythonSourceSpan span, long bytes, ChargeReclamationPool? pool)
    {
        Origin = origin; Arguments = arguments; OwnerMemoryGovernor = governor; AllocationSpan = span; _pool = pool;
        var parameters = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var argument in arguments)
        {
            if (argument is PyTypeParameter parameter && seen.Add(parameter)) parameters.Add(parameter);
            if (argument is PyUnpackedType { Value: PyTypeParameter unpacked } && seen.Add(unpacked)) parameters.Add(unpacked);
            if (argument is PyGenericAlias alias)
                foreach (var nested in alias.Parameters) if (seen.Add(nested)) parameters.Add(nested);
        }
        Parameters = parameters.Count == 0 ? PyTuple.Empty : new PyTuple(parameters, governor, span);
        _bytes = bytes + Parameters.CommittedStorageBytes;
    }
    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (Parameters.Count == 0) throw new LythonRuntimeException("TypeError", "Alias is not generic", span);
        using var scratch = OwnerMemoryGovernor!.ReserveTemporary(128L + 128L * (Parameters.Count + Arguments.Count), span);
        var replacements = index is PyTuple tuple ? tuple : new PyTuple([index], OwnerMemoryGovernor!, span);
        if (replacements.Count != Parameters.Count) throw new LythonRuntimeException("TypeError", "Incorrect number of generic arguments", span);
        var values = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < Parameters.Count; i++) values[Parameters[i]] = replacements[i];
        var substituted = new PyTuple(Arguments.Select(Substitute), OwnerMemoryGovernor!, span);
        _pool?.TrackFreshMutable(substituted, substituted.CommittedStorageBytes, span);
        return Create(Origin, substituted, OwnerMemoryGovernor!, span, _pool);

        object Substitute(object argument)
        {
            if (values.TryGetValue(argument, out var replacement)) return replacement;
            if (argument is PyGenericAlias nested)
            {
                var nestedArguments = new PyTuple(nested.Arguments.Select(Substitute), OwnerMemoryGovernor!, span);
                _pool?.TrackFreshMutable(nestedArguments, nestedArguments.CommittedStorageBytes, span);
                return Create(nested.Origin, nestedArguments, OwnerMemoryGovernor!, span, _pool);
            }
            return argument;
        }
    }
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch { "__origin__" => Origin, "__args__" => Arguments, "__parameters__" => Parameters, _ => PyNone.Instance };
        return value is not PyNone;
    }
    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeAsyncCore(arguments, span, context, false).GetAwaiter().GetResult();
    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeAsyncCore(arguments, span, context, true);
    private async ValueTask<object> InvokeAsyncCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (Origin is not LythonRuntime.ICallable callable) throw new LythonRuntimeException("TypeError", "Generic alias origin is not callable", span);
        var value = asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false) : callable.Invoke(arguments, span, context);
        if (value is PyInstance instance) { instance.SetAttribute("__orig_class__", this); context.Services.State.CallTemporaries.TrackGrowth(instance, instance.CommittedAttributeBytes, span); }
        return value;
    }
    public IEnumerable<object> Iterate() { yield return _unpacked ??= new PyUnpackedType(this); }
    public int GetPyHashCode() => HashCode.Combine(Origin is IPyHashableValue hashable ? hashable.GetPyHashCode() : RuntimeHelpers.GetHashCode(Origin), Arguments.GetPyHashCode());
    public override bool Equals(object? other) => other is PyGenericAlias alias && ReferenceEquals(Origin, alias.Origin) && PyEquality.AreEqual(Arguments, alias.Arguments);
    public override int GetHashCode() => GetPyHashCode();
    public PyString RenderPython(PyRenderingContext context)
    {
        using var traversal = PyStructuralGuard.EnterSingle(this, AllocationSpan, context.Context);
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, AllocationSpan);
        try
        {
            var union = ReferenceEquals(Origin, UnionOrigin);
            if (!union) { builder.AppendString(RenderArgument(Origin, context)); builder.Append((byte)'['); }
            for (var i = 0; i < Arguments.Count; i++)
            {
                context.Context.CheckExecutionBudget(AllocationSpan);
                if (i != 0) builder.AppendAscii(union ? " | " : ", ");
                builder.AppendString(union && ReferenceEquals(Arguments[i], PyType.NoneType) ? "None" : RenderArgument(Arguments[i], context));
            }
            if (!union) builder.Append((byte)']');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    internal static string RenderArgument(object value, PyRenderingContext context) => value switch
    {
        PyType type => type.Name,
        PyTypeAlias alias => alias.Name,
        PyBuiltinRuntimeType type => type.Name,
        INamedRuntimeCallable callable => callable.Name,
        PyNone => "None",
        PyString text => PyRendering.ToReprPyString(text, context).AsString(),
        _ => PyRendering.ToPythonString(value, context),
    };
}
