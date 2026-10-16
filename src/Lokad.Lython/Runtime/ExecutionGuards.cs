using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionGuards
{

    public ExecutionGuards(ExecutionState state)
    {
        State = state;
    }

    public ExecutionState State { get; }

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CheckExecution(LythonSourceSpan? span)
    {
        State.MemoryGovernor.ThrowIfAdmissionFailed();
        var limits = Limits;
        if (limits.MaxExecutionSteps is { } maxExecutionSteps &&
            ++limits.ExecutionStepCount > maxExecutionSteps)
        {
            ThrowExecutionStepLimit(maxExecutionSteps, limits.ExecutionStepCount, span);
        }

        if (limits.CancellationToken.IsCancellationRequested)
        {
            ThrowExecutionCanceled(span);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExecutionStepLimit(long maximum, long observed, LythonSourceSpan? span)
        => throw RuntimeErrors.Attributed("RuntimeError", $"maximum execution step count exceeded ({maximum})", span,
            LythonFailureReason.ExecutionStepLimit, maximum, observed);

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExecutionCanceled(LythonSourceSpan? span)
        => throw RuntimeErrors.Cancelled(span);

    public void RegisterHostCall(LythonSourceSpan? span)
    {
        CheckExecution(span);

        Limits.HostCallCount++;
        if (Limits.MaxHostCalls is { } maxHostCalls && Limits.HostCallCount > maxHostCalls)
        {
            throw RuntimeErrors.Attributed("RuntimeError", $"maximum host call count exceeded ({maxHostCalls})", span,
                LythonFailureReason.HostCallLimit, maxHostCalls, Limits.HostCallCount);
        }
    }

    public void EnterFunctionCall(LythonSourceSpan? span)
    {
        CheckExecution(span);
        if (Limits.MaxRecursionDepth is { } maxRecursionDepth &&
            Limits.CurrentRecursionDepth >= maxRecursionDepth)
        {
            throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded", span,
                LythonFailureReason.RecursionDepthLimit, maxRecursionDepth, Limits.CurrentRecursionDepth,
                Limits.CurrentRecursionDepth + 1L);
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

        throw RuntimeErrors.Attributed("RuntimeError", "maximum interpreter stack depth exceeded", span,
            LythonFailureReason.InterpreterStackLimit);
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
            throw RuntimeErrors.Attributed("RuntimeError", "maximum interpreter stack depth exceeded", span,
                LythonFailureReason.InterpreterStackLimit, LythonRuntime.ExecutionLimits.MaxInterpreterDepth,
                Limits.CurrentInterpreterDepth, Limits.CurrentInterpreterDepth + 1L);
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
