using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorFunction : PyFunctionBase
{
    private readonly ExecutableCodeObject _code;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell>? _closureCells;

    public PyGeneratorFunction(string name, FunctionParameterLayout parameterLayout,
        ExecutableCodeObject code, LythonRuntime.ExecutionContext closure,
        IReadOnlyDictionary<string, object> defaults, ScopeDirectiveFacts scopeFacts,
        IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameterLayout, closure, defaults, scopeFacts, callableKind)
    {
        _code = code;
        _closureCells = closureCells;
    }

    protected override bool RequiresArgumentMirroring => _code.RequiresLocalVariableMirroring;

    protected override object ExecuteBody(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => LythonRuntime.PyGenerator.Create(_code, frame, span, _closureCells, arguments);

    protected override ValueTask<object> ExecuteBodyAsync(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => new(ExecuteBody(frame, arguments, span));
}
