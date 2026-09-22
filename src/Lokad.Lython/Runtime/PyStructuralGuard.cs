using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

// N01: execution-local traversal state for async comparisons.
// Each run owns one StructuralGuardState through ExecutionState, carried
// explicitly via ExecutionContext through async recursion. Thread-static
// storage remains only for context-free synchronous paths that never span
// an await. Async scopes must use the overloads taking an explicit state so
// disposal removes entries from the same state that was entered, regardless
// of which pool thread resumes the continuation. Sharing one mutable set
// across runs is never done: states are per-run, never stored in AsyncLocal,
// and sequential execution within a run never mutates them concurrently.
internal sealed class StructuralGuardState
{
    internal int Depth;
    internal HashSet<PyStructuralGuard.StructuralPair>? Pairs;
    internal HashSet<object>? Singles;
    internal int WorkTick;
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
        private readonly StructuralPair _pair;
        private readonly object? _single;
        private readonly bool _isPair;
        private readonly bool _active;

        internal Scope(StructuralGuardState state, StructuralPair pair)
        {
            _state = state;
            _pair = pair;
            _single = null;
            _isPair = true;
            _active = true;
        }

        internal Scope(StructuralGuardState state, object single)
        {
            _state = state;
            _pair = default;
            _single = single;
            _isPair = false;
            _active = true;
        }

        public void Dispose()
        {
            if (!_active || _state is null)
            {
                return;
            }

            if (_isPair)
            {
                _state.Pairs?.Remove(_pair);
            }
            else if (_single is not null)
            {
                _state.Singles?.Remove(_single);
            }

            if (_state.Depth > 0)
            {
                _state.Depth--;
            }
        }
    }

    internal readonly record struct StructuralPair(object Left, object Right);

    private sealed class StructuralPairComparer : IEqualityComparer<StructuralPair>
    {
        public static readonly StructuralPairComparer Instance = new();

        public bool Equals(StructuralPair x, StructuralPair y)
            => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);

        public int GetHashCode(StructuralPair obj)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Left), RuntimeHelpers.GetHashCode(obj.Right));
    }

    internal static Scope EnterPair(object left, object right, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var state = context?.Services.State.StructuralTraversal ?? ThreadState;
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

        var limit = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        if (context?.Limits.MaxRecursionDepth is { } maxRec && maxRec < limit)
        {
            limit = maxRec;
        }

        if (state.Depth >= limit)
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
        }

        if (context is not null)
        {
            var recBudget = context.Limits.MaxRecursionDepth ?? LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
            if (context.Limits.CurrentRecursionDepth + state.Depth >= recBudget)
            {
                throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
            }

            if (context.Limits.CurrentInterpreterDepth + state.Depth >= LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
            }
        }

        var pairs = state.Pairs ??= new HashSet<StructuralPair>(StructuralPairComparer.Instance);
        var pair = new StructuralPair(left, right);
        var swapped = new StructuralPair(right, left);
        if (pairs.Contains(pair) || pairs.Contains(swapped))
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
        }

        state.Depth++;
        pairs.Add(pair);
        return new Scope(state, pair);
    }

    internal static Scope EnterSingle(object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var state = context?.Services.State.StructuralTraversal ?? ThreadState;
        return EnterSingleCore(state, value, span, context);
    }

    internal static Scope EnterSingle(StructuralGuardState state, object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EnterSingleCore(state, value, span, context);
    }

    private static Scope EnterSingleCore(StructuralGuardState state, object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        var limit = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        if (context?.Limits.MaxRecursionDepth is { } maxRec && maxRec < limit)
        {
            limit = maxRec;
        }

        if (state.Depth >= limit)
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }

        if (context is not null)
        {
            var recBudget = context.Limits.MaxRecursionDepth ?? LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
            if (context.Limits.CurrentRecursionDepth + state.Depth >= recBudget)
            {
                throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
            }

            if (context.Limits.CurrentInterpreterDepth + state.Depth >= LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
            }
        }

        var singles = state.Singles ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!singles.Add(value))
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }

        state.Depth++;
        return new Scope(state, value);
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

        var state = ThreadState;
        // Periodic cooperative checks for long structural scans: the outer expression
        // performs a single budget check on entry, which cannot stop a million-element
        // scan that never nests deeply. Throttle to every 64 elements to keep
        // per-element overhead negligible while remaining responsive to cancellation,
        // step budgets and host-call budgets.
        if ((++state.WorkTick & 63) != 0)
        {
            return;
        }

        context.CheckExecutionBudget(t_ambientSpan);
    }

    internal static void NoteWork(LythonRuntime.ExecutionContext? explicitContext, LythonSourceSpan? explicitSpan)
    {
        var context = explicitContext ?? t_ambientContext;
        if (context is null)
        {
            return;
        }

        var state = explicitContext?.Services.State.StructuralTraversal ?? ThreadState;
        if ((++state.WorkTick & 63) != 0)
        {
            return;
        }

        context.CheckExecutionBudget(explicitSpan ?? t_ambientSpan);
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

        context.CheckExecutionBudget(explicitSpan ?? t_ambientSpan);
    }

    // Test hook: thread-static depth for sync paths (async pair tracking lives
    // per-run and never touches thread state).
    internal static int ThreadDepthForTests => ThreadState.Depth;

    internal static int ThreadPairCountForTests => ThreadState.Pairs?.Count ?? 0;
}
