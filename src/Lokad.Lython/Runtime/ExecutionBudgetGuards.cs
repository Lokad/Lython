namespace Lokad.Lython.Runtime;

internal sealed class ExecutionBudgetGuards
{
    public ExecutionBudgetGuards(ExecutionState state)
    {
        State = state;
    }

    public ExecutionState State { get; }

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    public void CheckExecutionBudget(LythonSourceSpan? span)
    {
        Limits.ExecutionStepCount++;
        if (Limits.MaxExecutionSteps is { } maxExecutionSteps &&
            Limits.ExecutionStepCount > maxExecutionSteps)
        {
            throw RuntimeErrors.Runtime($"maximum execution step count exceeded ({maxExecutionSteps})", span);
        }

        if (Limits.CancellationToken.IsCancellationRequested)
        {
            throw RuntimeErrors.Runtime("execution canceled", span);
        }
    }

    public void RegisterHostCall(LythonSourceSpan? span)
    {
        CheckExecutionBudget(span);

        Limits.HostCallCount++;
        if (Limits.MaxHostCalls is { } maxHostCalls && Limits.HostCallCount > maxHostCalls)
        {
            throw RuntimeErrors.Runtime($"maximum host call count exceeded ({maxHostCalls})", span);
        }
    }

    public void EnterFunctionCall(LythonSourceSpan? span)
    {
        CheckExecutionBudget(span);
        Limits.CurrentRecursionDepth++;
        if (Limits.MaxRecursionDepth is { } maxRecursionDepth &&
            Limits.CurrentRecursionDepth > maxRecursionDepth)
        {
            Limits.CurrentRecursionDepth--;
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }
    }

    public void LeaveFunctionCall()
    {
        if (Limits.CurrentRecursionDepth > 0)
        {
            Limits.CurrentRecursionDepth--;
        }
    }

    public void EnterInterpreterFrame(LythonSourceSpan? span)
    {
        CheckExecutionBudget(span);
        Limits.CurrentInterpreterDepth++;
        if (Limits.CurrentInterpreterDepth > LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
        {
            Limits.CurrentInterpreterDepth--;
            throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
        }
    }

    public void LeaveInterpreterFrame()
    {
        if (Limits.CurrentInterpreterDepth > 0)
        {
            Limits.CurrentInterpreterDepth--;
        }
    }
}
