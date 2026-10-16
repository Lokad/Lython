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
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameters, closure, defaultValues, scopeFacts, callableKind)
    {
        _codeObject = codeObject;
        _closureCells = closureCells;
        SupportsDirectPositional = !_codeObject.RequiresLocalVariableMirroring && SupportsPositionalBinding;
        // Compiler parameter slots are the binding-layout prefix. The invocation
        // already owns this exact-size array through completion.
        // Captures use separate cells; exposed locals copy values, never the array.
        _argumentsAreLocals = !codeObject.RequiresLocalVariableMirroring &&
            BindingPlan.LayoutParameterNames.Count == codeObject.LocalNames.Count;
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    internal bool SupportsDirectPositional { get; }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        if (LythonRuntime.TryExecuteSimpleReturn(_codeObject, frame, boundArguments, span, out var result))
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
        if (LythonRuntime.TryExecuteSimpleReturn(_codeObject, frame, boundArguments, span, out var result))
            return result;
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _closureCells, _argumentsAreLocals, span).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
