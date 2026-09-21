using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal static class PyStructuralGuard
{
    [ThreadStatic]
    private static int t_depth;
    [ThreadStatic]
    private static HashSet<StructuralPair>? t_pairs;
    [ThreadStatic]
    private static HashSet<object>? t_singles;
    [ThreadStatic]
    private static LythonRuntime.ExecutionContext? t_ambientContext;
    [ThreadStatic]
    private static LythonSourceSpan? t_ambientSpan;
    [ThreadStatic]
    private static int t_workTick;

    internal readonly struct Scope : IDisposable
    {
        private readonly StructuralPair _pair;
        private readonly object? _single;
        private readonly bool _isPair;
        private readonly bool _active;

        internal Scope(StructuralPair pair)
        {
            _pair = pair;
            _single = null;
            _isPair = true;
            _active = true;
        }

        internal Scope(object single)
        {
            _pair = default;
            _single = single;
            _isPair = false;
            _active = true;
        }

        public void Dispose()
        {
            if (!_active)
            {
                return;
            }

            if (_isPair)
            {
                t_pairs?.Remove(_pair);
            }
            else if (_single is not null)
            {
                t_singles?.Remove(_single);
            }

            if (t_depth > 0)
            {
                t_depth--;
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
        if (ReferenceEquals(left, right))
        {
            return default;
        }

        var limit = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        if (context?.Limits.MaxRecursionDepth is { } maxRec && maxRec < limit)
        {
            limit = maxRec;
        }

        if (t_depth >= limit)
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
        }

        if (context is not null)
        {
            var recBudget = context.Limits.MaxRecursionDepth ?? LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
            if (context.Limits.CurrentRecursionDepth + t_depth >= recBudget)
            {
                throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
            }

            if (context.Limits.CurrentInterpreterDepth + t_depth >= LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
            }
        }

        var pairs = t_pairs ??= new HashSet<StructuralPair>(StructuralPairComparer.Instance);
        var pair = new StructuralPair(left, right);
        var swapped = new StructuralPair(right, left);
        if (pairs.Contains(pair) || pairs.Contains(swapped))
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded in comparison", span);
        }

        t_depth++;
        pairs.Add(pair);
        return new Scope(pair);
    }

    internal static Scope EnterSingle(object value, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context = null)
    {
        var limit = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        if (context?.Limits.MaxRecursionDepth is { } maxRec && maxRec < limit)
        {
            limit = maxRec;
        }

        if (t_depth >= limit)
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }

        if (context is not null)
        {
            var recBudget = context.Limits.MaxRecursionDepth ?? LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
            if (context.Limits.CurrentRecursionDepth + t_depth >= recBudget)
            {
                throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
            }

            if (context.Limits.CurrentInterpreterDepth + t_depth >= LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Runtime("maximum interpreter stack depth exceeded", span);
            }
        }

        var singles = t_singles ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!singles.Add(value))
        {
            throw RuntimeErrors.Recursion("maximum recursion depth exceeded", span);
        }

        t_depth++;
        return new Scope(value);
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

        // Periodic cooperative checks for long structural scans: the outer expression
        // performs a single budget check on entry, which cannot stop a million-element
        // scan that never nests deeply. Throttle to every 64 elements to keep
        // per-element overhead negligible while remaining responsive to cancellation,
        // step budgets and host-call budgets.
        if ((++t_workTick & 63) != 0)
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

        if ((++t_workTick & 63) != 0)
        {
            return;
        }

        context.CheckExecutionBudget(explicitSpan ?? t_ambientSpan);
    }
}
