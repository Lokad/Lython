using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<object> AnyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<object> AllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static async ValueTask<object> SumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static void EnsureSummableValue(object value, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out _) || value is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() does not support string or bytes operands.", span);
        }
    }
}
