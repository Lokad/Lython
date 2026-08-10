using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : PyFunctionBase
{
    private readonly ExecutableCodeObject _codeObject;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell> _closureCells;

    public PyExecutableFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        IReadOnlyList<LythonRuntime.ExecutableCell> closureCells,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
        : base(name, parameters, closure, defaultValues, scopeFacts)
    {
        _codeObject = codeObject;
        _closureCells = closureCells;
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        IReadOnlyDictionary<string, object> boundArguments,
        LythonSourceSpan span)
    {
        _ = span;
        LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells);
        return PyNone.Instance;
    }
}
