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

    internal bool SharesExecutionWith(LythonRuntime.ExecutionContext context)
        => ReferenceEquals(Closure.State, context.State);

    internal object CreateBoundGenerator(BoundFunctionArguments arguments, LythonSourceSpan span, bool staged = false)
    {
        LythonRuntime.ExecutionContext? frame = null;
        var entered = false;
        try
        {
            Closure.EnterFunctionCall(span);
            entered = true;
            var governor = Closure.MemoryGovernor;
            var bytes = LythonRuntime.PyGenerator.EstimateStorage(CodeObject);
            governor.Reserve(bytes, span);
            try
            {
                Closure.CheckExecution(span);
                frame = EnterInvocationFrame(arguments, span, enterFunctionCall: false);
                var generator = staged
                    ? InitializeStagedFrame(frame, arguments)
                    : (object)new LythonRuntime.PyGenerator(CodeObject, frame, span, _closureCells, arguments, this);
                governor.Commit(bytes);
                return generator;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }
        catch (LythonRuntimeException error) when (entered)
        {
            PyFunctionBinding.AnnotateException(error, frame ?? Closure, Name, span);
            throw;
        }
        finally
        {
            try { if (entered && frame is null) Closure.LeaveFunctionCall(); }
            finally { ReleaseBoundInvocation(arguments, frame); }
        }
    }

    private LythonRuntime.ExecutionContext InitializeStagedFrame(
        LythonRuntime.ExecutionContext frame, BoundFunctionArguments arguments)
    {
        frame.InitializeGeneratorExecution(CodeObject, arguments, _closureCells, this);
        return frame;
    }
}
