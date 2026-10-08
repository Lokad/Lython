using System.Runtime.ExceptionServices;

namespace Lokad.Lython.Runtime;

// Native List.Sort wraps comparer failures. Checkpoints must retain the Python
// failure identity so callers cannot turn cancellation into a host/CLR error.
internal static class ExecutionSort
{
    internal static void Sort<T>(List<T> values, LythonRuntime.ExecutionContext? context,
        LythonSourceSpan? span, Comparison<T>? comparison = null)
    {
        context?.CheckExecution(span);
        if (context is null || values.Count < 2)
        {
            values.Sort(comparison ?? Comparer<T>.Default.Compare);
            return;
        }

        // The wrapper's closure/delegate are operation-local scratch; the caller
        // continues to own/fund the list and any materialization backing.
        using var scratch = context.MemoryGovernor.ReserveTemporary(256, span);
        var compare = comparison ?? Comparer<T>.Default.Compare;
        var untilCheck = 64;
        try
        {
            values.Sort((left, right) =>
            {
                if (--untilCheck == 0)
                {
                    untilCheck = 64;
                    context.CheckExecution(span);
                }
                return compare(left, right);
            });
        }
        catch (InvalidOperationException exception) when (exception.InnerException is LythonRuntimeException)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
        context.CheckExecution(span);
    }
}
