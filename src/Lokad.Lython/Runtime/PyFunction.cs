using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyFunction : PyFunctionBase
{
    private readonly IReadOnlyList<LoweredStatement> _body;

    public PyFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyList<LoweredStatement> body,
        LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
        : base(name, parameters, closure, defaultValues, scopeFacts)
    {
        _body = body;
    }

    protected override bool RequiresArgumentMirroring => true;

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        _ = boundArguments;
        var flow = LythonRuntime.ExecuteStatements(_body, frame);
        if (flow.Return is not null)
        {
            return flow.Return.Value;
        }

        if (flow.Control is LythonRuntime.BreakSignal or LythonRuntime.ContinueSignal)
        {
            throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a function body.", span);
        }

        return PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        _ = boundArguments;
        var flow = await LythonRuntime.ExecuteStatementsAsync(_body, frame).ConfigureAwait(false);
        if (flow.Return is not null)
        {
            return flow.Return.Value;
        }

        if (flow.Control is LythonRuntime.BreakSignal or LythonRuntime.ContinueSignal)
        {
            throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a function body.", span);
        }

        return PyNone.Instance;
    }
}
