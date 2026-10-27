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
    IPyRenderableValue, IPyIterableValue, IPyHashableValue, IPyGovernedValue
{
    internal static readonly PyBuiltinRuntimeType TypeVarType = TypeObject("TypeVar", TypeParameterKind.TypeVar);
    internal static readonly PyBuiltinRuntimeType TypeVarTupleType = TypeObject("TypeVarTuple", TypeParameterKind.TypeVarTuple);
    internal static readonly PyBuiltinRuntimeType ParamSpecType = TypeObject("ParamSpec", TypeParameterKind.ParamSpec);
    internal static readonly PyNoTypeDefault NoDefault = new();
    private static PyBuiltinRuntimeType TypeObject(string name, TypeParameterKind kind)
        => new("typing." + name,
            (arguments, span, context) => LythonRuntime.ConstructTypeParameterAsync(kind, arguments, context, span, false).GetAwaiter().GetResult(),
            null,
            (arguments, span, context) => LythonRuntime.ConstructTypeParameterAsync(kind, arguments, context, span, true));

    private readonly PyString _name;
    private readonly object _module;
    private readonly PyLazyTypeValue? _bound, _default;
    private readonly object? _constantBound, _constantDefault;
    private readonly bool _constraints, _unpackDefault;
    private object? _unpacked, _unpackedDefault;
    public TypeParameterKind Kind { get; }
    public bool InferVariance { get; }
    public bool Covariant { get; }
    public bool Contravariant { get; }
    public bool HasDefault => _default is not null || _constantDefault is not null;
    public string Name => _name.AsString();
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }

    internal PyTypeParameter(string name, TypeParameterKind kind, bool inferVariance,
        PyLazyTypeValue? bound, bool constraints, PyLazyTypeValue? defaultValue, bool unpackDefault,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        object? constantBound = null, object? constantDefault = null, bool covariant = false, bool contravariant = false)
    {
        context.MemoryGovernor.Reserve(512, span);
        context.MemoryGovernor.Commit(512);
        try { _name = PyString.FromString(name, context.MemoryGovernor, span); }
        catch { context.MemoryGovernor.Release(512); throw; }
        Kind = kind; InferVariance = inferVariance; Covariant = covariant; Contravariant = contravariant;
        _bound = bound; _constraints = constraints; _default = defaultValue; _unpackDefault = unpackDefault;
        _constantBound = constantBound; _constantDefault = constantDefault;
        _module = PyFunctionBase.TryGetModuleName(context, out var module) ? module : PyNone.Instance;
        OwnerMemoryGovernor = context.MemoryGovernor; AllocationSpan = span;
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
            case "__infer_variance__" when Kind != TypeParameterKind.TypeVarTuple: return (true, InferVariance);
            case "__covariant__" when Kind != TypeParameterKind.TypeVarTuple: return (true, Covariant);
            case "__contravariant__" when Kind != TypeParameterKind.TypeVarTuple: return (true, Contravariant);
            case "__bound__" when Kind != TypeParameterKind.TypeVarTuple:
                return (true, _constraints ? PyNone.Instance : _bound is null ? _constantBound ?? PyNone.Instance : await _bound.GetAsync(asynchronous).ConfigureAwait(false));
            case "__constraints__" when Kind == TypeParameterKind.TypeVar:
                return (true, !_constraints ? PyTuple.Empty : _bound is null ? _constantBound! : await _bound.GetAsync(asynchronous).ConfigureAwait(false));
            case "__default__":
                var result = _default is null ? _constantDefault ?? NoDefault : await _default.GetAsync(asynchronous).ConfigureAwait(false);
                return (true, _unpackDefault ? _unpackedDefault ??= new PyUnpackedType(result) : result);
            case "has_default":
                context.MemoryGovernor.Reserve(96, span); context.MemoryGovernor.Commit(96);
                var method = new HasDefaultMethod(this, context.MemoryGovernor, span);
                return (true, method);
            case "args" when Kind == TypeParameterKind.ParamSpec: return (true, PyParamSpecPart.Create(this, true, context, span));
            case "kwargs" when Kind == TypeParameterKind.ParamSpec: return (true, PyParamSpecPart.Create(this, false, context, span));
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
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, AllocationSpan);
        try
        {
            if (!InferVariance && Kind != TypeParameterKind.TypeVarTuple)
                builder.AppendAscii(Covariant ? "+" : Contravariant ? "-" : "~");
            builder.Append(_name);
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    private sealed class HasDefaultMethod(PyTypeParameter parameter, MemoryGovernor governor, LythonSourceSpan span) : LythonRuntime.ICallable,
        LythonRuntime.IPyBoundEngineMethod, IPyGovernedValue
    {
        public MemoryGovernor? OwnerMemoryGovernor => governor;
        public LythonSourceSpan? AllocationSpan => span;

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
    public PyString RenderPython(PyRenderingContext context)
    {
        using var traversal = PyStructuralGuard.EnterSingle(this, null, context.Context);
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        try
        {
            builder.AppendAscii(Value is PyTypeParameter ? "typing.Unpack[" : "*");
            PyGenericAlias.AppendArgument(builder, Value, context);
            if (Value is PyTypeParameter) builder.Append((byte)']');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__origin__" => PyTyping.Unpack,
            "__args__" => new PyTuple([Value]),
            _ => PyNone.Instance,
        };
        return value is not PyNone;
    }
}

internal sealed record PyParamSpecPart(PyTypeParameter Parameter, bool Positional) : IPyRenderableValue, IPyDynamicAttributes,
    IPyGovernedValue
{
    public MemoryGovernor? OwnerMemoryGovernor { get; private init; }
    public LythonSourceSpan? AllocationSpan { get; private init; }
    internal static PyParamSpecPart Create(PyTypeParameter parameter, bool positional, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.Reserve(96, span); context.MemoryGovernor.Commit(96);
        var value = new PyParamSpecPart(parameter, positional) { OwnerMemoryGovernor = context.MemoryGovernor, AllocationSpan = span };
return value;
    }
    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Parameter.Name + (Positional ? ".args" : ".kwargs"), context.Context.MemoryGovernor, AllocationSpan);
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) { value = name == "__origin__" ? Parameter : PyNone.Instance; return value is not PyNone; }
    public bool Equals(PyParamSpecPart? other) => other is not null && ReferenceEquals(Parameter, other.Parameter) && Positional == other.Positional;
    public override int GetHashCode() => HashCode.Combine(Parameter, Positional);
}

internal sealed class PyTypeAlias : IPyContextualDynamicAttributes, IPyAsyncDynamicAttributes, IPySubscriptableValue,
    IPyRenderableValue, IPyHashableValue, IPyGovernedValue
{
    internal static readonly PyBuiltinRuntimeType RuntimeType = new("typing.TypeAliasType",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Use a type statement to define a type alias in Lython.", span));
    private readonly PyString _name;
    private readonly object _module;
    private readonly PyLazyTypeValue _value;
    public string Name => _name.AsString();
    internal PyString NameValue => _name;
    public PyTuple TypeParameters { get; }
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }

    internal PyTypeAlias(string name, PyTuple parameters, LoweredExpression value, LythonRuntime.ExecutionContext scope, LythonSourceSpan span)
    {
        scope.MemoryGovernor.Reserve(512, span);
        scope.MemoryGovernor.Commit(512);
        try { _name = PyString.FromString(name, scope.MemoryGovernor, span); }
        catch { scope.MemoryGovernor.Release(512); throw; }
        TypeParameters = parameters; _value = new(value, scope);         _module = PyFunctionBase.TryGetModuleName(scope, out var module) ? module : PyNone.Instance;
        OwnerMemoryGovernor = scope.MemoryGovernor; AllocationSpan = span;
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
        return PyGenericAlias.Create(this,index,OwnerMemoryGovernor!,span);
    }
    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    public PyString RenderPython(PyRenderingContext context) => _name;
    public PyString RenderInterpolated(PyRenderingContext context) => _name;
}
