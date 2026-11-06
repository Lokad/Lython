using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

// N01: execution-local traversal state for async comparisons.
// Each run owns one StructuralGuardState through ExecutionState, carried
// explicitly via ExecutionContext through async recursion. Thread-static
// storage remains only for context-free synchronous paths that never span
// an await. Async scopes must use the overloads taking an explicit state so
// disposal removes entries from the same state that was entered, regardless
// of which pool thread resumes the continuation. Sharing one mutable path
// across runs is never done: states are per-run, never stored in AsyncLocal,
// and sequential execution within a run never mutates them concurrently.
internal sealed class StructuralGuardState
{

    internal int Depth;
    // Only identities on the active structural path, never a guest inventory.
    // Existing managed admission bounds its length. A null right denotes a
    // single-object traversal; pair and single cycles remain independent.
    internal List<PyStructuralGuard.TraversalEntry>? ActivePath;
    internal int WorkTick;

    internal bool ContainsSingle(object value)
    {
        if (ActivePath is { } path)
            for (var index = path.Count - 1; index >= 0; index--)
            {
                var entry = path[index];
                if (entry.Right is null && ReferenceEquals(entry.Left, value)) return true;
            }
        return false;
    }
}

internal static class PyStructuralGuard
{
    [ThreadStatic]
    private static StructuralGuardState? t_threadState;

    [ThreadStatic]
    private static LythonRuntime.ExecutionContext? t_ambientContext;
    [ThreadStatic]
    private static LythonSourceSpan? t_ambientSpan;

    private static StructuralGuardState ThreadState => t_threadState ??= new StructuralGuardState();

    internal readonly struct Scope : IDisposable
    {
        private readonly StructuralGuardState? _state;
        private readonly object? _left;
        private readonly object? _right;

        internal Scope(StructuralGuardState state, object? left = null, object? right = null)
        {
            _state = state;
            _left = left;
            _right = right;
        }

        public void Dispose()
        {
            if (_state is null)
            {
                return;
            }

            if (_left is not null && _state.ActivePath is { } path)
            {
                // Normal lexical unwind finds the final entry immediately.
                // Retain out-of-order disposal without storing invalidatable
                // indices in scopes that can resume on another thread.
                for (var index = path.Count - 1; index >= 0; index--)
                {
                    var entry = path[index];
                    if (ReferenceEquals(entry.Left, _left) && ReferenceEquals(entry.Right, _right))
                    {
                        path.RemoveAt(index);
                        break;
                    }
                }
            }

            if (_state.Depth > 0)
            {
                _state.Depth--;
            }
        }
    }

    internal readonly struct TraversalEntry(object left, object? right)
    {
        internal readonly object Left = left;
        internal readonly object? Right = right;
    }

    internal static Scope EnterPair(object left, object right, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var state = context?.State.StructuralTraversal ?? ThreadState;
        return EnterPairCore(state, left, right, span, context);
    }

    internal static Scope EnterPair(StructuralGuardState state, object left, object right, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EnterPairCore(state, left, right, span, context);
    }

    private static Scope EnterPairCore(StructuralGuardState state, object left, object right, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        if (ReferenceEquals(left, right))
        {
            return default;
        }

        CheckDepthAndStack(state, span, context, comparison: true);

        var path = state.ActivePath ??= new();
        for (var index = path.Count - 1; index >= 0; index--)
        {
            var entry = path[index];
            if (ReferenceEquals(entry.Left, left) && ReferenceEquals(entry.Right, right)
                || ReferenceEquals(entry.Left, right) && ReferenceEquals(entry.Right, left))
                throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
        }

        path.Add(new TraversalEntry(left, right));
        state.Depth++;
        return new Scope(state, left, right);
    }

    internal static Scope EnterSingle(object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var state = context?.State.StructuralTraversal ?? ThreadState;
        return EnterSingleCore(state, value, span, context);
    }

    internal static Scope EnterSingle(StructuralGuardState state, object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EnterSingleCore(state, value, span, context);
    }

    private static Scope EnterSingleCore(StructuralGuardState state, object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        CheckDepthAndStack(state, span, context, comparison: false);

        if (state.ContainsSingle(value))
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);

        var path = state.ActivePath ??= new();
        path.Add(new TraversalEntry(value, null));
        state.Depth++;
        return new Scope(state, value);
    }

    internal static Scope EnterDepth(LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var state = context?.State.StructuralTraversal ?? ThreadState;
        CheckDepthAndStack(state, span, context, comparison: false);
        state.Depth++;
        return new Scope(state);
    }

    private static void CheckDepthAndStack(StructuralGuardState state, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, bool comparison)
    {
        var message = comparison ? "maximum recursion depth exceeded in comparison" : "maximum recursion depth exceeded";
        var limit = ExecutionState.MaxInterpreterDepth;
        if (context?.State.MaxRecursionDepth is { } maxRec && maxRec < limit)
        {
            limit = maxRec;
        }

        if (state.Depth >= limit)
        {
            throw RuntimeErrors.Attributed("RecursionError", message, span,
                limit < ExecutionState.MaxInterpreterDepth ? LythonFailureReason.RecursionDepthLimit : LythonFailureReason.InterpreterStackLimit,
                limit, state.Depth, state.Depth + 1L);
        }

        if (context is not null)
        {
            var recBudget = context.State.MaxRecursionDepth ?? ExecutionState.MaxInterpreterDepth;
            if (context.State.CurrentRecursionDepth + state.Depth >= recBudget)
            {
                throw RuntimeErrors.Attributed("RecursionError", message, span,
                    context.State.MaxRecursionDepth.HasValue ? LythonFailureReason.RecursionDepthLimit : LythonFailureReason.InterpreterStackLimit,
                    recBudget, context.State.CurrentRecursionDepth + state.Depth, context.State.CurrentRecursionDepth + state.Depth + 1L);
            }

            if (context.State.CurrentInterpreterDepth + state.Depth >= ExecutionState.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Attributed("RuntimeError", "maximum interpreter stack depth exceeded", span,
                    LythonFailureReason.InterpreterStackLimit, ExecutionState.MaxInterpreterDepth,
                    context.State.CurrentInterpreterDepth + state.Depth, context.State.CurrentInterpreterDepth + state.Depth + 1L);
            }
        }

        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw RuntimeErrors.Attributed("RecursionError", message, span,
                LythonFailureReason.InterpreterStackLimit);
        }

    }

    internal readonly struct AmbientScope : IDisposable
    {
        private readonly LythonRuntime.ExecutionContext? _previousContext;
        private readonly LythonSourceSpan? _previousSpan;
        private readonly bool _active;

        internal AmbientScope(LythonRuntime.ExecutionContext? previousContext, LythonSourceSpan? previousSpan, bool active)
        {
            _previousContext = previousContext;
            _previousSpan = previousSpan;
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
            {
                return;
            }

            t_ambientContext = _previousContext;
            t_ambientSpan = _previousSpan;
        }
    }

    // Ambient comparison provenance (R13/R16): structural positions reached under
    // an operator or member comparison may dispatch guest __eq__ through this
    // context; context-free paths (CLR comparer callbacks below, hashing, sorts)
    // see null and stay structural. Guest dispatch is additionally suppressed
    // while a CLR comparer callback runs, so reentrant lookups from inside
    // __eq__ never execute guest code through comparer internals.
    // N01: ambient remains thread-static and must never span an await. Async
    // structural traversals carry ExecutionContext explicitly instead.
    internal static LythonRuntime.ExecutionContext? AmbientContext => t_ambientContext;

    internal static LythonSourceSpan? AmbientSpan => t_ambientSpan;

    [ThreadStatic]
    private static int t_suppressGuestDispatch;

    internal static bool GuestDispatchSuppressed => t_suppressGuestDispatch > 0;

    internal readonly struct ComparerScope : IDisposable
    {
        public void Dispose()
        {
            if (t_suppressGuestDispatch > 0)
            {
                t_suppressGuestDispatch--;
            }
        }
    }

    internal static ComparerScope SuppressGuestDispatch()
    {
        t_suppressGuestDispatch++;
        return new ComparerScope();
    }

    internal static AmbientScope PushAmbient(LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var prevContext = t_ambientContext;
        var prevSpan = t_ambientSpan;
        // Nested pushes preserve the outer ambient; only the outermost budget matters.
        // Overwrite unconditionally and restore on dispose so reentrant structural work
        // (for example __eq__ calling back into ==) keeps the innermost span for diagnostics.
        t_ambientContext = context;
        t_ambientSpan = span;
        return new AmbientScope(prevContext, prevSpan, active: true);
    }

    internal static void NoteWork()
    {
        var context = t_ambientContext;
        if (context is null)
        {
            return;
        }

        var state = context.State.StructuralTraversal;
        // Periodic cooperative checks for long structural scans: the outer expression
        // performs a single budget check on entry, which cannot stop a million-element
        // scan that never nests deeply. Throttle to every 64 elements to keep
        // per-element overhead negligible while remaining responsive to cancellation.
        if ((++state.WorkTick & 63) != 0)
        {
            return;
        }

        context.CheckExecution(t_ambientSpan);
    }

    internal static void NoteWork(LythonRuntime.ExecutionContext? explicitContext, LythonSourceSpan? explicitSpan)
    {
        var context = explicitContext ?? t_ambientContext;
        if (context is null)
        {
            return;
        }

        // Ambient and explicit calls advance the same execution-owned interval;
        // previous executions on this thread must not shift its cancellation checks.
        var state = context.State.StructuralTraversal;
        if ((++state.WorkTick & 63) != 0)
        {
            return;
        }

        context.CheckExecution(explicitSpan ?? t_ambientSpan);
    }

    internal static void NoteWork(StructuralGuardState state, LythonRuntime.ExecutionContext? explicitContext, LythonSourceSpan? explicitSpan)
    {
        ArgumentNullException.ThrowIfNull(state);
        var context = explicitContext ?? t_ambientContext;
        if (context is null)
        {
            return;
        }

        if ((++state.WorkTick & 63) != 0)
        {
            return;
        }

        context.CheckExecution(explicitSpan ?? t_ambientSpan);
    }

    // Test hook: thread-static depth for sync paths (async pair tracking lives
    // per-run and never touches thread state).
    internal static int ThreadDepthForTests => ThreadState.Depth;

    internal static int ThreadPairCountForTests
    {
        get
        {
            var count = 0;
            if (ThreadState.ActivePath is { } path)
                foreach (var entry in path)
                    if (entry.Right is not null) count++;
            return count;
        }
    }
}
