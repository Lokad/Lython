using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : PyFunctionBase
{

    private readonly ExecutableCodeObject _codeObject;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell> _closureCells;
    private readonly bool _argumentsAreLocals;

    public PyExecutableFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        IReadOnlyList<LythonRuntime.ExecutableCell> closureCells,
        IReadOnlyDictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameters, closure, defaultValues, scopeFacts, callableKind,
            storageCapacity: codeObject.RequiresLocalVariableMirroring ? null : codeObject.ValueStorageCapacity)
    {
        _codeObject = codeObject;
        _closureCells = closureCells;
        SupportsDirectPositional = !_codeObject.RequiresLocalVariableMirroring && SupportsPositionalBinding;
        // Compiler parameter slots are the binding-layout prefix. The invocation
        // owns the full local-capacity array through completion.
        // Captures use separate cells; exposed locals copy values, never the array.
        _argumentsAreLocals = !codeObject.RequiresLocalVariableMirroring;
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    internal bool SupportsDirectPositional { get; }

    internal ExecutableCodeObject CodeObject => _codeObject;
    internal IReadOnlyList<LythonRuntime.ExecutableCell> ClosureCells => _closureCells;

    internal override bool TryExecuteWithoutFrame(ReadOnlySpan<object?> arguments, LythonSourceSpan span, out object result)
    {
        // Closed native bodies cannot observe a function namespace or suspend.
        // Class and nested definitions keep their lexical/class-cell machinery.
        if (_codeObject.NativeBody is null || OwnerType is not null || Closure.ParentContext is not null
            || Closure.IsClassBody || Closure.IsAnnotationScope || Closure.ClassCell is not null
            || Closure.InlinedComprehensionLookup is not null || Closure.ActiveHandlerVariables is { Count: > 0 })
        {
            result = PyNone.Instance;
            return false;
        }
        return LythonRuntime.TryExecuteNativeModuleFunction(_codeObject, Closure, arguments, Name, span, out result);
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        if (LythonRuntime.TryExecuteNativeReturn(_codeObject, frame, boundArguments.Values.AsSpan(0, boundArguments.Assigned.Length), span, out var result))
            return result;
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells, _argumentsAreLocals, span)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        // Only statically proven bodies without callbacks use this shared shortcut.
        if (LythonRuntime.TryExecuteNativeReturn(_codeObject, frame, boundArguments.Values.AsSpan(0, boundArguments.Assigned.Length), span, out var result))
            return result;
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _closureCells, _argumentsAreLocals, span).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
