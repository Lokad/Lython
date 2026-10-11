using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionGuards
{
    private int _checkpointsUntilSweep = 256;

    public ExecutionGuards(ExecutionState state)
    {
        State = state;
    }

    public ExecutionState State { get; }

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CheckExecution(LythonSourceSpan? span)
    {
        if (--_checkpointsUntilSweep == 0)
        {
            SweepCheckpointTemporaries();
        }
        var limits = Limits;
        if (limits.MaxExecutionSteps is { } maxExecutionSteps &&
            ++limits.ExecutionStepCount > maxExecutionSteps)
        {
            ThrowExecutionStepLimit(maxExecutionSteps, span);
        }

        if (limits.CancellationToken.IsCancellationRequested)
        {
            ThrowExecutionCanceled(span);
        }
    }

    // Keep sweep and exception construction out of the frequently executed
    // checkpoint body while retaining their original order and cadence.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SweepCheckpointTemporaries()
    {
        _checkpointsUntilSweep = 256;
        State.CallTemporaries.Sweep();
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExecutionStepLimit(long maximum, LythonSourceSpan? span)
        => throw RuntimeErrors.Runtime($"maximum execution step count exceeded ({maximum})", span);

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExecutionCanceled(LythonSourceSpan? span)
        => throw RuntimeErrors.Runtime("execution canceled", span);

    public void RegisterHostCall(LythonSourceSpan? span)
    {
        CheckExecution(span);

        Limits.HostCallCount++;
        if (Limits.MaxHostCalls is { } maxHostCalls && Limits.HostCallCount > maxHostCalls)
        {
            throw RuntimeErrors.Runtime($"maximum host call count exceeded ({maxHostCalls})", span);
        }
    }

    public void EnterFunctionCall(LythonSourceSpan? span)
    {
        CheckExecution(span);
        if (Limits.MaxRecursionDepth is { } maxRecursionDepth &&
            Limits.CurrentRecursionDepth >= maxRecursionDepth)
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }

        EnsureInterpreterStack(span);
        Limits.CurrentRecursionDepth++;
    }

    // The managed runtime checks the current stack on every nesting entry,
    // including entries on a host continuation. A false result trips before
    // descending; no native range cache or future-cap prediction is needed.
    internal static void EnsureInterpreterStack(LythonSourceSpan? span)
    {
        if (RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return;
        }

        throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
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
        CheckExecution(span);
        if (Limits.CurrentInterpreterDepth >= LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
        {
            throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
        }

        EnsureInterpreterStack(span);
        Limits.CurrentInterpreterDepth++;
    }

    public void LeaveInterpreterFrame()
    {
        if (Limits.CurrentInterpreterDepth > 0)
        {
            Limits.CurrentInterpreterDepth--;
        }
    }
}
