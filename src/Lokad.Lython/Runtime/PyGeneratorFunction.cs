using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorFunction : PyFunctionBase
{
    private readonly LythonRuntime.ExecutableCaptures _closureCells;

    public PyGeneratorFunction(FunctionParameterLayout parameterLayout,
        ExecutableCodeObject code, LythonRuntime.ExecutionContext closure,
        FunctionDefaults defaults,
        LythonRuntime.ExecutableCaptures closureCells = default)
        : base(code, parameterLayout, closure, defaults)
    {
        _closureCells = closureCells;
    }

    protected override object ExecuteBody(LythonRuntime.ExecutionContext frame, BoundFunctionArguments arguments, LythonSourceSpan span)
        => LythonRuntime.PyGenerator.Create(CodeObject, frame, span, _closureCells, arguments);

    protected override ValueTask<object> ExecuteBodyAsync(LythonRuntime.ExecutionContext frame, BoundFunctionArguments arguments, LythonSourceSpan span)
        => new(ExecuteBody(frame, arguments, span));
}
