using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed partial class ExecutionState
{
    public CancellationToken CancellationToken { get; }
    public int? MaxRecursionDepth { get; }
    // Preserve the validated managed nesting bound, independently of execution time.
    public const int MaxInterpreterDepth = 512;
    public long CurrentRecursionDepth { get; set; }
    public long CurrentInterpreterDepth { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CheckExecution(LythonSourceSpan? span)
    {
        MemoryGovernor.ThrowIfAdmissionFailed();
        if (CancellationToken.IsCancellationRequested)
        {
            ThrowExecutionCanceled(span);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExecutionCanceled(LythonSourceSpan? span)
        => throw RuntimeErrors.Cancelled(span);

    public void EnterFunctionCall(LythonSourceSpan? span)
    {
        CheckExecution(span);
        if (MaxRecursionDepth is { } maxRecursionDepth &&
            CurrentRecursionDepth >= maxRecursionDepth)
        {
            throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded", span,
                LythonFailureReason.RecursionDepthLimit, maxRecursionDepth, CurrentRecursionDepth,
                CurrentRecursionDepth + 1L);
        }

        EnsureInterpreterStack(span);
        CurrentRecursionDepth++;
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
        if (CurrentRecursionDepth > 0)
        {
            CurrentRecursionDepth--;
        }
    }

    public void EnterInterpreterFrame(LythonSourceSpan? span)
    {
        CheckExecution(span);
        if (CurrentInterpreterDepth >= MaxInterpreterDepth)
        {
            throw RuntimeErrors.Attributed("RuntimeError", "maximum interpreter stack depth exceeded", span,
                LythonFailureReason.InterpreterStackLimit, MaxInterpreterDepth,
                CurrentInterpreterDepth, CurrentInterpreterDepth + 1L);
        }

        EnsureInterpreterStack(span);
        CurrentInterpreterDepth++;
    }

    public void LeaveInterpreterFrame()
    {
        if (CurrentInterpreterDepth > 0)
        {
            CurrentInterpreterDepth--;
        }
    }
}
