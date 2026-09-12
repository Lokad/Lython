using System.Runtime.InteropServices;

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

        EnsureStackForNesting(span, isFunctionCall: true);
    }

    // Stack-probe backstop (MG25): the depth caps above cannot trip first
    // when the CLR stack is nearly exhausted (each nesting level burns
    // kilobytes while the caps allow hundreds of levels), so nesting past the
    // probe threshold also proves real stack headroom before deepening. Every
    // execution path that can nest Python calls (sync/async,
    // executable/lowered) funnels through the two Enter methods above, and
    // async continuations resume with fresh stacks but preserved counters, so
    // each hop re-proves its own headroom. The ruler observes without
    // consuming, so a trip already runs with the full measured headroom
    // available for raising. (The guard API TryEnsureSufficientExecutionStack
    // was measured to never throw down to true exhaustion on this runtime, so
    // burning stack toward it cannot work; only direct telemetry protects.)
    private void EnsureStackForNesting(LythonSourceSpan? span, bool isFunctionCall)
    {
        if (Limits.CurrentRecursionDepth <= LythonRuntime.ExecutionLimits.StackProbeDepthThreshold &&
            Limits.CurrentInterpreterDepth <= LythonRuntime.ExecutionLimits.StackProbeDepthThreshold)
        {
            return;
        }

        if (StackRuler.RemainingBytes() >= LythonRuntime.ExecutionLimits.StackProbeHeadroomBytes)
        {
            return;
        }

        // Only the counter this Enter incremented is released: every call site
        // Enters before its try, so no matching Leave runs for an Enter-time
        // throw (mirroring the cap paths above). The trip error converges to
        // whichever cap would have tripped first instead of depending on host
        // stack size.
        if (isFunctionCall)
        {
            Limits.CurrentRecursionDepth--;
        }
        else
        {
            Limits.CurrentInterpreterDepth--;
        }

        throw RecursionTripsFirst()
            ? RuntimeErrors.Recursion("maximum recursion depth exceeded", span)
            : RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
    }

    // Predicts which counter cap would trip first under continued linear
    // growth: (capR - rec) further function Enters accrue about
    // (capR - rec) * interp/rec interpreter Enters at the observed ratio.
    private bool RecursionTripsFirst()
    {
        if (Limits.MaxRecursionDepth is not { } maxRecursionDepth)
        {
            return false;
        }

        var recursionDepth = Limits.CurrentRecursionDepth;
        var interpreterDepth = Limits.CurrentInterpreterDepth;
        return (maxRecursionDepth - recursionDepth) * interpreterDepth <
            (LythonRuntime.ExecutionLimits.MaxInterpreterDepth - interpreterDepth) * Math.Max(recursionDepth, 1);
    }

    // Direct stack telemetry for the probe above. Safe C# exposes no stack
    // pointer, so the current address comes from a three-line unsafe read
    // (address only, never dereferenced) while the low limit comes from the
    // OS per thread; the limit is thread-constant, so it resolves once per
    // thread and every later probe is one lightweight read pair. All
    // mainstream runtimes grow the stack downward, which the subtraction
    // assumes. Any telemetry failure fails open (reports ample room, the
    // pre-probe status quo) rather than breaking funded depths; only a small
    // reading trips, which is always the safe direction. Readings are
    // range-checked (limit below the pointer, plausible size) so interop
    // surprises degrade to fail-open instead of mistripping.
    private static class StackRuler
    {
        [ThreadStatic]
        private static nuint t_stackLow;

        [ThreadStatic]
        private static nuint t_stackSize;
        [ThreadStatic]
        private static bool t_rulerResolved;

        internal static long RemainingBytes()
        {
            if (!t_rulerResolved)
            {
                if (!TryResolveStackRange(out t_stackLow, out t_stackSize))
                {
                    t_stackLow = 0;
                    t_stackSize = 0;
                }

                t_rulerResolved = true;
            }

            if (t_stackSize == 0)
            {
                return long.MaxValue;
            }

            var pointer = CurrentStackPointer();
            if (t_stackLow == 0 || pointer <= t_stackLow || pointer > t_stackLow + t_stackSize)
            {
                return long.MaxValue;
            }

            return (long)(pointer - t_stackLow);
        }

        private static unsafe nuint CurrentStackPointer()
        {
            byte marker = 0;
            return (nuint)(void*)&marker;
        }

        private static bool TryResolveStackRange(out nuint stackLow, out nuint stackSize)
        {
            stackLow = 0;
            stackSize = 0;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    GetCurrentThreadStackLimits(out var low, out var high);
                    if (low == 0 || high <= low)
                    {
                        return false;
                    }

                    stackLow = low;
                    stackSize = high - low;
                    return true;
                }

                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ||
                    OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
                {
                    return UnixPthread.TryGetStackRange(out stackLow, out stackSize);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
            {
            }

            return false;
        }

        [DllImport("kernel32.dll")]
        private static extern void GetCurrentThreadStackLimits(out nuint lowLimit, out nuint highLimit);

        // pthread resolution through an explicit library cascade: the library
        // name varies (libpthread stub vs integrated libc, libSystem on Apple),
        // so each candidate is tried until all four symbols resolve. Anything
        // unresolved fails open via the caller. Only plain-integer signatures
        // are used, so a signature mismatch can mistrip at worst, never corrupt.
        private static class UnixPthread
        {
            private static readonly object s_sync = new();
            private static bool s_initialized;
            private static nint s_handle;
            private static PthreadSelf? s_self;
            private static PthreadGetattrNp? s_getattr;
            private static PthreadAttrGetstack? s_getstack;
            private static PthreadAttrDestroy? s_destroy;

            internal static bool TryGetStackRange(out nuint stackLow, out nuint stackSize)
            {
                stackLow = 0;
                stackSize = 0;
                try
                {
                    if (!EnsureResolved() ||
                        s_self is null || s_getattr is null || s_getstack is null || s_destroy is null)
                    {
                        return false;
                    }

                    const int attrBytes = 128;
                    unsafe
                    {
                        byte* attr = stackalloc byte[attrBytes];
                        for (var i = 0; i < attrBytes; i++)
                        {
                            attr[i] = 0;
                        }

                        if (s_getattr(s_self(), (nint)attr) != 0)
                        {
                            return false;
                        }

                        try
                        {
                            nuint address = 0;
                            nuint size = 0;
                            if (s_getstack((nint)attr, &address, &size) != 0 || address == 0 || size < 64 * 1024)
                            {
                                return false;
                            }

                            stackLow = address;
                            stackSize = size;
                            return true;
                        }
                        finally
                        {
                            s_destroy((nint)attr);
                        }
                    }
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
                {
                    return false;
                }
            }

            private static bool EnsureResolved()
            {
                if (s_initialized)
                {
                    return s_handle != 0;
                }

                lock (s_sync)
                {
                    if (s_initialized)
                    {
                        return s_handle != 0;
                    }

                    s_initialized = true;
                    string[] candidates = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
                        ? ["libSystem.dylib", "libc.dylib", "libc"]
                        : ["libpthread.so.0", "libc.so.6", "libc.so", "libc"];
                    foreach (var candidate in candidates)
                    {
                        if (!NativeLibrary.TryLoad(candidate, out var handle))
                        {
                            continue;
                        }

                        if (!NativeLibrary.TryGetExport(handle, "pthread_self", out var self) ||
                            !NativeLibrary.TryGetExport(handle, "pthread_getattr_np", out var getattr) ||
                            !NativeLibrary.TryGetExport(handle, "pthread_attr_getstack", out var getstack) ||
                            !NativeLibrary.TryGetExport(handle, "pthread_attr_destroy", out var destroy))
                        {
                            continue;
                        }

                        s_handle = handle;
                        s_self = Marshal.GetDelegateForFunctionPointer<PthreadSelf>(self);
                        s_getattr = Marshal.GetDelegateForFunctionPointer<PthreadGetattrNp>(getattr);
                        s_getstack = Marshal.GetDelegateForFunctionPointer<PthreadAttrGetstack>(getstack);
                        s_destroy = Marshal.GetDelegateForFunctionPointer<PthreadAttrDestroy>(destroy);
                        return true;
                    }

                    return false;
                }
            }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate nuint PthreadSelf();

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int PthreadGetattrNp(nuint thread, nint attr);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private unsafe delegate int PthreadAttrGetstack(nint attr, nuint* stackAddress, nuint* stackSize);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int PthreadAttrDestroy(nint attr);
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

        EnsureStackForNesting(span, isFunctionCall: false);
    }

    public void LeaveInterpreterFrame()
    {
        if (Limits.CurrentInterpreterDepth > 0)
        {
            Limits.CurrentInterpreterDepth--;
        }
    }
}
