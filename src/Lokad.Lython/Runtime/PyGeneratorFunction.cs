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

    internal LythonRuntime.ExecutionContext DefinitionContext => Closure;
    internal LythonRuntime.ExecutableCaptures Captures => _closureCells;

    private bool CanDeferStagedFrame => CodeObject.UsesOnlyLexicalSlots &&
        !CodeObject.RequiresLocalVariableMirroring && !CodeObject.ScopeFacts.RequiresClassCell &&
        CodeObject.ScopeFacts.NonlocalNames.Count == 0 && CodeObject.CapturedLocalSlots.Count == 0 &&
        CodeObject.PatternStorageCount == 0 && CodeObject.ExceptionRegions.Count == 0 &&
        !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope && Closure.ClassCell is null &&
        Closure.ActiveHandlerVariables is not { Count: > 0 };

    internal object CreateBoundGenerator(BoundFunctionArguments arguments, LythonSourceSpan span,
        PyStarmapIterator? consumer = null)
    {
        LythonRuntime.ExecutionContext? frame = null;
        var entered = false;
        var deferred = false;
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
                if (consumer is not null && CanDeferStagedFrame)
                {
                    governor.Commit(bytes);
                    consumer.StageGenerator(new(null, span, this, arguments));
                    deferred = true;
                    return PyNone.Instance;
                }

                frame = EnterInvocationFrame(arguments, span, enterFunctionCall: false);
                if (consumer is null)
                {
                    var generator = new LythonRuntime.PyGenerator(CodeObject, frame, span, _closureCells, arguments, this);
                    governor.Commit(bytes);
                    return generator;
                }
                frame.InitializeGeneratorExecution(CodeObject, arguments, _closureCells, this);
                governor.Commit(bytes);
                consumer.StageGenerator(new(frame, span));
                return PyNone.Instance;
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
            finally { if (!deferred) ReleaseBoundInvocation(arguments, frame); }
        }
    }

    internal LythonRuntime.ExecutionContext CreateAdmittedFrame(BoundFunctionArguments arguments, LythonSourceSpan span)
    {
        var frame = EnterInvocationFrame(arguments, span, enterFunctionCall: false);
        frame.InitializeGeneratorExecution(CodeObject, arguments, _closureCells, this);
        return frame;
    }
}
